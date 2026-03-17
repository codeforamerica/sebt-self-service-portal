"""Secrets Manager rotation handler for SES SMTP credentials.

Implements the four-step Secrets Manager rotation protocol, adapted for
ECS Fargate from the AWS sample at:
https://github.com/aws-samples/serverless-mail/tree/ses-credential-rotation/ses-credential-rotation

Key differences from the AWS sample:
  - Secret stored as JSON ({"username", "password"}) instead of "username:password"
    because ECS environment_secrets extracts JSON fields by key.
  - ECS force-redeployment replaces SSM Run Command for credential delivery.
  - Two-key deferred cleanup: the old key is deleted at the START of the next
    rotation cycle (not immediately), giving ECS tasks time to roll over.

Environment variables (set by OpenTofu):
    IAM_USERNAME   – Name of the IAM user that owns the SMTP access keys.
    SMTP_ENDPOINT  – SES SMTP endpoint (e.g. email-smtp.us-east-1.amazonaws.com).
    ECS_CLUSTER    – ECS cluster name for triggering redeployment.
    ECS_SERVICE    – ECS service name for triggering redeployment.

See: https://docs.aws.amazon.com/secretsmanager/latest/userguide/rotating-secrets-lambda-function-overview.html
See: https://docs.aws.amazon.com/ses/latest/dg/smtp-credentials.html
"""

import base64
import hashlib
import hmac
import json
import logging
import os

import boto3
import botocore

logger = logging.getLogger()
logger.setLevel(logging.INFO)

# These values are required to calculate the SMTP signature. Do not change them.
# https://github.com/aws-samples/serverless-mail/blob/ses-credential-rotation/ses-credential-rotation/ses_credential_rotator/lambda_function.py#L14-L19
DATE = "11111111"
SERVICE = "ses"
MESSAGE = "SendRawEmail"
TERMINAL = "aws4_request"
VERSION = 0x04


# SMTP password derivation from the AWS sample:
# https://github.com/aws-samples/serverless-mail/blob/ses-credential-rotation/ses-credential-rotation/ses_credential_rotator/lambda_function.py#L22-L35
def sign(key, msg):
    return hmac.new(key, msg.encode("utf-8"), hashlib.sha256).digest()


def calculate_key(secret_access_key, region):
    signature = sign(("AWS4" + secret_access_key).encode("utf-8"), DATE)
    signature = sign(signature, region)
    signature = sign(signature, SERVICE)
    signature = sign(signature, TERMINAL)
    signature = sign(signature, MESSAGE)
    signature_and_version = bytes([VERSION]) + signature
    smtp_password = base64.b64encode(signature_and_version)
    return smtp_password.decode("utf-8")


# ---------------------------------------------------------------------------
# Rotation steps
# ---------------------------------------------------------------------------


def create_secret(secret_client, secret_arn, token, iam_username, region):
    """Create a new IAM access key and store it as AWSPENDING.

    Adapted from the AWS sample:
    https://github.com/aws-samples/serverless-mail/blob/ses-credential-rotation/ses-credential-rotation/ses_credential_rotator/lambda_function.py#L38-L63

    Differences:
      - Cleans up leftover key from previous rotation BEFORE creating a new
        one (two-key deferred cleanup strategy).
      - Stores the secret as JSON instead of "username:password" string.
    """
    # Check if AWSPENDING already has a value (idempotency for retries).
    try:
        secret_client.get_secret_value(
            SecretId=secret_arn,
            VersionId=token,
            VersionStage="AWSPENDING",
        )
        logger.info("AWSPENDING already exists for token %s. Skipping.", token)
        return
    except secret_client.exceptions.ResourceNotFoundException:
        pass

    # Get the current secret so we know which key ID to keep.
    current = secret_client.get_secret_value(
        SecretId=secret_arn, VersionStage="AWSCURRENT"
    )
    current_creds = json.loads(current["SecretString"])
    current_key_id = current_creds["username"]

    # Clean up any leftover key from the previous rotation cycle. IAM allows
    # at most 2 access keys per user, so we must make room before creating a
    # new one. The key matching current_key_id is still in use by running ECS
    # tasks; any other key is a leftover that can be safely deleted.
    iam_client = boto3.client("iam")
    _cleanup_old_keys(iam_client, iam_username, current_key_id)

    # Create new access key and derive SMTP password.
    # https://github.com/aws-samples/serverless-mail/blob/ses-credential-rotation/ses-credential-rotation/ses_credential_rotator/lambda_function.py#L41-L49
    new_key = iam_client.create_access_key(UserName=iam_username)
    new_access_key = new_key["AccessKey"]["AccessKeyId"]
    new_secret_key = new_key["AccessKey"]["SecretAccessKey"]
    new_smtp_password = calculate_key(new_secret_key, region)

    logger.info("Created new access key %s for user %s.", new_access_key, iam_username)

    # Store the new credentials as AWSPENDING.
    # The AWS sample stores as "username:password"; we use JSON because ECS
    # environment_secrets extracts fields by JSON key.
    try:
        secret_client.put_secret_value(
            SecretId=secret_arn,
            ClientRequestToken=token,
            SecretString=json.dumps(
                {"username": new_access_key, "password": new_smtp_password}
            ),
            VersionStages=["AWSPENDING"],
        )
    except botocore.exceptions.ClientError as error:
        # If we can't store the secret, clean up the IAM key we just created.
        # https://github.com/aws-samples/serverless-mail/blob/ses-credential-rotation/ses-credential-rotation/ses_credential_rotator/lambda_function.py#L52-L63
        logger.error("put_secret_value failed, removing new IAM key: %s", error)
        iam_client.delete_access_key(
            UserName=iam_username, AccessKeyId=new_access_key
        )
        raise


