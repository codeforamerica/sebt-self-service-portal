# 8. CO OIDC (MyColorado) Authentication and State Auth Context

Date: 2026-02-27

## Status

Accepted

## Context

Colorado uses MyColorado (PingOne) for CO resident authentication. The portal must support sign-in via MyColorado and use identity data from that provider—including phone and other claims for downstream use. This requires an OIDC flow completed on the backend, a session-scoped "state auth context" (tokens and claims) that plugins can read, and plugin contracts plus CO connector implementations. The work spans the portal, state-connector, and co-connector repos.

## Decision

We use a frontend-driven Authorization Code/PKCE flow: the **Next.js** server exchanges the authorization code with the IdP, validates the `id_token` using JWKS, issues a short-lived callback JWT, and returns it to the client. The client then POSTs the callback token to the .NET API's `complete-login` endpoint, which validates the token, builds **StateAuthContext** from claims, stores it in a session-keyed store, sets a cookie, and returns the portal JWT.

- **State connector** — Interfaces: **IStateAuthStore**, **IStateAuthSessionAccessor**, **IStateAuthService**, and **StateAuthContext**.

- **Portal (API)** - `GET /api/auth/oidc/{code}/config` (public config: authorization endpoint, token endpoint, client id, redirect URI and `POST /api/auth/oidc/complete-login` (accepts callback token, returns portal JWT). **MemoryStateAuthStore** and **CookieStateAuthSessionAccessor** (`StateAuth.SessionId`) implement the store and session lookup; both are registered before plugin composition so the CO plugin can import them. MEF exports **IStateAuthService**.

- **Portal (Next.js)** — `POST /api/auth/oidc/callback`: accepts code and code_verifier and `stateCode` (must match current deployment state), exchanges code with IdP, validates id_token via JWKS, issues short-lived callback JWT signed with `OIDC_COMPLETE_LOGIN_SIGNING_KEY`, returns callback token to client. Client secret and signing key live only in Next.js env (`OIDC_DISCOVERY_ENDPOINT`, `OIDC_CLIENT_ID`, `OIDC_CLIENT_SECRET`, `OIDC_REDIRECT_URI`).

- **Frontend** — State login page (e.g. OIDC flow) fetches config from the API at `GET /api/auth/oidc/{stateCode}/config`, builds PKCE, redirects to IdP; IdP redirects to `/callback`. Callback page POSTs code and code_verifier (and current state as `stateCode`) to the Next.js OIDC callback API, receives the callback token, then POSTs it with `stateCode` to the .NET `complete-login` endpoint and completes login.

- **CO connector** — **ColoradoStateAuthService** (reads StateAuthContext from the store for the current session). Consuming state auth context via **IStateAuthService** (for example, using phone number from the claims for fetching household data) is future work.

**Configuration:**  
Next.js (state-agnostic; used when the current deployment state uses OIDC): `OIDC_DISCOVERY_ENDPOINT`, `OIDC_CLIENT_ID`, `OIDC_CLIENT_SECRET`, `OIDC_REDIRECT_URI`, `OIDC_COMPLETE_LOGIN_SIGNING_KEY` (min 32 chars). API: `Oidc:CompleteLoginSigningKey` (same value as Next.js); for the public config endpoint, `Oidc:{state}:DiscoveryEndpoint`, `Oidc:{state}:ClientId`, `Oidc:{state}:CallbackRedirectUri`, and optionally `Oidc:{state}:LanguageParam`. See `appsettings.Development.example.json` and `.env.example`.

## Consequences

Users can sign in with OIDC services (Such as MyColorado); the portal stores IdP claims in **StateAuthContext** per session and plugins can read them via **IStateAuthService**. OIDC config is per-state; the client secret lives only in the Next.js server (for code exchange and id_token validation). 

Development requires real or test PingOne credentials and the correct redirect URI (`http://localhost:3000/callback`). **MemoryStateAuthStore** is in-memory only; production or multi-instance deployments need a distributed store or sticky sessions.

## References

- ADR-0007: Multi-state plugin approach.
- Portal (API): `OidcController`, `ServiceCollectionPluginExtensions`, `MemoryStateAuthStore`, `CookieStateAuthSessionAccessor`.
- Portal (Next.js): OIDC callback API route; state login page (OIDC flow), callback page, `oidc-pkce`.

## Related ADRs

- **ADR-0007**: Multi-state plugin approach.  OIDC and state auth extend the plugin contract and MEF composition.
