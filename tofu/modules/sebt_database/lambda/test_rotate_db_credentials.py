"""Unit tests for the DB credential rotation Lambda.

Run with:
    pip install pytest pymssql==2.3.1
    pytest tofu/modules/sebt_database/lambda/test_rotate_db_credentials.py -v
"""
import json
import os
import sys
from unittest.mock import MagicMock, patch

import pytest

sys.path.insert(0, os.path.dirname(__file__))

import rotate_db_credentials as rot


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _secret_value(username="appuser", password="OldPass1!", host="db.example.com"):
    return {
        "SecretString": json.dumps({
            "username": username,
            "password": password,
            "host": host,
            "port": "1433",
            "dbname": "SebtPortal",
        })
    }


def _make_client(token, current_user="appuser"):
    client = MagicMock()
    client.exceptions.ResourceNotFoundException = Exception
    client.describe_secret.return_value = {
        "RotationEnabled": True,
        "VersionIdsToStages": {
            "prev-token": ["AWSCURRENT"],
            token: ["AWSPENDING"],
        },
    }
    client.get_secret_value.side_effect = lambda **kw: (
        {"SecretString": json.dumps({"username": "admin", "password": "AdminPw1!"})}
        if kw.get("SecretId") == "admin-arn"
        else _secret_value(username=current_user)
    )
    return client


# ---------------------------------------------------------------------------
# _other_user
# ---------------------------------------------------------------------------

def test_other_user_returns_clone_when_given_primary():
    assert rot._other_user("appuser") == "appuser_clone"


def test_other_user_returns_primary_when_given_clone():
    assert rot._other_user("appuser_clone") == "appuser"


def test_other_user_raises_on_unknown_username():
    with pytest.raises(ValueError, match="Unexpected username"):
        rot._other_user("someotheruser")


# ---------------------------------------------------------------------------
# _generate_password
# ---------------------------------------------------------------------------

def test_generate_password_length():
    pw = rot._generate_password()
    assert len(pw) == 32


def test_generate_password_has_required_categories():
    import string
    pw = rot._generate_password()
    assert any(c in string.ascii_uppercase for c in pw)
    assert any(c in string.ascii_lowercase for c in pw)
    assert any(c in string.digits for c in pw)
    assert any(c in "!@#$%^&*" for c in pw)


def test_generate_password_uniqueness():
    assert rot._generate_password() != rot._generate_password()


# ---------------------------------------------------------------------------
# create_secret
# ---------------------------------------------------------------------------

@patch.dict(os.environ, {"ADMIN_SECRET_ARN": "admin-arn", "DB_HOST": "db", "DB_NAME": "SebtPortal",
                         "ECS_CLUSTER": "cluster", "ECS_SERVICE": "svc"})
def test_create_secret_stores_pending_for_inactive_user():
    client = _make_client("new-token", current_user="appuser")
    # Idempotency check raises → no AWSPENDING yet → proceed
    client.get_secret_value.side_effect = [
        Exception("not found"),
        _secret_value("appuser"),
    ]

    rot.create_secret(client, "secret-arn", "new-token")

    client.put_secret_value.assert_called_once()
    stored = json.loads(client.put_secret_value.call_args.kwargs["SecretString"])
    assert stored["username"] == "appuser_clone"
    assert client.put_secret_value.call_args.kwargs["VersionStages"] == ["AWSPENDING"]


@patch.dict(os.environ, {"ADMIN_SECRET_ARN": "admin-arn", "DB_HOST": "db", "DB_NAME": "SebtPortal",
                         "ECS_CLUSTER": "cluster", "ECS_SERVICE": "svc"})
def test_create_secret_is_idempotent_when_pending_already_set():
    client = _make_client("new-token")
    client.get_secret_value.return_value = _secret_value("appuser_clone")

    rot.create_secret(client, "secret-arn", "new-token")

    client.put_secret_value.assert_not_called()


# ---------------------------------------------------------------------------
# set_secret
# ---------------------------------------------------------------------------

@patch.dict(os.environ, {"ADMIN_SECRET_ARN": "admin-arn", "DB_HOST": "db", "DB_PORT": "1433",
                         "DB_NAME": "SebtPortal", "ECS_CLUSTER": "cluster", "ECS_SERVICE": "svc"})