def set_secret():
    """No-op. IAM access keys are active immediately after creation.

    https://github.com/aws-samples/serverless-mail/blob/ses-credential-rotation/ses-credential-rotation/ses_credential_rotator/lambda_function.py#L66-L69
    """
    return


def test_secret(secret_client, secret_arn, token):
    """Verify the new access key is active.

    The AWS sample tests via SMTP login:
    https://github.com/aws-samples/serverless-mail/blob/ses-credential-rotation/ses-credential-rotation/ses_credential_rotator/lambda_function.py#L72-L103

    We verify the IAM key status instead, because our Lambda runs outside the
    VPC and cannot reach the SMTP endpoint directly. The SMTP password is
    derived deterministically from the secret access key using the AWS-
    documented algorithm, so if the IAM key is active the SMTP credentials
    will work.
    """
    pending = secret_client.get_secret_value(
        SecretId=secret_arn,
        VersionId=token,
        VersionStage="AWSPENDING",
    )
    pending_creds = json.loads(pending["SecretString"])
    pending_key_id = pending_creds["username"]

    iam_client = boto3.client("iam")
    keys = iam_client.list_access_keys(UserName=os.environ["IAM_USERNAME"])[
        "AccessKeyMetadata"
    ]
    for key in keys:
        if key["AccessKeyId"] == pending_key_id:
            if key["Status"] != "Active":
                raise ValueError(
                    f"Access key {pending_key_id} exists but status is "
                    f"{key['Status']}, expected Active."
                )
            logger.info("Access key %s is Active. Test passed.", pending_key_id)
            return

    raise ValueError(
        f"Access key {pending_key_id} not found on IAM user "
        f"{os.environ['IAM_USERNAME']}."
    )


