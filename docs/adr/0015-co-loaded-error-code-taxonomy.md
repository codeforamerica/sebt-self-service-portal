# 15. Co-Loaded Error-Code Taxonomy

Date: 2026-05-05

## Status

Accepted

## Context

Co-loaded household lookups (a portal user matched into a state's SNAP/TANF system rather than holding a SUN Bucks-issued case) can fail in several distinct ways, and analytics needs to distinguish them so product can measure where the funnel breaks. Today the dashboard's `household_result` event captures only a coarse `household_status` of `success | empty | error` — analysts can't tell whether an `error` was a 404 versus an auth denial versus a CMS outage, and they can't separate "got data, no qualifying children" from "couldn't reach data at all."

We also need a stable, narrow vocabulary the frontend can emit, the backend can log, and the content team can map to user-facing copy. Sharing the same string codes across the layers means an analyst, an SRE, and a writer can all join their respective tools (Mixpanel / Loki / the i18n bundle) on the same key.

## Decision

We adopt a **fixed taxonomy of co-loaded outcome codes** carried on the analytics `page.error_code` field and emitted at the matching observability and copy seams.

| Code | When it fires | UI shown today |
|---|---|---|
| `NOT_FOUND` | Backend returned 404 — no household record exists for this user. | Empty-state alert (apply CTA). |
| `NO_CHILDREN` | 200 with zero `summerEbtCases` and zero `applications`. Distinct from `NOT_FOUND`: a record exists, none of its members qualify. | Empty-state alert. |
| `AUTH_FAILURE` | 401 / 403. The auth middleware normally redirects before the dashboard renders, but the code is reserved so middleware bypasses (e.g. test seams) still tag correctly. | Login redirect. |
| `RATE_LIMIT` | 429 — the client hit a rate limit (e.g. OTP or API throttling). | Rate-limit / try-again messaging. |
| `TECH_ERROR` | Anything else — network failure, 5xx, schema-parse rejection, an unparseable response. The catch-all bucket. | Generic error alert with retry guidance. |
| `INVALID_INPUT` | Form submissions where the server rejects the request as client error (400 with `ValidationProblemDetails`, business-rule 400s such as card-replacement cooldown). | Inline field-level errors or flow-specific error screens. |

Codes are **uppercase snake_case strings**, used verbatim across:

- **Frontend analytics** — `setPageData('error_code', code)` immediately before the relevant event fires. Dashboard lookups use `DashboardContent.tsx`; self-service address update and card replacement use `apiErrorCodeFromUnknown()` in `analytics-helpers.ts`.
- **Backend logs** — structured Serilog field `OutcomeCode` (Pascal-cased to match Serilog conventions) attached to every co-loaded lookup.
- **i18n / user copy** — when a user-facing message varies by code, locale keys take a `.{code}` suffix (e.g. `dashboard.errorBody.NO_CHILDREN`). When the same generic copy is acceptable across codes, we reuse a single key — the PRD calls out shared error screens for the dashboard's first-error path.

Codes are **closed over the union above**. New codes require an ADR amendment and updates in all three layers.

### HTTP status mapping for form submissions

Self-service flows (address update, card replacement) map API failures through `apiErrorCodeFromUnknown()` in `src/SEBT.Portal.Web/src/lib/analytics-helpers.ts`:

| HTTP status | `error_code` |
|---|---|
| 401 / 403 | `AUTH_FAILURE` |
| 404 | `NOT_FOUND` |
| 429 | `RATE_LIMIT` |
| Other 4xx | `INVALID_INPUT` |
| 5xx, network, non-`ApiError` | `TECH_ERROR` |

The dashboard read path (`DashboardContent.tsx`) intentionally maps all non-404/401/403 4xx responses to `TECH_ERROR` because it does not submit user input — only the form paths above emit `INVALID_INPUT` for generic 4xx.

### Address validation outcome codes on submit

When PUT `/household/address` returns a structured validation outcome (HTTP 422 with `AddressUpdateResponse`), the `address_update_submit` event carries the backend `reason` uppercased in `page.error_code` (e.g. `ABBREVIATED`, `BLOCKED`). These are **validation outcome labels**, not additions to the closed taxonomy above: they pass through from the backend validation layer and do not emit a separate `address_update_error` event. API transport failures on the same endpoint still use the HTTP mapping table and emit both `address_update_submit` and `address_update_error`.

## Consequences

**Positive**

- A single string identifies the same failure mode across analytics, logs, and copy. Dashboards, runbooks, and content reviews stay aligned.
- Adding a new failure category is an explicit, reviewed change — the closed taxonomy resists drift into vendor-specific or transient codes.
- The `NOT_FOUND` vs `NO_CHILDREN` split lets analytics measure whether a state-connector outage is masquerading as low enrollment.

**Negative**

- The taxonomy is intentionally narrow — it doesn't capture sub-categories (e.g. CMS-side error sub-codes from the DC stored proc). Those live alongside the bucketed code as additional structured-log fields, not as new analytics events.
- Frontend and backend must update in lockstep when a code is added or renamed. The ADR amendment requirement is the gate.

**Neutral**

- Codes are defined where they're emitted, not in a shared schema package. This keeps the JS bundle and the .NET assemblies decoupled. The taxonomy in this ADR is the contract; the strings are duplicated by design (frontend in `DashboardContent.tsx` / `analytics-helpers.ts`, backend in the connector logging path).
