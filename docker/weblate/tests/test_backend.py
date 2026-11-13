import os
import unittest
from unittest import mock

import django
from django.conf import settings

# The OPTIONS cfa_weblate.settings builds for django-iam-dbauth.
IAM_OPTIONS = {
    "sslmode": "require",
    "use_iam_auth": True,
    "region_name": "us-east-1",
    "resolve_cname_enabled": False,
}

if not settings.configured:
    settings.configure(
        USE_TZ=True,
        DATABASES={
            "default": {
                "ENGINE": "cfa_weblate.db",
                "NAME": "weblate",
                "USER": "weblate_web",
                "HOST": "db.example.test",
                "PORT": "5432",
                "OPTIONS": IAM_OPTIONS,
            },
            "pooled": {
                "ENGINE": "cfa_weblate.db",
                "NAME": "weblate",
                "USER": "weblate_web",
                "HOST": "db.example.test",
                "PORT": "5432",
                "OPTIONS": {**IAM_OPTIONS, "pool": True},
            },
        },
    )
    django.setup()

from botocore.exceptions import NoCredentialsError  # noqa: E402
from django.core.exceptions import ImproperlyConfigured  # noqa: E402
from django.db import connections  # noqa: E402
from django_iam_dbauth.aws.postgresql import base as library_base  # noqa: E402

from cfa_weblate.db import base  # noqa: E402

FAKE_CREDS = {
    "AWS_ACCESS_KEY_ID": "AKIAIOSFODNN7EXAMPLE",
    "AWS_SECRET_ACCESS_KEY": "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
}
SESSION = "django_iam_dbauth.aws.database_wrapper.boto3.session.Session"


def session_minting(*tokens):
    session = mock.Mock()
    session.return_value.client.return_value.generate_db_auth_token.side_effect = list(tokens)
    return session


class LibraryBackendTest(unittest.TestCase):
    def test_builds_on_django_iam_dbauth(self):
        self.assertTrue(issubclass(base.DatabaseWrapper, library_base.DatabaseWrapper))
        self.assertIsInstance(connections["default"], base.DatabaseWrapper)

    def test_every_new_connection_gets_a_freshly_minted_token(self):
        session = session_minting("token-1", "token-2")
        with mock.patch(SESSION, session):
            first = connections["default"].get_connection_params()
            second = connections["default"].get_connection_params()
        self.assertEqual(first["password"], "token-1")
        self.assertEqual(second["password"], "token-2")
        session.return_value.client.assert_called_with(service_name="rds", region_name="us-east-1")
        signed = session.return_value.client.return_value.generate_db_auth_token.call_args.kwargs
        # Django hands over PORT as the settings string; the signed endpoint is the same.
        self.assertEqual(
            (signed["DBHostname"], str(signed["Port"]), signed["DBUsername"]),
            ("db.example.test", "5432", "weblate_web"),
        )

    def test_signs_for_the_host_as_given_without_dns_lookups(self):
        with mock.patch(SESSION, session_minting("token")), mock.patch(
            "django_iam_dbauth.aws.database_wrapper.resolve_cname"
        ) as resolve:
            connections["default"].get_connection_params()
        resolve.assert_not_called()

    def test_hands_the_driver_only_connection_params(self):
        with mock.patch(SESSION, session_minting("token")):
            params = connections["default"].get_connection_params()
        self.assertEqual(params["dbname"], "weblate")
        self.assertEqual(params["user"], "weblate_web")
        self.assertEqual(params["host"], "db.example.test")
        self.assertEqual(params["sslmode"], "require")
        for key in ("use_iam_auth", "region_name", "resolve_cname_enabled"):
            self.assertNotIn(key, params)

    def test_refuses_connection_pooling(self):
        # A pool is built from one set of params, so its later connections would
        # reuse a token that has since expired.
        with self.assertRaisesRegex(ImproperlyConfigured, "pool"):
            connections["pooled"].get_connection_params()


class LoggingTest(unittest.TestCase):
    @mock.patch.dict(os.environ, FAKE_CREDS, clear=True)
    def test_logs_each_mint_and_never_the_token(self):
        with self.assertLogs("cfa_weblate.iam", level="INFO") as logs:
            token = connections["default"].get_connection_params()["password"]
        output = "\n".join(logs.output)
        self.assertIn("iam_token_minted", output)
        self.assertIn("host=db.example.test port=5432 user=weblate_web region=us-east-1", output)
        self.assertIn("process=", output)
        self.assertNotIn("token_sha256", output)
        self.assertNotIn(token, output)
        self.assertNotIn(token.rsplit("X-Amz-Signature=", 1)[1], output)

    def test_logs_a_failed_mint_and_reraises(self):
        session = mock.Mock()
        session.return_value.client.return_value.generate_db_auth_token.side_effect = (
            NoCredentialsError()
        )
        with mock.patch(SESSION, session), self.assertLogs(
            "cfa_weblate.iam", level="ERROR"
        ) as logs, self.assertRaises(NoCredentialsError):
            connections["default"].get_connection_params()
        self.assertIn("iam_token_mint_failed", logs.output[0])
        self.assertIn("error=NoCredentialsError", logs.output[0])


if __name__ == "__main__":
    unittest.main()