def finish_secret(secret_client, secret_arn, token):
    """Promote AWSPENDING to AWSCURRENT and trigger ECS redeployment.

    The version promotion reuses the AWS sample's logic:
    https://github.com/aws-samples/serverless-mail/blob/ses-credential-rotation/ses-credential-rotation/ses_credential_rotator/lambda_function.py#L227-L245

    Instead of the AWS sample's SSM Run Command to push credentials to EC2
    instances, we trigger an ECS force-redeployment so Fargate tasks pick up
    the new secret values on next launch. The old IAM key is NOT deleted here
    (unlike the AWS sample) — it stays active until the next rotation cycle
    so that in-flight ECS tasks are not disrupted.
    """
    _mark_new_secret_as_current(secret_client, secret_arn, token)

    # Trigger a rolling redeployment so ECS tasks pick up the new credentials.
    try:
        ecs_client = boto3.client("ecs")
        ecs_client.update_service(
            cluster=os.environ["ECS_CLUSTER"],
            service=os.environ["ECS_SERVICE"],
            forceNewDeployment=True,
        )
        logger.info(
            "Triggered ECS redeployment for %s/%s.",
            os.environ["ECS_CLUSTER"],
            os.environ["ECS_SERVICE"],
        )
    except Exception:
        # Log but do not fail the rotation — the secret is already updated.
        # Old tasks continue working because the previous key is still active.
        logger.exception(
            "Failed to trigger ECS redeployment. The secret was rotated "
            "successfully, but running tasks still have old credentials. "
            "A manual redeployment may be needed."
        )


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def _cleanup_old_keys(iam_client, iam_username, current_key_id):
    """Delete any access key that isn't the current one.

    IAM allows at most 2 access keys per user. Before creating a new key,
    we must ensure there's room. The key matching current_key_id is kept
    (it's still in use by running ECS tasks); any other key is a leftover
    from the previous rotation and can be safely deleted.
    """
    keys = iam_client.list_access_keys(UserName=iam_username)["AccessKeyMetadata"]
    for key in keys:
        if key["AccessKeyId"] != current_key_id:
            iam_client.delete_access_key(
                UserName=iam_username, AccessKeyId=key["AccessKeyId"]
            )
            logger.info(
                "Deleted old access key %s (keeping %s).",
                key["AccessKeyId"],
                current_key_id,
            )


# Reused from the AWS sample:
# https://github.com/aws-samples/serverless-mail/blob/ses-credential-rotation/ses-credential-rotation/ses_credential_rotator/lambda_function.py#L227-L245
def _mark_new_secret_as_current(secret_client, secret_arn, token):
    """Move the AWSCURRENT staging label to the new secret version."""
    metadata = secret_client.describe_secret(SecretId=secret_arn)
    current_version = None
    for version in metadata["VersionIdsToStages"]:
        if "AWSCURRENT" in metadata["VersionIdsToStages"][version]:
            if version == token:
                logger.info(
                    "Version %s already marked as AWSCURRENT for %s.",
                    version,
                    secret_arn,
                )
                return
            current_version = version
            break

    secret_client.update_secret_version_stage(
        SecretId=secret_arn,
        VersionStage="AWSCURRENT",
        MoveToVersionId=token,
        RemoveFromVersionId=current_version,
    )
    logger.info(
        "Successfully set AWSCURRENT stage to version %s for secret %s.",
        token,
        secret_arn,
    )


# Entry point — structure reused from the AWS sample:
# https://github.com/aws-samples/serverless-mail/blob/ses-credential-rotation/ses-credential-rotation/ses_credential_rotator/lambda_function.py#L248-L301
def handler(event, context):
    """Entry point invoked by Secrets Manager for each rotation step."""
    logger.info("Received event: %s", event)

    secret_arn = event["SecretId"]
    token = event["ClientRequestToken"]
    step = event["Step"]

    iam_username = os.environ["IAM_USERNAME"]

    secret_client = boto3.client("secretsmanager")

    # Validate the secret version is staged correctly.
    metadata = secret_client.describe_secret(SecretId=secret_arn)
    if not metadata.get("RotationEnabled"):
        raise ValueError(f"Secret {secret_arn} does not have rotation enabled.")

    versions = metadata["VersionIdsToStages"]
    if token not in versions:
        raise ValueError(
            f"Secret version {token} has no stage for rotation of "
            f"secret {secret_arn}."
        )
    if "AWSCURRENT" in versions[token]:
        logger.info(
            "Secret version %s already set as AWSCURRENT for secret %s.",
            token,
            secret_arn,
        )
        return
    elif "AWSPENDING" not in versions[token]:
        raise ValueError(
            f"Secret version {token} not set as AWSPENDING for rotation of "
            f"secret {secret_arn}."
        )

    if step == "createSecret":
        logger.info("Executing createSecret.")
        region = os.environ["AWS_REGION"]
        create_secret(secret_client, secret_arn, token, iam_username, region)
    elif step == "setSecret":
        logger.info("Executing setSecret.")
        set_secret()
    elif step == "testSecret":
        logger.info("Executing testSecret.")
        test_secret(secret_client, secret_arn, token)
    elif step == "finishSecret":
        logger.info("Executing finishSecret.")
        finish_secret(secret_client, secret_arn, token)
    else:
        raise ValueError(f"Invalid step parameter: {step}")
