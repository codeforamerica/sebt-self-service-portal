# ADR 0016: Per-request Cache-Busting on Client API GET Requests

## Status

Accepted

## Context

The portal's API responses for authenticated, user-specific endpoints (auth status, household data, verification status) carry `Cache-Control: no-cache`. In DC's production environment, Cloudflare sits in front of the Next.js web app as both WAF and edge cache. Cloudflare has been observed serving cached responses from one user (User A) to another user (User B) on the same browser after a logout/login sequence — a cross-user data leak.

The leak is acute in shared-machine scenarios common to our population: library terminals, kiosks, public Wi-Fi networks where multiple devices share egress IPs that may participate in Cloudflare's cache key. `Cache-Control: no-cache` is being ignored for at least some endpoints, despite our intent that authenticated responses never be edge-cached.

We need a defense that does not depend on Cloudflare honoring response headers, because:

1. Cloudflare's configuration sits outside this repository, owned by a partner team; coordinating fixes is slower than a code change.
2. Even after the CDN is reconfigured, a future regression (a rule change, a new zone, a different state's CDN) would silently reintroduce the leak. The portal should be robust to CDN misconfiguration.

The portal API is a BFF consumed only by the Next.js web app; we control both ends and can change request semantics atomically.

## Decision

Append a per-request `?_=<crypto.randomUUID()>` query parameter to every **GET** request issued through `apiFetch` (`src/SEBT.Portal.Web/src/api/client.ts`). Each network round-trip carries a URL no cache has ever seen, forcing the edge to either pass through to origin or miss and revalidate.

Scope:

- **GET only.** Mutations are not edge-cached in correctly configured CDNs; the bust adds no defense.
- **Inside `apiFetch`**, the single chokepoint for all client API calls. No per-call retrofit.
- **`crypto.randomUUID()`** as the entropy source. 122 bits from the browser's CSPRNG; collision probability is negligible across both time (sequential calls) and space (kiosks on the same network egress).

React Query's cache is keyed by `queryKey`, not by URL. Mutating the URL inside `apiFetch` does not invalidate React Query's in-memory cache. RQ continues to suppress redundant calls when data is fresh; only when RQ *does* fire a fetch does the bust take effect.

## Alternatives Considered

**Fix Cloudflare only.** Reconfigure the CDN to honor `Cache-Control: no-cache`. Owned by a different team; required regardless, but insufficient alone — leaves no in-app defense against future regression.

**Per-session token, rotated on login.** Generate one UUID at login, attach to all requests for that session. Simpler theoretically, but introduces failure modes: if rotation logic ever breaks (race on logout, edge case in OIDC flow), the entire session shares a cache key and the leak reappears. Per-request entropy has no shared state to break.

**Rely on `Cache-Control` headers only.** This is the textbook correct answer that demonstrably failed in production. Insufficient.

**Device fingerprinting (e.g., FingerprintJS) + timestamp.** Heavier dependency, privacy implications, and adds no cache-busting value over a random UUID. UUIDs are already independent across machines because each browser draws from its own OS entropy pool — public IP and network proximity are irrelevant.

**Server-side cache-bust via a response header (e.g., `Vary: <random>`).** Would require coordination with backend; doesn't help if the CDN ignores `Vary` (the same root cause as ignoring `Cache-Control`).

## Consequences

- **Edge cache hits for authenticated GET endpoints are impossible by construction.** This is the desired outcome: user-specific responses should never be edge-cached.
- **No increase in backend request volume vs. status quo.** React Query's in-memory cache continues to deduplicate within a tab; the bust only takes effect on fetches RQ has already decided to make.
- **Request paths in backend logs gain a `_=<uuid>` suffix.** Existing logging already captures full query strings; the noise is contained and identifiable.
- **The `_` query param is undeclared in any controller.** ASP.NET ignores unbound query parameters, so OpenAPI specs and model binding are unaffected.
- **Hard requirement on a secure context** (HTTPS or `localhost`). `crypto.randomUUID()` throws on insecure HTTP — surfacing any misconfiguration loudly rather than silently degrading.
- **Cloudflare configuration should still be corrected** in parallel. This change is defense-in-depth, not a substitute for properly configured edge caching.
