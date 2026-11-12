"""Weblate settings for the shared-services hosting platform.

Weblate's stock Docker settings build DATABASES from POSTGRES_* and a static
password. The platform injects DATABASE_* instead and only allows IAM auth, so
Weblate's own database config is switched off (WEBLATE_DATABASES=false) and
DATABASES is built here from the platform's variables.
"""

import os

from django.core.exceptions import ImproperlyConfigured
from weblate.utils.environment import get_env_bool, get_env_int_or_none

if get_env_bool("WEBLATE_DATABASES", True):
    raise ImproperlyConfigured(
        "Set WEBLATE_DATABASES=false; DATABASES comes from the platform's DATABASE_* variables."
    )

from weblate.settings_docker import *  # noqa: E402,F403
from weblate.settings_docker import DEFAULT_LOG, DEFAULT_LOGLEVEL, LOGGING  # noqa: E402


def _require(name):
    value = os.environ.get(name, "")
    if not value:
        raise ImproperlyConfigured(f"{name} is required; the hosting platform injects it.")
    return value


if _require("DATABASE_AUTH") != "iam":
    raise ImproperlyConfigured(
        f"DATABASE_AUTH={os.environ['DATABASE_AUTH']!r} isn't supported; this image "
        "only authenticates with IAM tokens."
    )

def _region():
    for name in ("AWS_REGION", "AWS_DEFAULT_REGION"):
        if os.environ.get(name):
            return os.environ[name]
    raise ImproperlyConfigured("AWS_REGION is required to sign IAM database tokens.")


_options = {
    "sslmode": os.environ.get("DATABASE_SSLMODE", "require"),
    # Read by django-iam-dbauth, which mints a token for each new connection.
    "use_iam_auth": True,
    "region_name": _region(),
    # Sign for DATABASE_HOST as given; following DNS aliases fails for hosts
    # outside rds.amazonaws.com.
    "resolve_cname_enabled": False,
}
if os.environ.get("DATABASE_SSLROOTCERT"):
    _options["sslrootcert"] = os.environ["DATABASE_SSLROOTCERT"]

DATABASES = {
    "default": {
        "ENGINE": "cfa_weblate.db",
        "NAME": _require("DATABASE_NAME"),
        "USER": _require("DATABASE_USERNAME"),
        # Weblate's first migration runs ALTER ROLE <this> SET timezone.
        "ALTER_ROLE": _require("DATABASE_USERNAME"),
        # The backend supplies a fresh token per connection.
        "PASSWORD": "",
        "HOST": _require("DATABASE_HOST"),
        "PORT": os.environ.get("DATABASE_PORT", "5432"),
        "OPTIONS": _options,
        "CONN_MAX_AGE": get_env_int_or_none("POSTGRES_CONN_MAX_AGE"),
        "CONN_HEALTH_CHECKS": True,
        "DISABLE_SERVER_SIDE_CURSORS": False,
    }
}

# Weblate's database dump hands settings PASSWORD to pg_dump, which IAM auth
# leaves empty. Aurora's own backups cover the database instead.
DATABASE_BACKUP = "none"

LOGGING["loggers"]["cfa_weblate"] = {
    "handlers": [*DEFAULT_LOG],
    "level": DEFAULT_LOGLEVEL,
}
