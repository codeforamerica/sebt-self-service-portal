# Local Keycloak OIDC (dev / POC only)

Keycloak stands in for PingOne / myColorado during local CO development so you can exercise the portal’s OIDC login, logout, and step-up flows without a third-party IdP.

Never deploy this to production. The compose service is gated behind a Docker Compose profile and uses insecure `start-dev` defaults.

## Start Keycloak

```bash
docker compose --profile keycloak up -d keycloak
```

Wait until healthy, then open:

| URL | Purpose |
| --- | --- |
| http://localhost:8180 | Keycloak (admin console: `/admin`) |
| http://localhost:8180/realms/sebt/.well-known/openid-configuration | OIDC discovery |

Admin login: `admin` / `admin` (local only).

Realm import comes from `docker/keycloak/sebt-realm.json` (`--import-realm` on first start).
Login UI uses the custom `sebt` theme under `docker/keycloak/themes/sebt` (potato logo above the title).

## Point the portal at Keycloak

1. Copy values from [`appsettings.keycloak.example.json`](../../apps/portal/src/SEBT.Portal.Api/appsettings.keycloak.example.json) into your local `appsettings.co.json` / `appsettings.Development.json`, or set the equivalent env vars (`Oidc__DiscoveryEndpoint`, and so on).
2. Keep `Oidc:CompleteLoginSigningKey` at least 32 characters.
3. For mock households to resolve by email, set seeding to match Keycloak fixture users:

```json
"Seeding": {
  "EmailPattern": "sebt.co+{0}@example.com",
  "State": "co"
}
```

4. Run CO as usual (`pnpm dev:co`). The API and browser must both reach Keycloak at `http://localhost:8180` (host-run API/Web is the expected path).

Alternatively, edit user emails in the Keycloak admin UI to match your existing `Seeding:EmailPattern`.

## Test users

Password for all users: `password`

| Username | Email | Phone attribute | Notes |
| --- | --- | --- | --- |
| `co-loaded` | `sebt.co+co-loaded@example.com` | `8185558437` | Matches default `DevelopmentPhoneOverride` and co-loaded mock household |
| `verified` | `sebt.co+verified@example.com` | `3035551009` | Verified-style persona |
| `ial1-only` | `sebt.co+non-co-loaded@example.com` | `3035551999` | No Socure verification attributes; use for IAL1 / step-up |

Login client (`sebt-portal`) emits `email`, `phone` / `phone_number`, and profile claims.  
Step-up client (`sebt-portal-stepup`) also emits `socureIdVerificationLevel` / `socureIdVerificationDate` from user attributes when present.

## Smoke checklist

1. Discovery document loads and lists `authorization_endpoint`, `token_endpoint`, `end_session_endpoint`, JWKS.
2. CO login redirects to Keycloak; after credentials, lands on `/callback` and establishes a portal session cookie.
3. Logout hits Keycloak `end_session_endpoint` and clears the IdP SSO session (next login prompts again).
4. Step-up path uses `sebt-portal-stepup` and raises IAL when verification claims are present (`ial1-only` can be used to exercise the challenge).

## Resetting the realm

Import runs when the realm is missing. To re-import after editing `sebt-realm.json`:

```bash
docker compose --profile keycloak stop keycloak
docker compose --profile keycloak rm -f keycloak
docker compose --profile keycloak up -d keycloak
```

(`start-dev` uses an ephemeral H2 store inside the container; removing the container drops realm state.)

## Related

- [ADR-0008](../adr/0008-oidc-mycolorado-authentication-and-state-auth-context.md): portal OIDC flow
- [ADR-0012](../adr/0012-oidc-rp-initiated-logout.md): RP-initiated logout
