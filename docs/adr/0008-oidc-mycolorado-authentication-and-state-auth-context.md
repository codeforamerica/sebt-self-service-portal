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
