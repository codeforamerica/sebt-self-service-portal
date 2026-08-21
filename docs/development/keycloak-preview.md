# Deployed Keycloak (shared OIDC stand-in)

Keycloak can run as a shared OIDC IdP in AWS (Fargate + Postgres) for non-production use: development stacks, demos, and ephemeral preview hosts. Local Docker Compose Keycloak remains unchanged for laptop development (`docs/development/keycloak-oidc.md`, ADR-0019).

The long-lived portal path that uses the production IdP is separate (`module.app`); do not point production traffic at this Keycloak.

## Architecture

- Hostname: `https://auth.<DOMAIN>`
- Public ALB + Fargate (`tofu/modules/sebt_keycloak`) + dedicated Postgres
- Realm/theme baked into `docker/keycloak`
- Consumers override API OIDC env toward Keycloak and omit production IdP client secrets when those would conflict

## First-time setup

1. Apply bootstrap so the Keycloak ECR repo exists (environment bootstrap tofu).
2. Build and push the image:

```bash
ECR_KEYCLOAK_REPOSITORY_URL=<ecr-repo-url> ./scripts/preview/build-keycloak.sh
```

3. Apply environment tofu (`enable_preview_keycloak=true`, default) so ECS/ALB/DNS/Postgres are created.
4. Confirm discovery:

```text
https://auth.<DOMAIN>/realms/sebt/.well-known/openid-configuration
```

### Preview stacks

Preview deploy scripts point OIDC at this Keycloak automatically. OTP bypass remains enabled on previews as a fallback.

Keycloak 26 only allows path-trailing wildcards in Valid Redirect URIs (`https://host.example/*`), not hostname wildcards (`https://*.example/*`). Because each preview uses a distinct `pr-N.<DOMAIN>` host, `deploy-co.sh` registers that host on the shared `sebt-portal` and `sebt-portal-stepup` clients via the Keycloak Admin API, and `destroy-co.sh` removes it. Helpers live in `scripts/preview/keycloak.sh`.

Admin credentials come from Secrets Manager. Prefer setting `PREVIEW_KEYCLOAK_ADMIN_SECRET_ID` to the tofu output `preview_keycloak_admin_secret_arn` (the preview workflow reads `vars.PREVIEW_KEYCLOAK_ADMIN_SECRET_ID`). If unset, the scripts fall back to the secret name `sebt-portal-co-development-keycloak-admin`.

## Fixture users

From `docker/keycloak/sebt-realm.preview.json` (password `password`):

| Username | Notes |
| --- | --- |
| `verified` | Has Socure-shaped IAL claims for step-up |
| `co-loaded` | Co-loaded persona |
| `ial1-only` | IAL1-only (no Socure claims) |

## Client credentials

Realm clients (also the preview deploy script defaults):

- Login: `sebt-portal` / `sebt-portal-dev-secret`
- Step-up: `sebt-portal-stepup` / `sebt-portal-stepup-dev-secret`

Override with `PREVIEW_OIDC_*` (or equivalent) env vars if you rotate secrets in the realm.

Baked realm redirect URIs cover the long-lived host and localhost only. Ephemeral preview hosts are added at deploy time (see above).

## Ops notes

- Do **not** use this IdP for production.
- Admin credentials are in Secrets Manager (`…-keycloak-admin`). Prefer SSM exec / secret read over exposing `/admin` broadly.
- After realm or theme changes: rebuild/push the image, then force a new ECS deployment for the Keycloak service. Note that `--import-realm` does not overwrite an existing realm in the Postgres database; use the Admin API (or a fresh DB) when you need live client changes beyond what preview deploy already manages.
- When the environment domain changes, update the long-lived entries in `sebt-realm.preview.json` and rebuild the image.
