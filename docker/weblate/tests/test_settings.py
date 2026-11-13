"""Load the real settings module in a subprocess, with only what the platform injects."""

import json
import os
import subprocess
import sys
import unittest

IMAGE_ROOT = os.path.join(os.path.dirname(__file__), "..")

# Exactly the shape tofu/configs/hosting/local.tf injects for a shared-postgres service.
PLATFORM_ENV = {
    "DATABASE_HOST": "shared-services-development.cluster-abc123.us-east-1.rds.amazonaws.com",
    "DATABASE_PORT": "5432",
    "DATABASE_NAME": "weblate",
    "DATABASE_USERNAME": "weblate_web",
    "DATABASE_TYPE": "postgres",
    "DATABASE_SSLMODE": "require",
    "DATABASE_AUTH": "iam",
}

# The region comes from the task's environment, not the platform's DATABASE_* set.
TASK_ENV = {"AWS_REGION": "us-east-1"}

IAM_OPTIONS = {
    "sslmode": "require",
    "use_iam_auth": True,
    "region_name": "us-east-1",
    "resolve_cname_enabled": False,
}

PROBE = (
    "import json, cfa_weblate.settings as s; "
    "print(json.dumps({'db': s.DATABASES, 'backup': s.DATABASE_BACKUP}))"
)


def load(extra_env):
    env = {
        "PATH": os.environ["PATH"],
        "PYTHONPATH": IMAGE_ROOT,
        "WEBLATE_SITE_DOMAIN": "weblate.example.test",
        "WEBLATE_DATABASES": "false",
        **extra_env,
    }
    return subprocess.run(
        [sys.executable, "-c", PROBE], env=env, capture_output=True, text=True
    )


class SettingsTest(unittest.TestCase):
    def test_builds_databases_from_platform_env_alone(self):
        result = load({**PLATFORM_ENV, **TASK_ENV})
        self.assertEqual(result.returncode, 0, result.stderr)
        db = json.loads(result.stdout)["db"]["default"]
        self.assertEqual(db["ENGINE"], "cfa_weblate.db")
        self.assertEqual(db["NAME"], "weblate")
        self.assertEqual(db["USER"], "weblate_web")
        self.assertEqual(db["ALTER_ROLE"], "weblate_web")
        self.assertEqual(db["HOST"], PLATFORM_ENV["DATABASE_HOST"])
        self.assertEqual(db["PORT"], "5432")
        self.assertEqual(db["OPTIONS"], IAM_OPTIONS)
        self.assertEqual(db["PASSWORD"], "")

    def test_falls_back_to_aws_default_region(self):
        result = load({**PLATFORM_ENV, "AWS_DEFAULT_REGION": "us-west-2"})
        self.assertEqual(result.returncode, 0, result.stderr)
        options = json.loads(result.stdout)["db"]["default"]["OPTIONS"]
        self.assertEqual(options["region_name"], "us-west-2")

    def test_refuses_to_load_without_a_region(self):
        result = load(PLATFORM_ENV)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("AWS_REGION", result.stderr)

    def test_passes_a_ca_bundle_through_when_one_is_vendored(self):
        result = load({**PLATFORM_ENV, **TASK_ENV, "DATABASE_SSLMODE": "verify-full",
                       "DATABASE_SSLROOTCERT": "/app/cfa/rds-ca-bundle.pem"})
        self.assertEqual(result.returncode, 0, result.stderr)
        options = json.loads(result.stdout)["db"]["default"]["OPTIONS"]
        self.assertEqual(options, {**IAM_OPTIONS, "sslmode": "verify-full",
                                   "sslrootcert": "/app/cfa/rds-ca-bundle.pem"})

    def test_disables_weblates_database_dump(self):
        # pg_dump is handed settings PASSWORD, which IAM auth leaves empty.
        result = load({**PLATFORM_ENV, **TASK_ENV, "WEBLATE_DATABASE_BACKUP": "plain"})
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(json.loads(result.stdout)["backup"], "none")

    def test_refuses_anything_but_iam_auth(self):
        result = load({**PLATFORM_ENV, **TASK_ENV, "DATABASE_AUTH": "password"})
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("DATABASE_AUTH", result.stderr)

    def test_refuses_when_weblates_own_database_config_is_on(self):
        result = load({**PLATFORM_ENV, **TASK_ENV, "WEBLATE_DATABASES": "true", "POSTGRES_USER": "x",
                       "POSTGRES_PASSWORD": "x", "POSTGRES_HOST": "x", "POSTGRES_DB": "x"})
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("WEBLATE_DATABASES", result.stderr)


if __name__ == "__main__":
    unittest.main()
