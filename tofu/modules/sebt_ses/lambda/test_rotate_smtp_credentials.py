"""Unit tests for the SES SMTP credential rotation Lambda.

Run with:
    pip install pytest boto3
    pytest tofu/modules/sebt_ses/lambda/test_rotate_smtp_credentials.py -v
"""
import json
import os
import sys
from unittest.mock import MagicMock, patch

import botocore.exceptions
import pytest

sys.path.insert(0, os.path.dirname(__file__))

import rotate_smtp_credentials as rot


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _make_secret_client(token, current_key_id="AKIA_CURRENT"):
    client = MagicMock()
    client.exceptions.ResourceNotFoundException = Exception
    client.describe_secret.return_value = {
        "RotationEnabled": True,
        "VersionIdsToStages": {
            "prev-token": ["AWSCURRENT"],
            token: ["AWSPENDING"],
        },
    }
    client.get_secret_value.return_value = {
        "SecretString": json.dumps({"username": current_key_id, "password": "old-smtp-pass"})
    }
    return client


_ENV = {"IAM_USERNAME": "myuser", "ECS_CLUSTER": "cluster", "ECS_SERVICE": "svc"}


# ---------------------------------------------------------------------------
# calculate_key
# ---------------------------------------------------------------------------

def test_calculate_key_returns_a_non_empty_string():
    result = rot.calculate_key("test_secret_key", "us-east-1")
    assert isinstance(result, str) and len(result) > 0


def test_calculate_key_is_deterministic():
    assert rot.calculate_key("test_secret_key", "us-east-1") == rot.calculate_key("test_secret_key", "us-east-1")


def test_calculate_key_differs_by_region():
    assert rot.calculate_key("test_secret_key", "us-east-1") != rot.calculate_key("test_secret_key", "us-west-2")


def test_calculate_key_differs_by_secret():
    assert rot.calculate_key("secret_a", "us-east-1") != rot.calculate_key("secret_b", "us-east-1")


# ---------------------------------------------------------------------------
# _cleanup_old_keys
# ---------------------------------------------------------------------------

def test_cleanup_old_keys_deletes_non_current_key():
    iam = MagicMock()
    iam.list_access_keys.return_value = {
        "AccessKeyMetadata": [
            {"AccessKeyId": "AKIA_CURRENT"},
            {"AccessKeyId": "AKIA_OLD"},
        ]
    }
    rot._cleanup_old_keys(iam, "myuser", "AKIA_CURRENT")
    iam.delete_access_key.assert_called_once_with(UserName="myuser", AccessKeyId="AKIA_OLD")


def test_cleanup_old_keys_skips_current_key():
    iam = MagicMock()
    iam.list_access_keys.return_value = {
        "AccessKeyMetadata": [{"AccessKeyId": "AKIA_CURRENT"}]
    }
    rot._cleanup_old_keys(iam, "myuser", "AKIA_CURRENT")
    iam.delete_access_key.assert_not_called()


def test_cleanup_old_keys_deletes_multiple_old_keys():
    iam = MagicMock()
    iam.list_access_keys.return_value = {
        "AccessKeyMetadata": [
            {"AccessKeyId": "AKIA_CURRENT"},
            {"AccessKeyId": "AKIA_OLD_1"},
            {"AccessKeyId": "AKIA_OLD_2"},
        ]
    }
    rot._cleanup_old_keys(iam, "myuser", "AKIA_CURRENT")
    assert iam.delete_access_key.call_count == 2


# ---------------------------------------------------------------------------
# create_secret
# ---------------------------------------------------------------------------

@patch.dict(os.environ, _ENV)
@patch("rotate_smtp_credentials.boto3")
def test_create_secret_is_idempotent_when_pending_exists(mock_boto3):
    client = MagicMock()
    client.get_secret_value.return_value = {
        "SecretString": json.dumps({"username": "AKIA", "password": "pass"})
    }
    rot.create_secret(client, "secret-arn", "token", "myuser", "us-east-1")
    client.put_secret_value.assert_not_called()


@patch.dict(os.environ, _ENV)
@patch("rotate_smtp_credentials.boto3")
def test_create_secret_creates_new_key_and_stores_pending(mock_boto3):
    client = MagicMock()
    client.exceptions.ResourceNotFoundException = Exception
    client.get_secret_value.side_effect = [
        Exception("not found"),
        {"SecretString": json.dumps({"username": "AKIA_CURRENT", "password": "old"})},
    ]

    mock_iam = MagicMock()
    mock_iam.list_access_keys.return_value = {"AccessKeyMetadata": [{"AccessKeyId": "AKIA_CURRENT"}]}
    mock_iam.create_access_key.return_value = {
        "AccessKey": {"AccessKeyId": "AKIA_NEW", "SecretAccessKey": "new_secret_key"}
    }
    mock_boto3.client.return_value = mock_iam

    rot.create_secret(client, "secret-arn", "token", "myuser", "us-east-1")

    client.put_secret_value.assert_called_once()
    stored = json.loads(client.put_secret_value.call_args.kwargs["SecretString"])
    assert stored["username"] == "AKIA_NEW"
    assert "password" in stored


