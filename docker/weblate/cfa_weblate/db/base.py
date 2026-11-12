"""django-iam-dbauth's PostgreSQL backend, logging each IAM token it mints."""

import hashlib
import logging
import os
import sys

from django.core.exceptions import ImproperlyConfigured
from django_iam_dbauth.aws.postgresql import base

logger = logging.getLogger("cfa_weblate.iam")


def _process_name():
    return " ".join(os.path.basename(arg) for arg in sys.argv[:2]) or "unknown"


class DatabaseWrapper(base.DatabaseWrapper):
    def get_connection_params(self):
        options = self.settings_dict["OPTIONS"]
        # Django builds a pool from one call to this method, so pooled
        # connections opened later would present a token that has expired.
        if options.get("pool"):
            raise ImproperlyConfigured(
                "cfa_weblate.db doesn't support OPTIONS['pool']; IAM tokens "
                "must be minted per connection."
            )
        where = "host=%s port=%s user=%s region=%s" % (
            self.settings_dict["HOST"],
            self.settings_dict["PORT"],
            self.settings_dict["USER"],
            options.get("region_name"),
        )
        try:
            params = super().get_connection_params()
        except Exception as error:
            logger.error(
                "iam_token_mint_failed %s pid=%s process=%r error=%s",
                where, os.getpid(), _process_name(), type(error).__name__,
            )
            raise
        logger.info(
            "iam_token_minted %s pid=%s process=%r token_sha256=%s",
            where, os.getpid(), _process_name(),
            hashlib.sha256(params["password"].encode("utf-8")).hexdigest()[:12],
        )
        return params
