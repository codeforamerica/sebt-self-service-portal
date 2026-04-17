# 8. OIDC State IdP Authentication and IdP Claims in Portal JWT

Date: 2026-02-27

## Status

Accepted

## Context

States may use an external OpenID Connect provider for authentication. The portal must support sign-in via that IdP and use identity data. This requires an OIDC flow and a way for the API and plugins to access IdP-derived claims (for example, phone, givenName, familyName). 

## Decision

We'll use a frontend-driven Authorization Code/PKCE flow: the **Next.js** server exchanges the authorization code with the IdP, validates the `id_token` using JWKS, issues a short-lived callback JWT, and returns it to the client. The client then POSTs the callback token to the .NET API's `complete-login` endpoint, which validates the token, copies **non-principal** IdP claims (phone, givenName, familyName, userId, etc.) into the portal JWT, and stores that JWT in an HttpOnly session cookie (see DC-242 addendum below). This also sets the path for having a stateless way of having PII be available in the app without having it persisted in any way (which is a requirement for some states).

- **Portal (API)** — `GET /api/auth/oidc/{code}/config` (for fetching config info: authorization endpoint, token endpoint, client id, redirect URI) and `POST /api/auth/oidc/complete-login` (accepts callback token, sets the portal JWT as an HttpOnly cookie and returns metadata only). `Complete-login` treats standard OIDC/OAuth claims as common and does not add them to the portal JWT (although this is subject to change later; expiry is an example); all other claims from the callback token are added so that plugins and handlers can read them via `User.Claims`.

- **Portal (Next.js)** — `POST /api/auth/oidc/callback`: accepts code and code_verifier and `stateCode`, exchanges code with IdP, validates id_token via JWKS, issues short-lived callback JWT signed with `OIDC_COMPLETE_LOGIN_SIGNING_KEY`, returns callback token to client. Client secret and signing key are located in the Next.js environment.

- **Frontend (Web)** — State login page fetches config from the API at `GET /api/auth/oidc/{stateCode}/config`, builds PKCE, redirects to IdP; IdP redirects to `/callback`. Callback page POSTs code and code_verifier (and current state as `stateCode`) to the Next.js OIDC callback API, receives the callback token, then POSTs it with `stateCode` to the .NET `complete-login` endpoint. The browser stores the resulting session cookie automatically; the SPA then re-reads `/api/auth/status` to populate non-sensitive session claims (email, IAL, ID-proofing state) for UI gating.

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

---

## Addendum (DC-242, 2026-04-08): Portal session JWT moved to HttpOnly cookie

### Context
A penetration test flagged that the portal session JWT was being stored in `sessionStorage.auth_token`, which makes it readable by any XSS payload running on the portal origin.

### Decision
The portal session JWT is now transported via an HttpOnly, Secure, `SameSite=Lax` cookie (`sebt_portal_session`, see `SEBT.Portal.Api/Services/AuthCookies.cs`). The token is no longer returned in any response body and is no longer accessible to JavaScript.

- **`POST /api/auth/oidc/complete-login`** writes the JWT to the session cookie. The response body now carries only non-sensitive metadata (`returnUrl` for step-up navigation, otherwise an empty body). See `CompleteLoginResponse`.
- **`POST /api/auth/otp/validate`** and **`POST /api/auth/refresh`** write the JWT to the session cookie and return `204 No Content`.
- **`POST /api/auth/logout`** clears the session cookie (matching attributes + past expiry) and returns `204 No Content`.
- **`GET /api/auth/status`** is the SPA's only path to session state — it reads validated JWT claims off the `User` principal and returns the non-sensitive subset (email, IAL, ID-proofing fields) the UI needs for routing and analytics. The JWT itself never leaves the cookie.
- **`JwtBearer` middleware** (configured in `Program.cs`) falls back to the `sebt_portal_session` cookie via `OnMessageReceived` when no `Authorization` header is present. The header path is preserved for service-to-service callers.

The JWT's claim shape, signing key, and expiry behavior are unchanged from the original ADR — only the storage and transport changed.

### Consequences
- An XSS payload on the portal origin can no longer read the session JWT.
- The SPA cannot decode the JWT directly; non-sensitive claims must be exposed via `/auth/status`. Adding new UI gates that depend on JWT claims means adding fields to `AuthorizationStatusResponse`.
- All authenticated `fetch` calls from the SPA must include the cookie. The `apiFetch` wrapper sets `credentials: 'same-origin'`; the Next.js `/api/*` proxy forwards `Cookie` and `Set-Cookie` headers between the browser and the .NET API.
- E2E tests seed authentication by writing the cookie directly into the Playwright browser context (`e2e/fixtures/auth.ts`) and mocking `/api/auth/status` (`e2e/fixtures/api-routes.ts`).

