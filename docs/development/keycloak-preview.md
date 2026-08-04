# Deployed Keycloak (shared OIDC stand-in)

Keycloak can run as a shared OIDC IdP in AWS (Fargate + Postgres) for non-production use: development stacks, demos, and ephemeral preview hosts. Local Docker Compose Keycloak remains unchanged for laptop development (`docs/development/keycloak-oidc.md`, ADR-0019).

The long-lived portal path that uses the production IdP is separate (`module.app`); do not point production traffic at this Keycloak.

## Architecture

- Hostname: `https://auth.<DOMAIN>`
- Public ALB + Fargate (`tofu/modules/sebt_keycloak`) + dedicated Postgres
- Realm/theme baked into `docker/keycloak` (deployed realm allows `https://*.<DOMAIN>/*` redirects)
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

## Ops notes

- Do **not** use this IdP for production.
- Admin credentials are in Secrets Manager (`…-keycloak-admin`). Prefer SSM exec / secret read over exposing `/admin` broadly.
- After realm or theme changes: rebuild/push the image, then force a new ECS deployment for the Keycloak service.
- Realm redirect URIs must list the environment’s `<DOMAIN>` (and `*.<DOMAIN>`) wildcards; update `sebt-realm.preview.json` when the domain changes.
