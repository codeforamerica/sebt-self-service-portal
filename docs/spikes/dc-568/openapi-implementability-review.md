# OpenAPI Spec — Government Implementer Review

Reviewed: `docs/openapi.yaml` (SEBT State Backend API v1.0.0). Audience assumed: small, budget-constrained state/vendor team, weak on OpenAPI/RFC 9457/JWT, possibly fronting a mainframe/CBMS.

## Summary

Most likely to cause a failed or wrong implementation:

1. **JWT (`X-Sebt-User-Identity`) validation is under-specified and will be either skipped or done wrong.** The spec tells them *what* claims exist but not *how* to verify. No worked example (no sample token, no JWKS URL, no "reject if `alg` is `none`" warning). A defensive-but-inexperienced team will either accept unsigned tokens, skip `exp` checks, or choke on `ial: 1.5` as a float. The `supported: false` opt-out exists but isn't framed as the recommended starting point.
2. **Atomic address update will ship as partial-success.** "All-or-nothing" against a legacy CMS requires a transaction the mainframe may not offer. Teams will loop-update and return `200` after a partial write. The spec describes the contract but gives no guidance for backends that *can't* do atomic writes.
3. **Idempotency-Key dedup store will be skipped or wrong.** Implementing a 24h keyed dedup cache with "replay returns original body" is real work. Many will treat the header as decorative and process duplicates, causing double card orders.
4. **OAuth2 client-credentials is presented as the default; it's the harder path.** A small shop is more likely to stand up an API key in an afternoon than run a token-issuing authorization server. The spec buries the API-key alternative and labels OAuth "default," steering the weakest teams toward the hardest option.
5. **The capabilities-first "only build what you declare" model is correct but not stated forcefully enough at the top.** Risk is inverted from the others: a nervous team reads a big spec and thinks they must build all of it.

## Pain points

### 1. JWT verification has no worked example and no failure-mode guidance — **blocker**

Why they struggle: This audience has "limited familiarity with JWT intricacies" by assumption. The spec gives claims (`ial`, `userRef`, `iat`, `exp`) and names `HS256`/`RS256`, but never shows: a decoded sample token, how to fetch/pin the RS256 public key, that they MUST reject `alg: none` and MUST reject an `alg` that doesn't match the agreed one (the classic JWT algorithm-confusion attack — HS256-signed-with-the-public-key), how much clock skew to allow against a 60s TTL, or what to do when the header is absent vs. present-but-invalid. "Agreed during integration setup" hand-waves the single hardest integration step.

Concrete failure modes for this audience:
- Accept any token without verifying the signature (decode-only). This is the single most common JWT mistake and the spec does nothing to prevent it.
- Treat `alg` as advisory and allow `none`.
- Parse `ial` as an integer → `1.5` throws or truncates to `1` → silently under-enforces IAL.
- Ignore `exp`, or reject everything because a 60s TTL + no skew allowance + clock drift = most tokens look expired.

Mitigations (low cost, high value):
- Add a **non-normative "Validating `X-Sebt-User-Identity`" appendix** with: a base64 sample token, the exact verification checklist (verify signature first; pin `alg` to the agreed value; reject `none`; check `exp` with ±N s skew; parse `ial` as a decimal/number, values `1`, `1.5`, `2`), and a 2-line pseudocode snippet per algorithm.
- State the skew allowance in the spec (e.g. "allow up to 30s clock skew"). Don't leave it to guess.
- **Reframe the opt-out as the default.** Add a sentence in the auth section: "If you don't need per-user scoping or IAL enforcement, set `userAssertion.supported: false` and ignore this header entirely. This is a fully supported configuration — start here." Right now `supported: false` is documented but not *recommended*, so cautious teams will attempt validation they don't need.
- Pick a default algorithm for new integrations (recommend `HS256` — no PKI, one shared secret) and say so. RS256 key exchange/rotation is where small teams stall.
- `userRef` is described as HMAC of the portal user ID but the backend is never told what to *do* with it. If it's only useful for per-user scoping, say "ignore unless you scope data per user."