@patch.dict(os.environ, _ENV)
@patch("rotate_smtp_credentials.boto3")
def test_create_secret_deletes_new_key_on_put_failure(mock_boto3):
    client = MagicMock()
    client.exceptions.ResourceNotFoundException = Exception
    client.get_secret_value.side_effect = [
        Exception("not found"),
        {"SecretString": json.dumps({"username": "AKIA_CURRENT", "password": "old"})},
    ]
    client.put_secret_value.side_effect = botocore.exceptions.ClientError(
        {"Error": {"Code": "InternalServiceError", "Message": "error"}}, "PutSecretValue"
    )

    mock_iam = MagicMock()
    mock_iam.list_access_keys.return_value = {"AccessKeyMetadata": []}
    mock_iam.create_access_key.return_value = {
        "AccessKey": {"AccessKeyId": "AKIA_NEW", "SecretAccessKey": "new_secret_key"}
    }
    mock_boto3.client.return_value = mock_iam

    with pytest.raises(botocore.exceptions.ClientError):
        rot.create_secret(client, "secret-arn", "token", "myuser", "us-east-1")

    mock_iam.delete_access_key.assert_called_once_with(UserName="myuser", AccessKeyId="AKIA_NEW")


# ---------------------------------------------------------------------------
# test_secret
# ---------------------------------------------------------------------------

@patch.dict(os.environ, _ENV)
@patch("rotate_smtp_credentials.boto3")
def test_test_secret_passes_when_key_is_active(mock_boto3):
    client = MagicMock()
    client.get_secret_value.return_value = {
        "SecretString": json.dumps({"username": "AKIA_NEW", "password": "pass"})
    }
    mock_iam = MagicMock()
    mock_iam.list_access_keys.return_value = {
        "AccessKeyMetadata": [{"AccessKeyId": "AKIA_NEW", "Status": "Active"}]
    }
    mock_boto3.client.return_value = mock_iam

    rot.test_secret(client, "secret-arn", "token")  # should not raise


@patch.dict(os.environ, _ENV)
@patch("rotate_smtp_credentials.boto3")
def test_test_secret_raises_when_key_is_inactive(mock_boto3):
    client = MagicMock()
    client.get_secret_value.return_value = {
        "SecretString": json.dumps({"username": "AKIA_NEW", "password": "pass"})
    }
    mock_iam = MagicMock()
    mock_iam.list_access_keys.return_value = {
        "AccessKeyMetadata": [{"AccessKeyId": "AKIA_NEW", "Status": "Inactive"}]
    }
    mock_boto3.client.return_value = mock_iam

    with pytest.raises(ValueError, match="Inactive"):
        rot.test_secret(client, "secret-arn", "token")


@patch.dict(os.environ, _ENV)
@patch("rotate_smtp_credentials.boto3")
def test_test_secret_raises_when_key_not_found(mock_boto3):
    client = MagicMock()
    client.get_secret_value.return_value = {
        "SecretString": json.dumps({"username": "AKIA_NEW", "password": "pass"})
    }
    mock_iam = MagicMock()
    mock_iam.list_access_keys.return_value = {
        "AccessKeyMetadata": [{"AccessKeyId": "AKIA_DIFFERENT", "Status": "Active"}]
    }
    mock_boto3.client.return_value = mock_iam

    with pytest.raises(ValueError, match="not found"):
        rot.test_secret(client, "secret-arn", "token")


# ---------------------------------------------------------------------------
# finish_secret
# ---------------------------------------------------------------------------

@patch.dict(os.environ, _ENV)
@patch("rotate_smtp_credentials.boto3")
def test_finish_secret_promotes_pending_to_current(mock_boto3):
    client = _make_secret_client("new-token")
    rot.finish_secret(client, "secret-arn", "new-token")
    client.update_secret_version_stage.assert_called_once_with(
        SecretId="secret-arn",
        VersionStage="AWSCURRENT",
        MoveToVersionId="new-token",
        RemoveFromVersionId="prev-token",
    )


@patch.dict(os.environ, _ENV)
@patch("rotate_smtp_credentials.boto3")
def test_finish_secret_skips_promotion_if_already_current(mock_boto3):
    client = MagicMock()
    client.describe_secret.return_value = {
        "VersionIdsToStages": {"new-token": ["AWSCURRENT"]}
    }
    rot.finish_secret(client, "secret-arn", "new-token")
    client.update_secret_version_stage.assert_not_called()


@patch.dict(os.environ, _ENV)
@patch("rotate_smtp_credentials.boto3")
def test_finish_secret_swallows_ecs_failure(mock_boto3):
    client = _make_secret_client("new-token")
    mock_boto3.client.return_value.update_service.side_effect = Exception("ECS unavailable")
    rot.finish_secret(client, "secret-arn", "new-token")  # should not raise
