# 19. Use Keycloak as the local OIDC stand-in for local/demo development

Date: 2026-07-27

## Status

Accepted

## Context

Colorado production auth uses myColorado/PingOne (see [ADR-0008](./0008-oidc-mycolorado-authentication-and-state-auth-context.md) and [ADR-0012](./0012-oidc-rp-initiated-logout.md)).  For production use, it works fine with the application, but we don't have a local solution to run ourseleves and are depended on instances that we don't own.  Some example of problems that creates are:

- MFA and phone ownership block local sign-in and force workarounds such as using `DevelopmentPhoneOverride` or Google Groups juggling
- Shared IdP accounts can lock or change under other people.  The current process is that we reach out to existing third-party instances (in the case with MyCO) and directly request resetting of data and state.
- Claim shapes, IAL levels, and step-up success/failure are hard to force deterministically
- CO browser and E2E flows cannot reliably depend on a third-party IdP even with black box testing.

We need a self-hosted OIDC provider for local (and potentially lower-environment) use that can:

- Speak standard OIDC discovery, authorization code with PKCE being acceptable, JWKS, and RP-initiated logout
- Model separate login and step-up clients (`Oidc` vs `Oidc:StepUp`)
- Emit myColorado-shaped claims (`phone` / `phone_number`, `socureIdVerificationLevel`, `socureIdVerificationDate`, and related profile claims)
- Run under Docker Compose with a clear non-production gate
- Use licensing compatible with our allowlist (MIT, Apache-2.0, BSD-*, MS-PL)

## Decision

Use **Keycloak** (`quay.io/keycloak/keycloak`) as the local OIDC stand-in for Colorado (and future OIDC using states) portal development.

We'll be immediately including the changes tied to branch `spike/DC-513-oidc-provider`, which includes:

- Docker Compose service behind a `keycloak` profile, using insecure `start-dev` defaults.  This isn't planned for any production use, so this should be OK.
- Versioned realm import from `docker/keycloak/sebt-realm.json` (clients, fixture users, protocol mappers) that'll we can update as we add additional seeded users.
- Optional local theme under `docker/keycloak/themes/sebt` so the login UI is obviously not myColorado
- Portal overlay documented in `appsettings.keycloak.example.json` and [docs/development/keycloak-oidc.md](../development/keycloak-oidc.md)
- Two clients will be supported:
  - `sebt-portal` for normal login
  - `sebt-portal-stepup` for IAL elevation, emitting Socure-shaped verification claims when present on the user

### Alternatives considered

**Authentik**

Strong full IdP with a modern admin UI. Rejected for this use case because:

- Open-core licensing (MIT core plus a separate Authentik EE license for `enterprise/`) adds compliance review we do not need for a local stand-in; Keycloak is Apache-2.0 throughout.
- Ops shape is a fuller identity platform (server, worker, Postgres, historically Redis). Keycloak is much more lightweight in comparision
- Our portal already thinks in OIDC clients and claim mappers; Keycloak realms/clients/protocol-mappers map more directly onto login vs step-up clients

Authentik remains a reasonable revisit if we later need more robust features (reverse-proxies, outposts etc.) more than realm-import simplicity.

**Lightweight OIDC mocks** (for example `oidc-server-mock`)

Useful for narrow unit/stub tests. Too weak for full browser login, RP logout, dual clients, and claim experimentation.

**Zitadel**

Capable product, but AGPL is a poor fit relative to our license allowlist.

**Local/dev pointed only at real PingOne**

Highest fidelity to myColorado, but keeps the MFA, account, and non-determinism problems that motivated a local IdP in the first place

## Consequences

### Positive

- Developers can exercise real portal OIDC paths (authorize, callback, complete-login, IAL translation, IalGuard step-up, RP logout) without myColorado MFA
- Fixture users and claim mappers are versioned in git and resettable by recreating the container
- CO E2E can aim for deterministic login/step-up scenarios without Google Group phone micromanagement
- License story is simple (Apache-2.0) and aligned with `.github/workflows/scripts/licenses/allowed-licenses.json`

### Negative / trade-offs

- Claim parity with myColorado is approximate and must be maintained in realm mappers (POC gaps include missing `auth_time` when `max_age` is sent, and verification claims scoped to the step-up client by design)
- Another Compose dependency and a small amount of Keycloak-specific docs/theme surface area
- This isn't a production ready product for our needs for this application and shouldn't be deployed in production.

### Follow-ups

- Develop a realm-helper generator to align with how we seed users
- Decide whether lower non-prod environments should optionally run the same stand-in for CO E2E
- If accepted beyond the spike, link this ADR from README/onboarding docs alongside `docs/development/keycloak-oidc.md`

## References

- DC-513 (OIDC provider spike)
- [docs/development/keycloak-oidc.md](../development/keycloak-oidc.md)
- `compose.yaml` (`keycloak` profile)
- `docker/keycloak/sebt-realm.json`
- `apps/portal/src/SEBT.Portal.Api/appsettings.keycloak.example.json`
- [ADR-0008](./0008-oidc-mycolorado-authentication-and-state-auth-context.md)
- [ADR-0012](./0012-oidc-rp-initiated-logout.md)
