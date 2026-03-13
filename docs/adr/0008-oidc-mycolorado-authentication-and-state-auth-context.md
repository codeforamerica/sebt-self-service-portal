# 8. OIDC State IdP Authentication and IdP Claims in Portal JWT

Date: 2026-02-27

## Status

Accepted

## Context

States may use an external OpenID Connect provider for authentication. The portal must support sign-in via that IdP and use identity data. This requires an OIDC flow and a way for the API and plugins to access IdP-derived claims (for example, phone, givenName, familyName). 

## Decision

We'll use a frontend-driven Authorization Code/PKCE flow: the **Next.js** server exchanges the authorization code with the IdP, validates the `id_token` using JWKS, issues a short-lived callback JWT, and returns it to the client. The client then POSTs the callback token to the .NET API's `complete-login` endpoint, which validates the token, copies **non-principal** IdP claims (phone, givenName, familyName, userId, etc.) into the portal JWT, and returns that JWT.  This also sets the path for having a stateless way of having PII be available in the app without having it persisted in any way (which is a requirement for some states).

- **Portal (API)** — `GET /api/auth/oidc/{code}/config` (for fetching config info: authorization endpoint, token endpoint, client id, redirect URI) and `POST /api/auth/oidc/complete-login` (accepts callback token, returns portal JWT). `Complete-login` treats standard OIDC/OAuth claims as common and does not add them to the portal JWT (although this is subject to change later; expiry is an example); all other claims from the callback token are added so that plugins and handlers can read them via `User.Claims`.

- **Portal (Next.js)** — `POST /api/auth/oidc/callback`: accepts code and code_verifier and `stateCode`, exchanges code with IdP, validates id_token via JWKS, issues short-lived callback JWT signed with `OIDC_COMPLETE_LOGIN_SIGNING_KEY`, returns callback token to client. Client secret and signing key are located in the Next.js environment.

- **Frontend (Web)** — State login page fetches config from the API at `GET /api/auth/oidc/{stateCode}/config`, builds PKCE, redirects to IdP; IdP redirects to `/callback`. Callback page POSTs code and code_verifier (and current state as `stateCode`) to the Next.js OIDC callback API, receives the callback token, then POSTs it with `stateCode` to the .NET `complete-login` endpoint and completes login.

- **Plugins** — Code that needs IdP-derived data (e.g. phone for household lookup) reads from the request's `ClaimsPrincipal`; the portal JWT issued at complete-login includes those claims.

**Configuration:**  
Next.js (when the current deployment state uses OIDC): `OIDC_DISCOVERY_ENDPOINT`, `OIDC_CLIENT_ID`, `OIDC_CLIENT_SECRET`, `OIDC_REDIRECT_URI`, `OIDC_COMPLETE_LOGIN_SIGNING_KEY`. API: `Oidc:CompleteLoginSigningKey` (it should match the same value as the Next.js one); for the public config endpoint, `Oidc:DiscoveryEndpoint`, `Oidc:ClientId`, `Oidc:CallbackRedirectUri`, and optionally `Oidc:LanguageParam`. See `appsettings.Development.example.json` and `.env.example`.

## Consequences

Users can sign in with OIDC services (like Colorado's MyColorado). IdP claims such as phone, givenName, and familyName are available in the portal JWT and thus on `User.Claims` for the duration of the session. OIDC config is flat (one IdP per deployment); the client secret lives only in the Next.js server.

Development requires real or test IdP credentials and the correct redirect URI (`http://localhost:3000/callback`).

**Why auth is in the main portal:** The portal is the single application for all states. It must expose login endpoints, issue the portal JWT, and discover state plugins.

## References

- ADR-0007: Multi-state plugin approach.
- Portal (API): `OidcController`, `IJwtTokenService.GenerateToken(user, additionalClaims)`.
- Portal (Next.js): OIDC callback API route; state login page (OIDC flow), callback page, `oidc-pkce`.

## Related ADRs

- **ADR-0007**: Multi-state plugin approach. OIDC extends the auth flow; IdP claims are carried in the portal JWT.
