# Template Adapter Microservice — Options

**Status:** Spike / options report. Not a commitment.

A template adapter is a scaffolded, mostly-stubbed service that implements the SEBT state-backend REST contract ([`docs/openapi.yaml`](../openapi.yaml)). We hand it to a state's staff or vendor; they fill in one thing — the mapping from their data source to our schemas. Everything else (routing, auth, JWT verification, capabilities, health, ProblemDetails, validation, idempotency, `Cache-Control`) ships done. Audience: gov/vendor teams with limited capacity and modest familiarity with modern API tooling.

## Summary & recommendation

**Ship a .NET (ASP.NET Core minimal API) template as the primary, and a Node/TypeScript (Fastify) template as the alternate.** Don't try to serve everyone with one stack, and don't build three.

Why this split, not a single choice — two forces pull opposite ways:

- **Maintainer fit** favors .NET (and Java, which isn't on our candidate list). The state benefits-vendor pool skews Java-first, .NET-second; Python/Node are thin in eligibility/MMIS backends. Whoever inherits this at handoff is more likely a .NET/Java shop. .NET also gives RFC 9457 ProblemDetails *for free* and is what this portal already runs — so we can dogfood the template against our own stack.
- **Lowest codegen-maintenance tax** favors Node. `express-openapi-validator` and `fastify-openapi-glue` validate requests (and responses) at runtime *directly from the spec file* — no codegen step, no generated code to clobber. A spec version bump is "drop in the new YAML and reload," not "regenerate and reconcile." That's the single most valuable property for a low-capacity team, and Node owns it.

We can't collapse that tension into one stack, so offer both and let a state pick by what they can staff. Most will take .NET; a lean digital-services team will prefer Node.

**Both templates are spec-first and share one design:** the OpenAPI spec is the source of truth; the implementer writes only per-operation handler bodies plus mapping config. Custom code is the escape hatch, not the default surface.

**Highest-leverage effort-reducers** (build these regardless of stack):

1. **JWT verification fully handled.** The implementer never touches crypto — a validated `UserContext` (ial, userRef) is handed to their handler, or the request is already rejected with `403 + requiredIal`. This is the hardest part of the contract; owning it removes the biggest failure mode.
2. **Config-driven capabilities + enum-token mapping.** Declare support and map "their `CardStatus` tokens → our enum" in config, no code.
3. **Mock/echo mode + a conformance test suite.** Boot the template with zero backend wiring and get spec-valid canned responses immediately; run our conformance harness against their instance to prove they're done.

---

## Stack options

| | **.NET (ASP.NET Core minimal API)** | **Node / TypeScript (Fastify)** | **Python (FastAPI)** |
|---|---|---|---|
| Idiomatic direction | Code-first (framework pushes spec-from-code) | Both; `fastify-openapi-glue` is spec-first | Code-first (Pydantic → spec) |
| Spec-first server stubs | NSwag (maintained; single-maintainer risk); openapi-generator `aspnetcore` (**caps at .NET 8** — trails our .NET 10) | openapi-generator `nodejs-express-server` (clunky, "not runnable OOTB") | `fastapi-code-generator` (niche); models-only via `datamodel-code-generator` (mature) |
| **Runtime spec-driven validation, zero codegen** | No — codegen-based | **Yes** — `express-openapi-validator`, `fastify-openapi-glue` | Runtime validation yes (Pydantic), but spec is *derived* not *consumed* |
| Spec bump = config change | No (regenerate + reconcile) | **Yes** (replace YAML, reload) | Code-first: edit models; spec-first: regen |
| RFC 9457 ProblemDetails | **Native** (`AddProblemDetails`, `IProblemDetailsService`) | Hand-rolled (thin middleware; no dominant lib) | Add lib (`fastapi-problem-details`) or hand-roll |
| JWT HS256 + RS256 | `Microsoft.IdentityModel.JsonWebTokens` (mature, default) | `jose` (mature, ~76M wk dl) | `PyJWT` (mature; FastAPI's own docs use it — **avoid `python-jose`**) |
| Gov/vendor maintainer fit | 2nd (behind Java) | Weak in legacy gov; fits lean digital teams | Weak in benefits backends; civic-tech foothold |
| Dogfood against this portal | **Yes — same stack** | No | No |

Notes that changed the recommendation:

- **Kiota is client-only** — ruled out for server stubs.
- **Swashbuckle / `Microsoft.AspNetCore.OpenApi` are code-first** — they emit a spec *from* your controllers. For a template where the spec is the fixed contract we hand out, we invert that: the implementer's code must conform to *our* spec, so we generate the spec-conformant scaffold once and hand it over. We're deliberately swimming against .NET's code-first grain here, and that's fine — the spec is frozen from the implementer's point of view.
- **Java** would be a strong candidate on maintainer-fit grounds but isn't in scope. If a state is a Java shop and struggles with both templates, note it — a third template may be worth it later.

Drop Python as a shipped template. FastAPI is excellent but code-first, its spec-first path is the weakest of the three, and it has the poorest benefits-backend maintainer fit. Keep it as a documented "you can also implement the contract yourself in anything" fallback, not a maintained scaffold.

---

## Codegen strategy

**Generate the boundary, hand-write the behavior — and keep the two in separate files so regeneration never clobbers implementer code.**

What's generated vs. hand-written:

| Layer | Source | Regenerated on spec bump? |
|---|---|---|
| DTOs / request+response models | codegen from spec | Yes — pure output |
| Routing table (path → operation) | codegen (or runtime, Node) | Yes |
| Request/response schema validation | codegen or runtime | Yes / N/A |
| Abstract handler interfaces (one method per `operationId`) | codegen | Yes — but as an *abstract* layer |
| **Handler bodies (the mapping)** | **hand-written** | **No — this is the implementer's file** |
| Mapping config, enum tables, capabilities | **hand-written config** | No |

Per-stack:

- **.NET** — openapi-generator `aspnetcore` with `classModifier=abstract`, `operationModifier=abstract`, `generateBody=false`, OR NSwag with `ControllerStyle=partial`/`abstract`. Both produce an abstract base (regenerated) + a concrete derived/partial class (the implementer's, untouched). On a spec bump the compiler forces the implementer to fill any new operation — **fails loud, not silent.** Caveat: openapi-generator trails .NET releases (targets 8.0); prefer NSwag for a .NET 10 codebase, and accept its single-maintainer bus-factor risk (mitigate by vendoring the generated output — we regenerate rarely).
- **Node** — skip codegen for the server. `fastify-openapi-glue` reads the spec at runtime, routes `operationId → handler` via a service object, and validates requests against the schema. Use `openapi-typescript` (or `@hey-api/openapi-ts`) to generate *types only* for handler signatures. Spec bump = swap the YAML; a missing handler for a new `operationId` fails loudly at startup.
- **Python (fallback only)** — `datamodel-code-generator` for Pydantic models; wire routes by hand.

**Maintenance story when the spec bumps.** Within a major version the contract is additive (per the spec's versioning rules), so bumps add fields/capabilities and never repurpose. That's the easy case: new optional fields need no implementer action; a new *operation* is the only thing that demands a new handler, and both stacks surface that loudly (compiler error in .NET, startup error in Node). Pin the template to a spec version and publish the regenerated scaffold as a tagged release; states diff their handler file against the new abstract layer.

---

## Batteries the template provides

Everything on this list is done in the template and configured, not coded, by the implementer.

### JWT verification — the highest-value battery

The `X-Sebt-User-Identity` header is the hardest part of the contract for implementers, and it's pure boilerplate they should never write. The template owns it end to end:

- Middleware verifies the JWT signature (`HS256` shared secret or `RS256` portal public key), `exp`/`iat`, and 60-second TTL. Key material comes from env/secret file (`/run/secrets`), never code.
- On failure it returns `403` with `requiredIal` in ProblemDetails extensions — the implementer writes zero crypto and zero error-shaping.
- On success it hands the handler a validated `UserContext { ial, userRef }`. The implementer reads claims; they never parse or validate a token.
- Config declares whether user assertion is enforced and any minimum-IAL per operation. `userAssertion.supported` in the capabilities response is driven by the same config.
- The header is optional (`required: false`) and never sent for `POST /enrollment/check` — the template already excludes that route from assertion handling.

Libraries: `Microsoft.IdentityModel.JsonWebTokens` (.NET), `jose` (Node). Both do HS256+RS256 cleanly.

### The rest of the batteries

- **Capabilities endpoint, config-driven.** `GET /capabilities` is generated from a config document — the implementer declares `cardDetails.modes`, `coLoadedLookup`, `addressUpdate`, `cardReplacement.statusTracking`, etc. as booleans/enums. No code. Because the spec says an unsupported optional capability should be an unregistered route (router `404`), the template conditionally registers routes from the same config — declare `false` and the route simply isn't mounted.
- **Service auth (OAuth2 client-creds *and* API key), config.** Pick the scheme and supply credentials/JWKS via env/secrets. `GET /health` stays unauthenticated per spec.
- **RFC 9457 ProblemDetails, uniform.** All 4xx/5xx get the correct shape with `type`/`title`/`status`/`detail` and typed extensions (`requiredIal`, `failedCases`). Native in .NET; a thin shared middleware in Node. Implementers throw a typed domain error; the template serializes it.
- **Request/response validation from the spec.** Malformed bodies, bad enums, missing required fields → `400` before the handler runs. Node gets this at runtime from the spec; .NET from generated DTO validation + a validation filter.
- **Health check.** `GET /health` with `pass`/`warn`/`fail` and `503` on fail. The template wires the endpoint and status→HTTP mapping; the implementer supplies one `cmsReachable` probe.
- **Idempotency store.** `POST /cases/{caseId}/card-replacement` requires `Idempotency-Key`; the template enforces presence (`400` if missing), stores key→response for 24h (pluggable: in-memory default, Redis/DB adapter), and replays `200` on a repeat. Distinguishes replay (`200`) from a different pending request (`409`). Implementer writes none of this.
- **`Cache-Control` on capabilities.** Emitted from config so states can shorten TTL before a capability change.
- **`Retry-After` on 429.** Template-owned rate-limit middleware (optional, config-gated).

---

## The mapping layer

This is where the implementer's real work lives, and the design goal is to make it the *only* real work.

**Recommendation: thin per-operation handler interfaces, with declarative config for the mechanical parts.** Don't try to make the whole thing a config/DSL.

### Declarative config handles the mechanical mapping

- **Field mapping.** Their JSON/row field → our schema field, for the flat, 1:1 cases. A field-map config (their `dob` → our `dateOfBirth`, their `first` → `childFirstName`) covers a large share of `SummerEbtCase`/`Application` population.
- **Enum-token tables.** Their CMS `CardStatus`/`ApplicationStatus` tokens → our canonical enum, as a lookup table in config. Unmapped tokens fall through to `Unknown` (the spec already mandates this). This is high-value: enum drift is a common silent-failure source and a table is trivially reviewable.
- **Capabilities + route registration.** Covered above — pure config.
- **Auth, connection strings, keys.** env/secrets.

### Code is the escape hatch where declarative breaks down

A JSON-to-JSON DSL is tempting but breaks on everything that isn't a straight field copy — and the contract has several:

- **Lookup dispatch.** `POST /households/lookup` AND-matches a variable `signals[]` array against whatever the CMS supports, branches on `intent` (`primary` vs `coLoad`), and returns `400` for unknown intent. That's control flow, not a field map.
- **Co-load resolution.** Finding the SNAP/TANF household whose existing card carries the child's benefits is genuinely state-specific logic. No DSL expresses it.
- **Cases vs. applications disaggregation.** States model this differently (DC separates them; CO calls everything an "application"). Splitting one source shape into our `cases[]` + `applications[]` with the `applicationId` back-link is real code.
- **Signal dispatch / fuzzy matching.** `POST /enrollment/check` may fuzzy-match per state policy.
- **Atomic multi-case address update** with rollback and a `failedCases` extension on failure.

So: the implementer implements a handful of handler methods (one per `operationId`), and inside them leans on the template's field-map and enum-table helpers for the boring parts. The interface is small — roughly the eight operations in the spec, several of which are optional and simply left unimplemented (route not registered).

**Why not a full declarative DSL:** it would cover maybe 40% of the work (the flat field/enum mapping), add a second thing to learn, and become a debugging black box exactly where the logic gets hard (co-load, disaggregation). A low-capacity team debugging a mapping DSL at 2am is worse off than one reading a stack trace through their own handler. Give them config for the mechanical parts and plain code for the rest.

---

## Minimizing effort

- **Mock/echo mode.** Ship with `TEMPLATE_MODE=mock`: the service boots with zero CMS wiring and returns spec-valid canned responses (drawn from the spec's own `examples`) for every endpoint. An implementer clones, runs `docker compose up`, and immediately has a conformant backend the portal can talk to — before they've written a line of mapping. This turns "understand the whole contract first" into "make it real one endpoint at a time."
- **Conformance test suite.** A runnable harness (Schemathesis for property/schema-based testing against the live instance, plus hand-written scenario tests for the behaviors schema can't express: idempotency replay, `403 + requiredIal`, capability↔route consistency, atomic-rollback). The implementer points it at their instance and gets a pass/fail checklist. This is their "am I done?" signal and our integration gate — build it once, reuse per state.
- **Containerization/deploy template.** Multi-stage Dockerfile (slim base, non-root), `compose.yaml`, secrets via `/run/secrets`, env-var config — matching this project's container standards so it passes the same security review. States deploy by setting env vars, not editing Dockerfiles.
- **Docs generated from the spec.** Serve the OpenAPI UI (Scalar/Swagger UI) from the running template so the implementer reads the contract *in* their service. Ship a filled worked example (a fictional "Sample State" implementing every endpoint against a toy data source) as the reference — copy-adapt beats read-and-invent for this audience.
- **Starter config with sensible defaults.** Capabilities default to a minimal supported set; enum tables pre-seeded with common CMS token guesses to edit rather than author.

---

## Tradeoffs & risks

- **Convention-heavy scaffolding is magic that's hard to debug.** The more the template hides (routing from config, validation from spec, capabilities from a document), the more a confused implementer faces behavior with no obvious code to step through. Mitigate: keep the hidden machinery *shallow and legible* — config that clearly maps to behavior, loud startup errors, and a mock mode that lets them see the wiring work before they touch it. The line: **hide crypto, transport, and serialization (nobody should hand-write those); expose the mapping logic (they must own it).**
- **Two templates = two things to maintain.** Every spec bump means regenerating/retesting both, and the conformance suite must run against both. Real cost. Accept it only because no single stack serves both the maintainer-fit and low-tax goals — revisit if one template sees no uptake.
- **Codegen tool bus-factor.** NSwag is effectively single-maintainer; openapi-generator trails .NET releases. Mitigate by vendoring generated output and regenerating rarely (additive spec changes rarely force it).
- **Config-declared capabilities can silently mismatch reality.** A state declares `addressUpdate: true` but the handler is a stub. The ADR already flags this. Mitigate: the conformance suite must assert capability↔behavior consistency (declared-supported route must return non-stub responses).
- **Spec-first vs. .NET's code-first grain.** We're using .NET against its idiom. Fine for a frozen contract, but implementers who know .NET may be surprised the spec isn't generated from their code. Document it plainly.
- **Java shops fit neither template well.** The best-fit gov stack isn't offered. Accept for now; watch for a state that struggles with both.

---

## Open questions

| # | Question | Impact |
|---|---|---|
| OQ-1 | **One template or two?** Recommendation is .NET primary + Node alternate. Is the maintenance cost of two justified, or do we ship .NET only and treat Node as community-contributed? | High — sets scope |
| OQ-2 | **Where does the idempotency store live?** In-memory is fine for single-instance dev but loses dedup across restarts/replicas. Do we mandate a pluggable durable store (Redis/DB) for prod, and ship an adapter? | Medium |
| OQ-3 | **How far does field-map config reach before code?** Need to prototype against a real CMS shape (CO's) to find the declarative/code line empirically rather than guess it. | Medium — sizes the "config-only" win |
| OQ-4 | **Do we generate the DC/CO adapters from this template?** The TDD's north star is all-REST with DC/CO on middleware. If the template is good, the CO POC (DC-569) *is* the first template instance. Aligning them avoids building the adapter twice. | High — could merge two workstreams |
| OQ-5 | **RS256 key distribution.** Template verifies with the portal's public key. JWKS endpoint vs. static key file handed over at integration? Affects rotation. | Medium |
| OQ-6 | **Conformance suite as gate vs. guide.** Is passing it a hard precondition for the portal pointing `BaseUrl` at a state, or advisory? | Medium — integration process |
| OQ-7 | **IAL wire format.** Spec emits `ial` as a JSON number including `1.5`. Some JWT libs are fussy about non-integer numeric claims (TDD OQ-3). Verify against `jose`/`JsonWebTokenHandler` in the actual template before freezing. | Low — isolated |
