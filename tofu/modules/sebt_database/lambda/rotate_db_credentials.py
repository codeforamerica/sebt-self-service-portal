"""Alternating users rotation Lambda for RDS SQL Server.

Implements the Secrets Manager four-step rotation protocol. Two SQL Server
logins — 'appuser' and 'appuser_clone' — alternate as the active credential
each cycle. The inactive user's password is updated while the active user's
credentials remain valid throughout the ECS rolling restart, closing the
credential staleness window entirely.

Environment variables (set by OpenTofu):
    ADMIN_SECRET_ARN — ARN of the Secrets Manager secret holding RDS admin
                       credentials (the RDS-managed master-user secret).
    DB_HOST          — RDS SQL Server hostname.
    DB_PORT          — SQL Server port (default: "1433").
    DB_NAME          — Target database name for user creation and connection tests.
    ECS_CLUSTER      — ECS cluster name to redeploy after finishSecret.
    ECS_SERVICE      — ECS service name to redeploy after finishSecret.
"""

import json
import logging
import os
import secrets
import string

import boto3
import pymssql

logger = logging.getLogger()
logger.setLevel(logging.INFO)

_USERS = ("appuser", "appuser_clone")


def handler(event, context):
    """Entry point invoked by Secrets Manager for each rotation step."""
    secret_arn = event["SecretId"]
    token = event["ClientRequestToken"]
    step = event["Step"]

    client = boto3.client("secretsmanager")

    metadata = client.describe_secret(SecretId=secret_arn)
    if not metadata.get("RotationEnabled"):
        raise ValueError(f"Rotation not enabled for {secret_arn}")

    versions = metadata["VersionIdsToStages"]
    if token not in versions:
        raise ValueError(f"Version {token} not staged for rotation of {secret_arn}")
    if "AWSCURRENT" in versions[token]:
        logger.info("Version %s already AWSCURRENT — skipping", token)
        return
    if "AWSPENDING" not in versions[token]:
        raise ValueError(f"Version {token} not AWSPENDING for {secret_arn}")

    if step == "createSecret":
        create_secret(client, secret_arn, token)
    elif step == "setSecret":
        set_secret(client, secret_arn, token)
    elif step == "testSecret":
        test_secret(client, secret_arn, token)
    elif step == "finishSecret":
        finish_secret(client, secret_arn, token)
    else:
        raise ValueError(f"Invalid rotation step: {step}")


def create_secret(client, secret_arn, token):
    """Generate a new password for the inactive user and store as AWSPENDING."""
    try:
        client.get_secret_value(
            SecretId=secret_arn,
            VersionId=token,
            VersionStage="AWSPENDING",
        )
        logger.info("AWSPENDING already set for token %s — skipping createSecret", token)
        return
    except client.exceptions.ResourceNotFoundException:
        logger.info("AWSPENDING not yet set for token %s — will create new secret", token)

    current = _get_secret_dict(client, secret_arn, stage="AWSCURRENT")
    pending_user = _other_user(current["username"])
    new_password = _generate_password()

    client.put_secret_value(
        SecretId=secret_arn,
        ClientRequestToken=token,
        SecretString=json.dumps({
            "username": pending_user,
            "password": new_password,
            "host": current["host"],
            "port": current["port"],
            "dbname": current["dbname"],
        }),
        VersionStages=["AWSPENDING"],
    )
    logger.info("Stored AWSPENDING credentials for user %s", pending_user)


def set_secret(client, secret_arn, token):
    """Update the inactive user's password in SQL Server using admin credentials."""
    pending = _get_secret_dict(client, secret_arn, stage="AWSPENDING", version_id=token)
    admin = _get_admin_credentials(client)

    host = os.environ["DB_HOST"]
    port = int(os.environ.get("DB_PORT", "1433"))
    dbname = pending["dbname"]

    # Server-level operations (CREATE/ALTER LOGIN) require master db context.
    master_conn = pymssql.connect(
        server=host,
        port=port,
        user=admin["username"],
        password=admin["password"],
        database="master",
        tds_version="7.4",
    )
    try:
        cursor = master_conn.cursor()
        _ensure_login_exists(cursor, pending["username"], pending["password"])
        master_conn.commit()
        _alter_login_password(cursor, pending["username"], pending["password"])
        master_conn.commit()
        logger.info("Updated SQL Server password for pending login")
    finally:
        master_conn.close()

    # Database-level operations (CREATE USER, role grant) require the target db.
    db_conn = pymssql.connect(
        server=host,
        port=port,
        user=admin["username"],
        password=admin["password"],
        database=dbname,
        tds_version="7.4",
    )
    try:
        cursor = db_conn.cursor()
        _ensure_db_user_exists(cursor, pending["username"])
        db_conn.commit()
    finally:
        db_conn.close()


