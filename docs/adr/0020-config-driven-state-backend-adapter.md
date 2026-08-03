# 20. Config-driven state backend adapter

Date: 2026-07-31

## Status

Proposed — the adapter is dark behind `FeatureManagement:use_configurable_state_backend`; phase-5 validation against real state backends is in progress. It is unproven against production traffic.

## Context

Each new state today costs a bespoke MEF connector — roughly 9k LOC of transport, mapping, and matching code, a paired cross-repo CI build, and its own deploy (see [0007-multi-state-plugin-approach.md](./0007-multi-state-plugin-approach.md)). To find out how much of that is real variation, we diffed the two existing connectors — Colorado CBMS over REST, DC stored procedures over ADO.NET — concern by concern; bespoke code dominated the cost, front-loaded on transport and matching. The table below condenses that taxonomy (source anchors were `file:line` references valid as of the spike). Classification: **(a)** shared plumbing, **(b)** config over a shared primitive, **(c)** genuinely bespoke.

| # | Concern | CO | DC | Class |
|---|---|---|---|---|
| 1 | Lookup transport + identifiers | REST `POST /sebt/get-account-details` via Kiota; phone only | Stored-proc RPC `GetHouseholdByGuardian` over `SqlConnection`; email only | (c) transport; (b) identifier set |
| 2 | Case/application disaggregation | `EligSrc` value-set classifier + approval gate; grouped by `SebtAppId` | presence-of-`ApplicationId` split; grouped by `ApplicationId` | (b) |
| 3 | Enum/value mapping | token→enum switches (card, case, app status) | upper-cased word switch | (b) |
| 4 | Derived/inferred fields | issuance hard-coded `SummerEbt` | substring issuance inference; card status from date presence | (c) |
| 5 | Card details loading | co-loaded in the lookup, gated by query flag | co-loaded in the lookup, flag ignored | (a) |
| 6 | Card replacement | one `PATCH`, structured `respCd` | per-case sproc, fail-fast; policy-vs-error by substring `"policy"` | (b) result shape; (c) semantics |
| 7 | Address update | heuristic write-id resolution, one `PATCH`, no partial success | single proc keyed by email | (b) result; (c) write-id resolution |
| 8 | Backend auth | OAuth2 client-credentials | SQL connection string | (a) for HTTP; (c) for SQL |
| 9 | Caching | HybridCache SWR + negative cache | none | (a) |
| 10 | Guardian matching | phone lookup only | proc ORs email with IC+DOB | (c) |
| 11 | Error signaling | structured error codes | mixed: numeric codes + free text | (b) objects; (c) free text |
| 12 | Health check | authenticated HTTP ping | `SELECT 1` | (a) model; (c) probe body |

The plugin contract has no capability model. `IStatePlugin` is a bare marker; a state "supports" an operation by exporting an interface via MEF or by returning `null`/`false` from a method. Capability is implicit in code — invisible to config, ops, and the portal.

## Decision

We make every state backend speak JSON over HTTP, and we drive all of them through one config-driven adapter.

- **JSON over HTTP, everywhere.** A backend that can't (DC's stored procedures) gets a thin exact-passthrough REST wrapper — raw column names out, zero canonical mapping in the wrapper. Once every backend is JSON-over-HTTP, the only thing that varies is the mapping, and mapping is data.
- **One adapter.** [`ConfigurableStateBackend`](../../apps/portal/src/SEBT.Portal.Infrastructure.StateBackends/ConfigurableStateBackend.cs) implements the five Core ports, parameterized entirely by a per-state YAML bundle. Adding a state means authoring config and supplying secrets — no plugin, no cross-repo CI pairing.
- **A closed catalog of named primitives.** Auth schemes, field mappings, enum tables, `keywordRules`, disaggregation rules, the opaque `caseId` token, request binding, result classifiers, enrollment match strategies. Config names a primitive and supplies its parameters; every algorithm lives in fixed code.
- **Capabilities derived from config.** An operation's presence in the YAML *is* its capability — no separate manifest to keep in sync, no MEF-export inference. This replaces the implicit capability model above with declared data.
- **The config/code line sits at the shape of the data, not the meaning of the values.** Anything reducible to "read field X, apply table/predicate Y, emit canonical value Z" is config over a primitive. Anything that inspects state-specific structure in a way no table captures stays code.
- **The anti-DSL cap.** Config never gets comparison operators, boolean combinators, or an expression language. The grounding is empirical: two states in, disaggregation already needed two predicate shapes (`presence`, `valueInSet`) — an open predicate vocabulary drifts into a boolean-expression DSL no one can audit. The cap keeps every config finite: the behaviors a config can express equal the primitives that exist in code.
- **YAML for mapping rulesets.** Comments and anchors matter in mapping config; operational values and secrets stay in JSON/env with key *references*, never values. Config validates at load and fails loud — a bad bundle stops startup, not the first user request.

