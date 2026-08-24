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

5. If this Keycloak instance already had a `sebt` realm before `sebt-preview-deploy` was added to the realm JSON, seed the deploy client once (import does not overwrite existing realms). Pick **one** of:

**A. Bootstrap script** (preferred when bootstrap admin works):

```bash
./scripts/preview/bootstrap-keycloak-deploy-client.sh
```

Uses the bootstrap admin secret in Secrets Manager (`…-keycloak-admin`). If admin login returns 401, admin credentials have drifted from the live Keycloak DB — use B or C instead.

**B. Manual Admin Console** (when bootstrap admin is broken but you can still reach `/admin` with a working user):

1. Open `https://auth.<DOMAIN>/admin` and sign in.
2. Realm **sebt** → **Clients** → **Create client**:
   - Client ID: `sebt-preview-deploy`
   - Client authentication: **On**
   - Authentication flow: disable Standard flow; enable **Service accounts roles** only
3. Credentials tab: set the client secret to `sebt-preview-deploy-secret` (or your override).
4. Service accounts roles → `realm-management`: assign `manage-clients`, `view-clients`, `query-clients`.
5. Confirm:

```bash
# should print an access_token JSON field (HTTP 200)
curl -sS -X POST "https://auth.<DOMAIN>/realms/sebt/protocol/openid-connect/token" \
  -d grant_type=client_credentials \
  -d client_id=sebt-preview-deploy \
  -d client_secret=sebt-preview-deploy-secret
```

**C. Recreate the Keycloak database** (last resort): destroy/recreate the Keycloak RDS (or otherwise empty the DB), rebuild/push the image with the updated realm JSON, and force a new ECS deployment so `--import-realm` imports a fresh `sebt` realm including `sebt-preview-deploy`.

Until one of A–C succeeds, Preview (CO) deploys will fail when registering redirect URIs and login will keep returning `Invalid parameter: redirect_uri`.

### Preview stacks

Preview deploy scripts point OIDC at this Keycloak automatically. OTP bypass remains enabled on previews as a fallback.

Keycloak 26 only allows path-trailing wildcards in Valid Redirect URIs (`https://host.example/*`), not hostname wildcards (`https://*.example/*`). Because each preview uses a distinct `pr-N.<DOMAIN>` host, `deploy-co.sh` registers that host on the shared `sebt-portal` and `sebt-portal-stepup` clients via the Keycloak Admin API after Route53 aliases are created (so the preview URL still resolves if Keycloak is temporarily unreachable), and `destroy-co.sh` removes it. Helpers live in `scripts/preview/keycloak.sh`. Registration remains required for a successful deploy; without it, OIDC login will fail.

Preview scripts authenticate with the dedicated `sebt-preview-deploy` client (`client_credentials`), not the bootstrap admin user. Defaults match the baked realm secret; override with `PREVIEW_KEYCLOAK_DEPLOY_CLIENT_ID` / `PREVIEW_KEYCLOAK_DEPLOY_CLIENT_SECRET`, or `PREVIEW_KEYCLOAK_DEPLOY_SECRET_ID` pointing at Secrets Manager JSON `{ "clientId", "clientSecret" }`. If those overrides are wrong or the live client was never seeded, token errors name the client and point back to the bootstrap / manual steps above.

## Fixture users

From `docker/keycloak/sebt-realm.preview.json` (password `password`):

| Username | Notes |
| --- | --- |
| `verified` | Emits Socure + myColorado-shaped IAL claims (`1.5`) on login and step-up |
| `co-loaded` | Co-loaded persona; same IAL `1.5` claims on login and step-up |
| `ial1-only` | IAL1-only (no verification-level claims) |

## Client credentials

Realm clients (also the preview deploy script defaults):

- Login: `sebt-portal` / `sebt-portal-dev-secret`
- Step-up: `sebt-portal-stepup` / `sebt-portal-stepup-dev-secret`
- Preview Admin API: `sebt-preview-deploy` / `sebt-preview-deploy-secret` (service account with `manage-clients`)

Override with `PREVIEW_OIDC_*` / `PREVIEW_KEYCLOAK_DEPLOY_*` env vars if you rotate secrets in the realm.

Baked realm redirect URIs cover the long-lived host and localhost only. Ephemeral preview hosts are added at deploy time (see above).

## Ops notes

- Do **not** use this IdP for production.
- Bootstrap admin credentials remain in Secrets Manager (`…-keycloak-admin`) for break-glass / one-time bootstrap only. Prefer SSM exec / secret read over exposing `/admin` broadly.
- After realm or theme changes: rebuild/push the image, then force a new ECS deployment for the Keycloak service. Note that `--import-realm` does not overwrite an existing realm in the Postgres database; use `bootstrap-keycloak-deploy-client.sh` or the Admin API when you need live client changes beyond what preview deploy already manages.
- When the environment domain changes, update the long-lived entries in `sebt-realm.preview.json` and rebuild the image.