### 2. Atomic address update assumes transactional writes the CMS may not support — **high**

Why they struggle: "Either all cases are updated or none" is a transaction. A legacy batch system may only offer per-record updates with no rollback. The spec says roll back on failure but never acknowledges backends that *can't*, so they'll do the easy literal thing: update in a loop, return `200`, maybe stuff failures into a field nobody checks. That silently breaks the portal's all-or-nothing assumption.

Also ambiguous: `failedCases` appears only in prose and in the `additionalProperties: true` ProblemDetails bag — it's **not a defined schema**. A literal implementer won't know the shape (`caseId` + `detail`? array? object?) beyond copying the example. And the 400-vs-409 split (validation vs. business-rule conflict) is a subtlety they'll collapse into one status.

Mitigations:
- **Define `failedCases` as a real schema** (`FailedCase { caseId: string, detail: string }`, array) and reference it from the 400/409 responses instead of leaving it as a loose extension. Show it in the schema section, not just examples.
- Add explicit guidance: "If your CMS cannot perform a true atomic multi-record update, either (a) validate all cases *before* writing any, then write, or (b) declare `addressUpdate.supported: false` and let the portal fall back to per-case flows." Give them an out.
- One sentence on the decision rule: "Return `400` for input/validation problems (bad address, empty `caseIds`); `409` for state conflicts (case on hold/locked)."

### 3. Idempotency-Key dedup — replay-vs-conflict semantics will be skipped — **high**

Why they struggle: A correct implementation needs a persistent store keyed by `Idempotency-Key` (scoped to caseId), a 24h TTL, storage of the original response body, and the replay-returns-`200` / different-request-returns-`409` branch. That's a meaningful chunk of infra for a small team. The likely shortcut: ignore the header and process every request → duplicate card orders, which is exactly the harm idempotency prevents.

The spec is unusually clear *conceptually* here (the replay-vs-409 paragraph is good), but gives no implementation shape.

Mitigations:
- Add a short "Implementing idempotency" note: what to store (key → {request fingerprint, status, response body}), TTL 24h, the branch logic in 4 bullets. This is cheaper than a support ticket after prod double-issues cards.
- Clarify the key scope explicitly: is the key globally unique, or unique per `caseId`? The prose says "same key, same caseId" — state whether a collision across different caseIds is possible/handled.
- Define what "different request" means for conflict detection. The body is `{}`, so the only distinguishing input is caseId + key. Say so, or the "409 for a different request" branch is undefined in practice.

### 4. OAuth2-as-default steers weak teams to the hard path — **high**