### References
- `SEBT.Portal.Api/Services/AuthCookies.cs`
- `SEBT.Portal.Api/Controllers/Auth/{AuthController,OidcController,OtpController}.cs`
- `SEBT.Portal.Api/Program.cs` (JwtBearerEvents.OnMessageReceived)
- `SEBT.Portal.Api/Models/{AuthorizationStatusResponse,CompleteLoginResponse}.cs`
- `SEBT.Portal.Web/src/api/client.ts`, `src/features/auth/context/AuthContext.tsx`, `src/lib/jwt.ts`
- `SEBT.Portal.Tests/Unit/Services/AuthCookiesTests.cs`, `SEBT.Portal.Tests/Integration/AuthCookieAuthenticationTests.cs`


---

## Addendum (DC-243, 2026-04-10): Server-side OIDC pre-auth session

### Context
A penetration test found that the OIDC login flow was fully stateless from the portal's perspective: `code_verifier` was generated in the browser and sent in cleartext, authorization codes and ID tokens could be replayed from any machine to mint unlimited portal sessions, the `state` parameter was not validated server-side, and the `stateCode` route parameter accepted arbitrary values. The assessor demonstrated full account takeover by intercepting an authorization code and replaying it from a separate machine without any session binding.

### Decision
The OIDC flow now uses a server-side pre-auth session that binds PKCE, CSRF state, and token exchange to the browser that initiated the login.

**Pre-auth session lifecycle:**
1. `GET /api/auth/oidc/{stateCode}/config` generates PKCE (`code_verifier` + `code_challenge`) and a random `state` parameter on the server. These are stored in a `PreAuthSession` record in `HybridCache` (L1 memory + optional L2 Redis) with a 15-minute TTL. The session ID is set as an `oidc_session` HttpOnly, Secure, `SameSite=Strict` cookie. The response returns `state`, `code_challenge`, and the pinned `authorizationEndpoint` — never the `code_verifier`.
2. `POST /api/auth/oidc/callback` requires the `oidc_session` cookie, validates `state` and `stateCode` against the stored session, uses the server-stored `code_verifier` for the token exchange (the browser never sends it), verifies the id_token with strict 10-second clock skew, and advances the session to `CallbackCompleted`.
3. `POST /api/auth/oidc/complete-login` requires the same `oidc_session` cookie, verifies the callback token hash matches the session, enforces one-time use (session advances to `LoginCompleted`), mints the portal JWT, clears the pre-auth cookie, and removes the session from cache.

**Token exchange moved from Next.js to .NET:**
The OIDC code→token exchange and id_token verification previously happened in a Next.js API route (`/api/auth/oidc/callback/route.ts`). This route is deleted. The exchange now happens in `OidcExchangeService` on the .NET API, which owns the client secret, discovery document fetching, JWKS verification, and callback token signing. The Next.js proxy forwards all `/api/*` requests to .NET without interception.

**Endpoint hardening:**
- `stateCode` is validated against a server-side allowlist derived from loaded per-state OIDC configuration.
- The PingOne authorization endpoint is pinned in appsettings (`Oidc:AuthorizationEndpoint`), not fetched from the IdP discovery document at request time.
- `OidcOriginValidationMiddleware` requires a matching `Origin` header on all OIDC POST endpoints; mismatched `Referer` is logged but not enforced.
- ID token `exp` is enforced with ≤10-second clock skew (previously ~60 seconds).
- The callback token JWT now includes `iss`/`aud` claims matching the portal origin to prevent cross-environment confusion.
- `Oidc:CompleteLoginSigningKey` is validated at startup to be ≥32 characters.

**Config migration:**
OIDC secrets (`ClientSecret`, `CompleteLoginSigningKey`, step-up equivalents) moved from Next.js `.env.local` to the .NET API's per-state appsettings (`Oidc:ClientSecret`, `Oidc:StepUp:ClientSecret`). New required keys: `Oidc:AuthorizationEndpoint` and `Oidc:StepUp:AuthorizationEndpoint`. The Next.js env schema no longer includes OIDC variables.

### Consequences
- Authorization code and ID token replay from another machine is no longer possible — the attacker would need the `oidc_session` cookie, which is HttpOnly and `SameSite=Strict`.
- `code_verifier` never appears in browser-visible network traffic, sessionStorage, or request bodies.
- Each login flow consumes its session exactly once; a second attempt with the same cookie returns 403.
- The Next.js process no longer holds OIDC client secrets, reducing its attack surface.
- Deployment configs must add `Oidc:ClientSecret` and `Oidc:AuthorizationEndpoint` to the API's per-state appsettings and can remove the corresponding `OIDC_*` env vars from the Next.js container.
- The `oidc_session` cookie is short-lived (15 min) and cleared after successful login; it is not present during normal authenticated usage.

### References
- `SEBT.Portal.Api/Services/{PreAuthSession,PreAuthSessionStore,OidcSessionCookie,OidcExchangeService,PkceHelper,StateAllowlist}.cs`
- `SEBT.Portal.Api/Middleware/OidcOriginValidationMiddleware.cs`
- `SEBT.Portal.Api/Controllers/Auth/OidcController.cs`
- `SEBT.Portal.Tests/Integration/OidcPreAuthSecurityTests.cs`
- `appsettings.co.example.json` (Oidc block with new keys)
