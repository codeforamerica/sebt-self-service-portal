# 8. CO OIDC (MyColorado) Authentication and State Auth Context

Date: 2026-02-27

## Status

Accepted

## Context

Colorado uses MyColorado (PingOne) for CO resident authentication. The portal must support sign-in via MyColorado and use identity data from that provider—including phone and other claims for downstream use. This requires an OIDC flow completed on the backend, a session-scoped "state auth context" (tokens and claims) that plugins can read, and plugin contracts plus CO connector implementations. The work spans the portal, state-connector, and co-connector repos.

## Decision

We use a frontend-driven Authorization Code/PKCE flow: the frontend sends the authorization code and `code_verifier` to the backend; the backend exchanges them with the IdP (token endpoint, client secret), receives the `id_token`, validates it via the state plugin’s **ValidateIdTokenAsync**, stores **StateAuthContext** (IdToken, optional AccessToken, IdTokenClaims) in a session-keyed store, sets a cookie, and returns a portal JWT.

- **State connector** — New interfaces: **IStateOidcLoginService**, **IStateAuthStore**, **IStateAuthSessionAccessor**, **IStateAuthService**, and **StateAuthContext**.

- **Portal** — `GET /api/auth/oidc/{code}/config` (public config, no secrets) and `POST /api/auth/oidc/{code}/exchange-code` (takes code/code_verifier, returns a JWT . **MemoryStateAuthStore** and **CookieStateAuthSessionAccessor** (`StateAuth.SessionId`) implement the store and session lookup; both are registered before plugin composition so the CO plugin can import them. MEF exports **IStateOidcLoginService** and **IStateAuthService**; OIDC login plugins are keyed singletons by state code.

In the frontend: CO login page fetches config (because we require a client_id/secret to be used), builds PKCE, redirects to MyColorado; callback page POSTs code/code_verifier to `exchange-code` and completes login.

- **CO connector** — **ColoradoOidcLoginService** (ValidateIdTokenAsync only; delegates to **MyColoradoOidcService** for validation and claims). **ColoradoStateAuthService**. Consuming state auth context via **IStateAuthService** (for example, using phone number from the claims for fetching hosuehold data) is future work. 

**MyColoradoOidc.TestHost** is a standalone dev app (localhost:8080, `TestHost:Enabled=true`) for testing the real MyColorado flow; this is defaulted to off in the settings, but can be used to test the endpoints in isolations.

## Consequences

CO users can sign in with MyColorado; the portal stores IdP claims in **StateAuthContext** per session and plugins can read them via **IStateAuthService**. OIDC config is per-state and the client secret stays on the backend. Plugin contracts live in the state-connector repo

Development requires real or test PingOne credentials and the correct redirect URI (e.g. `http://localhost:3000/callback`). **MemoryStateAuthStore** is in-memory only; production or multi-instance deployments need a distributed store or sticky sessions.

## References

- ADR-0007: Multi-state plugin approach. Portal: `OidcController`, `ServiceCollectionPluginExtensions`, `MemoryStateAuthStore`, `CookieStateAuthSessionAccessor`; frontend `COLoginPage`, callback page, `oidc-pkce`. State-connector: `IStateOidcLoginService`, `IStateAuthService`, `IStateAuthStore`, `IStateAuthSessionAccessor`, `StateAuthContext`, `IdentityAssuranceLevel`. CO-connector: `ColoradoOidcLoginService`, `ColoradoStateAuthService`, `MyColoradoOidcService`, MyColoradoOidc.TestHost.

## Related ADRs

- **ADR-0007**: Multi-state plugin approach.  OIDC and state auth extend the plugin contract and MEF composition.