def test_secret(client, secret_arn, token):
    """Verify the AWSPENDING credentials by opening a test connection."""
    pending = _get_secret_dict(client, secret_arn, stage="AWSPENDING", version_id=token)

    conn = pymssql.connect(
        server=os.environ["DB_HOST"],
        port=int(os.environ.get("DB_PORT", "1433")),
        user=pending["username"],
        password=pending["password"],
        database=pending["dbname"],
        tds_version="7.4",
    )
    try:
        conn.cursor().execute("SELECT 1")
        logger.info("Test connection succeeded for pending login")
    finally:
        conn.close()


def finish_secret(client, secret_arn, token):
    """Promote AWSPENDING to AWSCURRENT and trigger an ECS rolling restart."""
    metadata = client.describe_secret(SecretId=secret_arn)
    current_version = None
    for version_id, stages in metadata["VersionIdsToStages"].items():
        if "AWSCURRENT" in stages:
            if version_id == token:
                logger.info("Version %s already AWSCURRENT — skipping finishSecret", token)
                return
            current_version = version_id
            break

    client.update_secret_version_stage(
        SecretId=secret_arn,
        VersionStage="AWSCURRENT",
        MoveToVersionId=token,
        RemoveFromVersionId=current_version,
    )
    logger.info("Promoted version %s to AWSCURRENT", token)

    _restart_ecs_service()


# ---------------------------------------------------------------------------
# Private helpers
# ---------------------------------------------------------------------------

def _other_user(username):
    """Return the inactive app user (the one not currently active)."""
    if username not in _USERS:
        raise ValueError(f"Unexpected username '{username}': must be one of {_USERS}")
    return _USERS[1] if username == _USERS[0] else _USERS[0]


def _generate_password(length=32):
    """Generate a random password that satisfies SQL Server complexity rules."""
    upper = string.ascii_uppercase
    lower = string.ascii_lowercase
    digits = string.digits
    symbols = "!@#$%^&*"
    alphabet = upper + lower + digits + symbols
    required = [
        secrets.choice(upper),
        secrets.choice(lower),
        secrets.choice(digits),
        secrets.choice(symbols),
    ]
    rest = [secrets.choice(alphabet) for _ in range(length - len(required))]
    combined = required + rest
    secrets.SystemRandom().shuffle(combined)
    return "".join(combined)


def _escape_sql_str(value):
    """Escape a value for embedding in a SQL single-quoted string literal."""
    return value.replace("'", "''")


def _get_secret_dict(client, secret_arn, stage, version_id=None):
    kwargs = {"SecretId": secret_arn, "VersionStage": stage}
    if version_id:
        kwargs["VersionId"] = version_id
    return json.loads(client.get_secret_value(**kwargs)["SecretString"])


def _get_admin_credentials(client):
    return _get_secret_dict(client, os.environ["ADMIN_SECRET_ARN"], stage="AWSCURRENT")


def _ensure_login_exists(cursor, username, password):
    """Create the SQL Server login if it does not already exist."""
    escaped_name = _escape_sql_str(username)
    escaped_pw = _escape_sql_str(password)
    cursor.execute(
        f"IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'{escaped_name}') "
        f"BEGIN "
        f"  CREATE LOGIN [{username}] WITH PASSWORD = N'{escaped_pw}', "
        f"  CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF "
        f"END"
    )


def _alter_login_password(cursor, username, password):
    """Change an existing SQL Server login's password."""
    escaped_pw = _escape_sql_str(password)
    cursor.execute(f"ALTER LOGIN [{username}] WITH PASSWORD = N'{escaped_pw}'")


def _ensure_db_user_exists(cursor, username):
    """Create the database user and grant db_owner if it does not already exist."""
    escaped_name = _escape_sql_str(username)
    cursor.execute(
        f"IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'{escaped_name}') "
        f"BEGIN "
        f"  CREATE USER [{username}] FOR LOGIN [{username}] "
        f"END"
    )
    cursor.execute(f"ALTER ROLE [db_owner] ADD MEMBER [{username}]")


def _restart_ecs_service():
    """Trigger an ECS rolling restart so new tasks pick up the new credentials."""
    cluster = os.environ["ECS_CLUSTER"]
    service = os.environ["ECS_SERVICE"]
    try:
        boto3.client("ecs").update_service(
            cluster=cluster,
            service=service,
            forceNewDeployment=True,
        )
        logger.info("Triggered rolling ECS restart for %s/%s", cluster, service)
    except Exception:
        # The secret is already rotated; running tasks remain on the active
        # user's valid credentials. A manual redeploy will pick up the new
        # secret at next launch.
        logger.exception(
            "Failed to trigger ECS restart for %s/%s — manual redeploy required",
            cluster,
            service,
        )