## Alternatives Considered

1. **Canonical state contract (`spike/state-api`)** — every state implements one portal-defined REST spec.
   🔴 We reject it: it pushes conformance cost onto states, and the portal still needs per-state mediation for backends that can't or won't conform. The adapter keeps mediation on our side, where we can ship it.

2. **Continue per-state MEF plugins** — the status quo per [0007-multi-state-plugin-approach.md](./0007-multi-state-plugin-approach.md).
   🔴 ~9k LOC per state, cross-repo CI pairing, and no capability model — capability stays implicit in exports and null returns.

3. **General transform-expression engine (JSONata, JUST, JsonLogic)** — replace the primitive catalog with one expression evaluator.
   🟡 Roughly 1,200 LOC smaller. But it trades load-time fail-loud validation for silent wrong answers at runtime — a bad expression maps garbage instead of refusing to start. Revisit at state #3 if the catalog sprawls.

## Consequences

- **Unproven against production traffic.** Phase-5 real-backend validation is in progress; test green is still substantially mock-based. "Proposed" means exactly that.
- **Dual-path coexistence.** MEF plugins remain the default and serve all traffic while the flag is off. Two integration paths coexist until the plugins delete (~19k LOC of eventual deletion) — until then, both paths carry maintenance and drift risk.
- **Promotion rules.** A real need no primitive covers means a *new named primitive* in code, with tests — never operators in the YAML. Promote a bespoke concern to a primitive only when a **third** state exhibits the *same shape* — the same shape, not the same concern. The trap is dressing a one-off up as config: DC's date-presence card status looks parameterizable but is a semantic choice, not a mapping table.
- **The documented limit.** Multi-signal fuzzy matching is irreducibly state-specific. We don't pretend it into config — it belongs across the wire, on the state's side of the lookup.
- **Deferred options.** Extracting the adapter to an out-of-process service per state (dual-mode); per-case card fetch — both current states batch-load card data inline in the lookup, so a per-case state needs a new port method plus a `cardDetails` operation config when one appears.
- **Open question:** who owns and operates the per-state wrappers. DC's wrapper lives in the DC connector repo and we run it today; the long-term operating model is TBD.

## Key files

- Ports: [`apps/portal/src/SEBT.Portal.Core/StateBackends/`](../../apps/portal/src/SEBT.Portal.Core/StateBackends/) — `IHouseholdLookupBackend`, `ICardReplacementBackend`, `IAddressUpdateBackend`, `IEnrollmentCheckBackend`, `IStateBackendHealth`
- Driver: [`apps/portal/src/SEBT.Portal.Infrastructure.StateBackends/ConfigurableStateBackend.cs`](../../apps/portal/src/SEBT.Portal.Infrastructure.StateBackends/ConfigurableStateBackend.cs)
- Config model: [`apps/portal/src/SEBT.Portal.Core/StateBackends/Configuration/`](../../apps/portal/src/SEBT.Portal.Core/StateBackends/Configuration/)
- Sample configs: [`dc.sample.yaml`](../../apps/portal/test/SEBT.Portal.Tests/Unit/Infrastructure/StateBackends/ConfigSamples/dc.sample.yaml), [`co.sample.yaml`](../../apps/portal/test/SEBT.Portal.Tests/Unit/Infrastructure/StateBackends/ConfigSamples/co.sample.yaml)
- Authoring guide: [`CONFIG-AUTHORING.md`](../../apps/portal/src/SEBT.Portal.Infrastructure.StateBackends/CONFIG-AUTHORING.md)
- DC REST wrapper: `src/SEBT.Portal.StatePlugins.DC.RestApi` in the `sebt-self-service-portal-dc-connector` repo
