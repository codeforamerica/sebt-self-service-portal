# Weblate 2026.10.1, pinned by digest.
FROM weblate/weblate@sha256:c74bf3c6f5cb52e4aa141e6f605c0f4fdefdcf1e89d1a9fbd011a79a4a83807d

# The platform has no service-to-service networking yet, so Valkey (cache and Celery broker) runs in
# this container on loopback. Only valid with one container per service.
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends valkey-server=9.0.4-0ubuntu0.2 \
    && rm -rf /var/lib/apt/lists/*

# django-iam-dbauth signs an RDS IAM token for each new database connection.
COPY requirements.txt /app/cfa/requirements.txt
RUN uv pip install --python /app/venv/bin/python --no-deps --require-hashes \
    -r /app/cfa/requirements.txt
USER 1000

# Loopback REDIS_HOST is what tells start-with-valkey to start the bundled Valkey.
ENV REDIS_HOST=127.0.0.1 \
    REDIS_PORT=6379

COPY cfa_weblate /app/cfa/cfa_weblate
COPY --chmod=0755 start-with-valkey /app/cfa/start-with-valkey
ENTRYPOINT ["/app/cfa/start-with-valkey"]
CMD ["runserver"]

# Point every Weblate process (web, Celery, management commands) at settings that
# read the platform's DATABASE_* variables, and switch off Weblate's POSTGRES_*
# config. Supervisor's children inherit this environment from the start script.
ENV PYTHONPATH=/app/cfa \
    DJANGO_SETTINGS_MODULE=cfa_weblate.settings \
    WEBLATE_DATABASES=false

# Sized for a 2 GB task: one web worker and one in-process Celery worker. Weblate's own
# sizing reads nproc and the memory limit, and ran out of memory at 1 GB.
ENV WEB_WORKERS=1 \
    CELERY_WORKER_MODE=single \
    WEBLATE_WORKERS=2

# Non-secret settings. Secrets come from the platform at run time. The site domain is dev's
# platform address, so another environment has to override it.
ENV WEBLATE_SITE_DOMAIN=weblate.dev.services.cfa.codes \
    WEBLATE_DEBUG=0 \
    WEBLATE_LOGLEVEL=INFO \
    WEBLATE_SITE_TITLE="SEBT Weblate POC" \
    WEBLATE_ADMIN_NAME="POC Admin" \
    WEBLATE_ADMIN_EMAIL=admin@localhost \
    WEBLATE_SERVER_EMAIL=weblate@localhost \
    WEBLATE_DEFAULT_FROM_EMAIL=weblate@localhost \
    WEBLATE_REGISTRATION_OPEN=0 \
    WEBLATE_DEFAULT_COMMITER_NAME="SEBT Weblate POC" \
    WEBLATE_DEFAULT_COMMITER_EMAIL=noreply@localhost

# The load balancer ends HTTPS and forwards plain HTTP. Without trusting its X-Forwarded-Proto,
# Weblate redirects every request back to https:// in a loop.
ENV WEBLATE_ENABLE_HTTPS=1 \
    WEBLATE_SECURE_PROXY_SSL_HEADER=HTTP_X_FORWARDED_PROTO,https