@patch("rotate_db_credentials.pymssql")
def test_set_secret_opens_connections_and_commits(mock_pymssql):
    mock_conn = MagicMock()
    mock_pymssql.connect.return_value = mock_conn

    client = MagicMock()
    client.get_secret_value.side_effect = [
        {"SecretString": json.dumps({"username": "appuser_clone", "password": "NewPw1!",
                                     "host": "db", "port": "1433", "dbname": "SebtPortal"})},
        {"SecretString": json.dumps({"username": "admin", "password": "AdminPw1!"})},
    ]

    rot.set_secret(client, "secret-arn", "new-token")

    assert mock_pymssql.connect.call_count == 2
    assert mock_conn.commit.call_count >= 2
    mock_conn.close.assert_called()


# ---------------------------------------------------------------------------
# test_secret
# ---------------------------------------------------------------------------

@patch.dict(os.environ, {"ADMIN_SECRET_ARN": "admin-arn", "DB_HOST": "db", "DB_PORT": "1433",
                         "DB_NAME": "SebtPortal", "ECS_CLUSTER": "cluster", "ECS_SERVICE": "svc"})
@patch("rotate_db_credentials.pymssql")
def test_test_secret_succeeds_on_valid_connection(mock_pymssql):
    mock_conn = MagicMock()
    mock_pymssql.connect.return_value = mock_conn

    client = MagicMock()
    client.get_secret_value.return_value = {
        "SecretString": json.dumps({"username": "appuser_clone", "password": "NewPw1!",
                                    "host": "db", "port": "1433", "dbname": "SebtPortal"})
    }

    rot.test_secret(client, "secret-arn", "new-token")

    mock_conn.cursor.return_value.execute.assert_called_once_with("SELECT 1")
    mock_conn.close.assert_called_once()


@patch.dict(os.environ, {"ADMIN_SECRET_ARN": "admin-arn", "DB_HOST": "db", "DB_PORT": "1433",
                         "DB_NAME": "SebtPortal", "ECS_CLUSTER": "cluster", "ECS_SERVICE": "svc"})
@patch("rotate_db_credentials.pymssql")
def test_test_secret_raises_on_failed_connection(mock_pymssql):
    mock_pymssql.connect.side_effect = Exception("connection refused")

    client = MagicMock()
    client.get_secret_value.return_value = {
        "SecretString": json.dumps({"username": "appuser_clone", "password": "BadPw1!",
                                    "host": "db", "port": "1433", "dbname": "SebtPortal"})
    }

    with pytest.raises(Exception, match="connection refused"):
        rot.test_secret(client, "secret-arn", "new-token")


# ---------------------------------------------------------------------------
# finish_secret
# ---------------------------------------------------------------------------

@patch.dict(os.environ, {"ADMIN_SECRET_ARN": "admin-arn", "DB_HOST": "db", "DB_NAME": "SebtPortal",
                         "ECS_CLUSTER": "cluster", "ECS_SERVICE": "svc"})
@patch("rotate_db_credentials.boto3")
def test_finish_secret_promotes_pending_to_current(mock_boto3):
    mock_secrets = MagicMock()
    mock_secrets.describe_secret.return_value = {
        "VersionIdsToStages": {
            "prev-token": ["AWSCURRENT"],
            "new-token": ["AWSPENDING"],
        }
    }

    rot.finish_secret(mock_secrets, "secret-arn", "new-token")

    mock_secrets.update_secret_version_stage.assert_called_once_with(
        SecretId="secret-arn",
        VersionStage="AWSCURRENT",
        MoveToVersionId="new-token",
        RemoveFromVersionId="prev-token",
    )


@patch.dict(os.environ, {"ADMIN_SECRET_ARN": "admin-arn", "DB_HOST": "db", "DB_NAME": "SebtPortal",
                         "ECS_CLUSTER": "cluster", "ECS_SERVICE": "svc"})
@patch("rotate_db_credentials.boto3")
def test_finish_secret_skips_if_already_current(mock_boto3):
    mock_secrets = MagicMock()
    mock_secrets.describe_secret.return_value = {
        "VersionIdsToStages": {
            "new-token": ["AWSCURRENT"],
        }
    }

    rot.finish_secret(mock_secrets, "secret-arn", "new-token")

    mock_secrets.update_secret_version_stage.assert_not_called()
