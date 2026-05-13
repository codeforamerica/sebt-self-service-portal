# Cache-Busting API Fetch Requests — Design

Date: 2026-05-11
Status: Approved
Related: [ADR 0016 — Per-request cache-busting on client API GETs](../../adr/0016-cache-busting-api-fetch.md)

## Problem

In DC's production environment, Cloudflare's edge cache has been observed serving one user's authenticated API responses to another user on the same machine after logout/login. The portal sets `Cache-Control: no-cache` on responses, but Cloudflare's configuration ignores it for some endpoints.

The cross-user leak risk is acute for shared-machine scenarios (library kiosks, public terminals) where two distinct users may share a browser session, network egress IP, or both.

## Decision

Append a unique `?_=<crypto.randomUUID()>` parameter to every client-side **GET** request inside `apiFetch`. Each network round-trip gets a URL that no cache has ever seen, forcing an origin fetch.

The rationale, alternatives considered, and consequences are recorded in [ADR 0016](../../adr/0016-cache-busting-api-fetch.md).

## Scope

- **In scope:** Client-side GET requests issued through `apiFetch` in `src/SEBT.Portal.Web/src/api/client.ts`.
- **Out of scope:**
  - Non-GET methods (mutations are not edge-cached in any sane CDN config; bust param adds no value).
  - Server-side fetches in route handlers (e.g., `/api/[[...path]]/route.ts` proxy) — those are inside our trust boundary and never traverse Cloudflare.
  - Third-party fetches (Smarty autocomplete, etc.) — not behind our Cloudflare zone.
  - Fixing Cloudflare's configuration. That should be done in parallel by the platform team; this change is defense-in-depth so the portal is robust regardless of CDN configuration.

## Implementation

The change is localized to one function in `src/SEBT.Portal.Web/src/api/client.ts`. Before constructing the network URL, if `method === 'GET'`, append a random UUID under the query key `_`.

```ts
let resolvedEndpoint = endpoint
if (method === 'GET') {
  const url = new URL(endpoint, 'http://placeholder.invalid')
  url.searchParams.set('_', crypto.randomUUID())
  resolvedEndpoint = `${url.pathname}${url.search}`
}

response = await fetch(`${API_ROUTE_PREFIX}${resolvedEndpoint}`, { ... })
```

Notes:
- `URL` requires a base for relative inputs. `http://placeholder.invalid` is an RFC 6761 reserved-unresolvable host used purely as a syntactic anchor; only `pathname` and `search` are read back.
- `crypto.randomUUID()` requires a secure context. Production, staging, and `localhost` dev all qualify. If apiFetch is ever called from an insecure HTTP page, this will throw — surfacing the misconfiguration immediately.
- `URLSearchParams.set` handles encoding and existing-query cases (`/foo?bar=baz` → `/foo?bar=baz&_=<uuid>`) without manual string juggling.

## Testing

TDD: write tests first, watch them fail against the current implementation, then implement.

Add to `src/SEBT.Portal.Web/src/api/client.test.ts`:

1. **GET adds `_` param** — MSW handler asserts `request.url` contains `_=` matching a UUID v4 pattern.
2. **GET preserves existing query params** — endpoint `/foo?bar=baz` results in a request URL containing both `bar=baz` and `_=<uuid>`.
3. **GET param is unique per call** — two sequential calls to the same endpoint produce two distinct `_` values.
4. **POST does not add `_` param** — MSW handler asserts the request URL has no `_=` param.
5. **PUT/PATCH/DELETE do not add `_` param** — one combined parametrized test is fine.

Existing tests must continue to pass (URL changes in MSW handlers only happen when the matchers are path-only, which they already are — MSW ignores query strings unless explicitly matched).

## Risks and Mitigations

- **Backend log noise:** every URL has a unique cache-bust suffix. ASP.NET request logs will see distinct paths. Acceptable — request paths are already logged with their full query string.
- **OpenAPI/Swagger drift:** the `_` param is not declared in any controller. ASP.NET ignores unbound query params, so this is functionally invisible to model binding. No spec changes required.
- **MSW test handlers matching on query string:** A scan of current handlers shows path-only matchers, so no test breakage is expected. Verified by running the existing suite after implementation.

## Rollout

Single PR. No feature flag — the change is too small and the bug is in production. Merge to `main`, deploy through normal ECR pipeline.