Why they struggle: Client-credentials still means running (or buying) an authorization server that issues and validates tokens, plus scope handling. For a shop that "cannot run an OAuth 2.0 authorization server" (the spec's own words), the API key is a header check — an afternoon. Labeling OAuth "default" and putting the API key second as a fallback nudges the least-capable teams toward the most work.

Mitigations:
- Rebalance the language: present both as first-class and say plainly "**API key is the lower-effort option** — a single static header credential. Use OAuth 2.0 client credentials if your organization already runs an authorization server." Don't call either the default.
- Note key handling expectations for the API-key path (transport over TLS only, rotation story) so they don't hardcode it insecurely.
- `tokenUrl` in the OAuth scheme is `auth.example.gov` — fine, but say explicitly the token endpoint is *theirs to provide*, not something the portal hosts. Easy to misread.

### 5. Capabilities-first "build only what you declare" is right but under-sold up front — **medium**

Why they struggle: The design is genuinely good and lowers their burden — but a cautious reader skims a 1500-line spec with seven endpoints and assumes it's all mandatory. The key liberating rule ("don't register routes for capabilities you don't implement; a 404 is the correct signal") is in the intro but competes with everything else.

Mitigations:
- Add a short **"Minimum viable backend"** callout near the top: the smallest conformant implementation is `GET /health` + `GET /capabilities` (declaring almost everything `false`) + `POST /households/lookup`. Everything else is opt-in. One paragraph flips their mental model from "build 7 endpoints" to "build 3, add more later."
- Spell out that `cardActivation` has *no endpoint in this spec* — it's declarable but there's nothing to implement yet. A literal reader will hunt for the missing route. (`cardActivation` appears in `CasesCapabilities` with no corresponding path.)

### 6. `intent` fail-closed vs. signal fail-open is a subtle split they'll get backwards — **medium**

Why they struggle: The spec has two *opposite* rules living close together: ignore unknown *signal types* silently (fail open), but reject unknown *intent* values with `400` (fail closed). A team implementing literally and defensively may apply one rule to both — most likely rejecting unknown signals too (breaking forward-compat) or accepting unknown intents (breaking access control). The reasoning ("intent shapes access control") is stated but easy to miss.

Mitigations:
- Keep the conventions table (it's good) but add a one-line rationale inline at each rule: "signals: fail open (forward-compat)"; "intent: fail closed (security)."
- Consider making `intent` a closed `enum` in the schema with a documented note that new values require a spec bump. A closed enum is self-documenting and makes the `400` behavior fall out of standard validation — far more likely to be implemented correctly than an "open string constant" they must hand-guard. (Trade-off: loses the forward-compat wiggle room the current design wants. Flag for decision — this is the one place the "open constant" design fights the audience.)

### 7. Enum mapping (`CardStatus`, `ApplicationStatus`, `IssuanceType`) — under-specified mapping burden — **medium**

Why they struggle: The spec says "backends map their CMS-internal tokens to these canonical values; unrecognized → `Unknown`." For a mainframe with dozens of cryptic status codes, this mapping *is* the integration, and the spec gives zero mapping guidance. `CardStatus` has 11 values with overlapping meanings (`Inactive`, `NotActivated`, `Frozen`, `DeactivatedByState`, `Processed`) and no definitions. A team will guess, and the portal's action-gating logic depends on these being right.

Mitigations:
- **Define each `CardStatus` value in one line** — especially the ambiguous ones. What's the portal-visible difference between `Inactive`, `Frozen`, and `DeactivatedByState`? Which statuses gate which self-service actions? Without this, mapping is a coin flip.
- State the safe default explicitly: "When in doubt, map to `Unknown` — the portal degrades gracefully." (Currently implied, worth making loud.)

### 8. Money as integer cents — will produce a decimal somewhere — **low**

Why they struggle: The rule is stated clearly (`12550` = $125.50) and exampled. Low risk, but legacy systems often store dollars-and-cents as decimal strings or `DECIMAL(9,2)`; a naive `balance * 100` on a float will occasionally emit `12549`. Only `CardDetails.balance` is affected.

Mitigation: One line — "convert with rounding, not truncation; e.g. `round(dollars * 100)`." Cheap insurance.

### 9. Health check `503`-with-a-body and vocabulary — **low**

Why they struggle: Two learnable-but-unusual things: `pass`/`warn`/`fail` vocab (not everyone knows the health-check draft) and returning a JSON *body* on a `503`. Some stacks/load balancers strip bodies on 5xx or return HTML error pages. Also `cmsReachable` is "required when status is warn or fail" in prose but not in the schema `required` list — a literal reader trusts the schema and omits it.

Mitigations:
- Enforce in schema what the prose requires, or drop the prose requirement. Right now they conflict. (Prefer: keep prose, add a note; making it schema-conditional is beyond most of this audience.)
- One line: "Return the JSON body even on `503`; ensure your gateway doesn't replace 5xx bodies with an HTML error page."

### 10. RFC 9457 ProblemDetails — the `type` URN scheme is invented and undocumented — **medium**

Why they struggle: The examples use `urn:sebt:error:unknown-intent`, `urn:sebt:error:address-update-failed`, etc., but there's **no registry** of valid `type` values and no statement of whether the portal keys off `type` or just `status`. A defensive implementer either invents their own URNs (fine if the portal ignores `type`) or omits `type` entirely and returns bare `{status, detail}`. Worse case for this audience: they skip ProblemDetails and return their framework's default HTML/plain-text error, breaking the portal's error parsing.

Mitigations:
- State plainly whether `type` is machine-significant. If the portal only branches on HTTP status + specific extensions (`requiredIal`, `failedCases`), say "`type` is informational; any stable URI is acceptable." That removes the fear of getting the URN wrong.
- Add one forceful line: "All error responses — including unexpected `500`s — must be `application/json` in this shape, never HTML. Configure your framework's default error handler accordingly." This is the single most common real-world violation and the spec should call it out.
- List the extension fields that *are* significant (`requiredIal`, `failedCases`) in one place so they aren't discovered only via examples.

## What the spec already does well

- **Capabilities-first design genuinely reduces scope** — declaring `false`/omitting is a real, documented opt-out. The 404-for-unregistered-route convention is a clean signal.
- **API-key alternative exists** for teams that can't run OAuth — the right escape hatch, just needs promoting.
- **`userAssertion.supported: false`** is a first-class way to skip the hardest part (JWT) entirely.
- **Enrollment check explicitly carries no user assertion** — removes a whole class of confusion for the public-facing path.
- **The idempotency replay-vs-conflict paragraph** is conceptually crisp.
- **Conventions table** (dates, phone, money, null-vs-absent) is exactly the kind of upfront rule table this audience needs.
- **Good, realistic examples** on most endpoints — including no-match, no-card, and readOnly-mode cases.
- **`Unknown` fallback on every enum** gives a safe default and prevents mapping from hard-failing.

## Recommended changes

Docs/examples only (cheap, do first):
1. JWT validation appendix: sample token, verification checklist, skew value, `alg`-pinning / reject-`none` warning, `ial` as decimal. **(pain 1)**
2. "Minimum viable backend" callout + note that `cardActivation` has no endpoint. **(pain 5)**
3. Reframe API key as the lower-effort auth option; stop calling OAuth "default." **(pain 4)**
4. Reframe `userAssertion.supported: false` as the recommended starting point. **(pain 1)**
5. Per-value `CardStatus` definitions + which statuses gate which actions. **(pain 7)**
6. "Errors are always JSON in ProblemDetails shape, never HTML; `type` is informational" statement + list significant extensions. **(pain 10)**
7. Idempotency implementation note (store shape, TTL, branch logic) + key-scope clarification. **(pain 3)**
8. Address-update fallback guidance for non-transactional backends + 400/409 decision rule. **(pain 2)**
9. Health: return body on 503; gateway warning. Money: round don't truncate. **(pains 8, 9)**

Spec changes (small, worth it):
10. Define `failedCases` / `FailedCase` as a real schema; reference from 400/409. **(pain 2)**
11. Reconcile `cmsReachable` required-in-prose vs. optional-in-schema. **(pain 9)**
12. Add inline fail-open/fail-closed rationale to the signals/intent rules. **(pain 6)**

Decision needed (design tension):
13. Make `intent` a closed `enum` vs. keep it an "open string constant." Closed enum is far more implementable-correctly for this audience but sacrifices forward-compat. **(pain 6)**

## Open questions

- Does the portal branch on ProblemDetails `type`, or only on HTTP status + named extensions? Answer determines how much the URN scheme matters. `TBD`
- Is `Idempotency-Key` scoped per `caseId` or globally? What defines a "different request" when the body is `{}`? `TBD`
- What is the portal's expected clock-skew tolerance for the 60s JWT TTL? `TBD`
- Recommended default signing algorithm for new integrations — is `HS256` acceptable as the baseline, or is `RS256` required for any state? `TBD`
- For backends that can't do atomic multi-record writes, is declaring `addressUpdate.supported: false` acceptable, or is address update effectively mandatory? `TBD`
- Which `CardStatus` values gate which self-service actions? Needed to make mapping meaningful. `TBD`
- `userRef` — is there any backend obligation, or is it purely for per-user scoping (ignorable otherwise)? `TBD`
