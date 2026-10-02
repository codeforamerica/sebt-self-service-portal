# Config-driven state-backend adapter

One adapter — [`ConfigurableStateBackend`](./ConfigurableStateBackend.cs) — implements all five per-operation Core ports, driven entirely by a per-state YAML bundle. There's no per-state logic code: a state is config plus credentials. Adding a state means authoring a YAML file and supplying its secrets, not writing and deploying a plugin. See [CONFIG-AUTHORING.md](./CONFIG-AUTHORING.md) for the step-by-step authoring guide.

## The thesis

Every state backend speaks JSON over HTTP. Per-state variation — different endpoints, field names, enum tokens, batch shapes, match rules — collapses to config plus a closed catalog of narrow named primitives. DC's SQL sprocs sit behind a thin REST wrapper, so DC speaks JSON too. Once every state is JSON-over-HTTP, the only thing that varies is the mapping, and mapping is data.

## The ports

The Core ports are transport-free. Core has no HTTP or plugin-contract dependencies. They define the operations; the adapter decides how to serialize and transport them. One port per operation:

- [`IHouseholdLookupBackend`](../SEBT.Portal.Core/StateBackends/IHouseholdLookupBackend.cs) — resolve a household from identity signals (email, phone, IC, DOB, …) plus caller context into canonical household data.
- [`ICardReplacementBackend`](../SEBT.Portal.Core/StateBackends/ICardReplacementBackend.cs) — request replacement cards for opaque `caseId` tokens. `callMode: perCase` (default, DC) fans out one call per token; `callMode: batch` (CO) sends one call collecting every decoded case. The household identifier rides on the write envelope, not inside the tokens.
- [`IAddressUpdateBackend`](../SEBT.Portal.Core/StateBackends/IAddressUpdateBackend.cs) — household-routed mailing-address update: the envelope carries `householdIdentifier`. Case tokens may be empty when the binding does not need per-case fields (`collect` / token `shared` still require them).
- [`IEnrollmentCheckBackend`](../SEBT.Portal.Core/StateBackends/IEnrollmentCheckBackend.cs) — check enrollment eligibility for a batch of children; one match verdict per child.
- [`IStateBackendHealth`](../SEBT.Portal.Core/StateBackends/IStateBackendHealth.cs) — liveness probe; returns healthy/unhealthy, including when the socket times out. The shared handler chain applies the state's auth scheme to health calls too. DC's open `/health` ignores it, but a backend may require it.

`Capabilities` is derived from which operations the config declares — the presence of a **complete** operation *is* its capability. A path-only stub is rejected at load, so the portal cannot advertise a feature the adapter cannot actually perform. Card replacement reports `PerCase` or `Batch` from `callMode`.

Card data is batch-loaded today: both DC and CO return card details inline in the household lookup, so there is no per-case card fetch port. A state that only exposes cards via a per-case endpoint needs a new port method plus a `cardDetails` operation config — deferred until a real state needs it.

## The closed catalog of primitives

Config picks from a fixed set of narrow, named primitives. It never exposes comparison or boolean operators. Detail and worked YAML live in [CONFIG-AUTHORING.md](./CONFIG-AUTHORING.md).

- **Auth schemes.** `api_key` (header + `keyRef`) or `client_credentials` (`tokenUrl` + `clientId` + `clientSecretRef`). Secrets are key *references*, resolved from env / `/run/secrets` — never inlined.
- **Field mapping.** `from` (source property), optional exact date `format`, optional named `enum` table. LHS is our canonical field name, RHS is the state's flavor.
- **Enum tables.** Top-level `enums:` — domain-centered `OurValue: [state tokens]` plus an optional `default` (absent default + unlisted token fails fast). Inverted to a token→our-value lookup at load.
- **`keywordRules`.** Ordered, first-match-wins, case-insensitive substring-contains over one or more `from` sources. Used for DC issuance-type inference. No regex, no conditionals.
- **Disaggregation.** Group records into applications and decide case inclusion via a closed `rule` (`presence` / `valueInSet`) and named `caseInclusion` predicates — not an expression DSL. `valueInSet` requires a non-empty `applicationValues` list at load (a missing list would silently treat every row as not application-based); matching is case-insensitive, matching Colorado's existing classifier.
- **Opaque `caseId`.** A self-describing token of backend-issued routing ids (case keys, application ids). Encode/decode is fixed platform code. The portal and UI treat it as opaque. It must not pack PII — writes bind `householdIdentifier` from the request envelope. `fromContext` remains as a primitive for non-PII caller context; packing `householdIdentifier` is rejected at load.
- **Request binding.** `constants` (fixed literals), `map` (our input → dotted target path, fail-fast when unresolved), `mapOptional` (bind-if-present / omit-if-absent; rejected on write ops), and the two batch shapes `shared` (one value across the batch, fail-fast on disagreement) and `collect` (per-case values into an array).
- **Result classifier.** Ordered, first-match-wins `conditions`, each exactly one closed kind (`statusIn` / `valueIn`+`field` / `messageContains`+`messageField`), plus a `default`.
- **Enrollment.** `callMode` (`batch` / `perChild`), closed candidate `expand` (`transposeMonthDay`), and named match strategies (`anyRowValueIn` / `confidenceThreshold`).

## Load and validate

Config loads from YAML via YamlDotNet in [`StateBackendConfigurationLoader`](./Configuration/StateBackendConfigurationLoader.cs). Immediately after deserialization, [`StateBackendConfigurationValidator`](./Configuration/StateBackendConfigurationValidator.cs) runs and fails fast. A bad config throws at load, not on the first request. Every check is a function of the config alone, so startup surfaces the failure. What it checks:

- **Field mappings** — every canonical target is a known field; date-typed targets carry an exact `format`.
- **Enum tables** — the referenced table exists, targets an enum-typed field, every canonical key is a real enum member, and no state token is listed under two canonical values.
- **`keywordRules`** — enum-typed target, `order` covers every `map` key, every named value (including `default`) is a real enum member, and no keyword is empty.
- **Result classifiers** (each configured write op) — every condition is exactly one closed kind; `valueIn` names a `field`; `messageContains` names a `messageField`.
- **`caseId` compositions** — every `fromContext` entry names a known context name that is not `householdIdentifier` (PII); no token field is sourced from both `fields` and `fromContext`.
- **Disaggregation** — `valueInSet` carries a non-empty `applicationValues` list.
- **Incomplete operations** — a declared write/enrollment/lookup op must include its request and result/response mappings; unmatched YAML properties fail at load.
- **`mapOptional` on writes** — rejected on `cardReplacement` / `addressUpdate` (the write body builders don't read it; a silent no-op would be worse).
- **Enrollment coherence** — `batch` requires an `indexField` on both sides; `perChild` forbids one and forbids `expand`; each match strategy carries its required params, and `confidenceThreshold`'s optional eligibility check takes `field` + `valueIn` together or not at all.

## The anti-DSL discipline

Config exposes a **closed set of narrow named primitives** — never comparison operators, boolean combinators, or an expression language. The `>` in `confidenceThreshold`, the `contains` in `keywordRules`, the argmax in the match strategy live in fixed code; config names the primitive and supplies its parameters.

When a real state needs something no primitive covers, stop and add a **new named primitive** in code, with tests — the promotion rule. No operators, no mini-language in the YAML. The set of behaviors a config can express equals the set of primitives that exist in code. Rationale: [`docs/adr/0020-config-driven-state-backend-adapter.md`](../../../../docs/adr/0020-config-driven-state-backend-adapter.md).

## Status

Spike / prototype (DC-568). MEF plugins remain the default live path. The adapter is wired behind a dark-launch flag.

- **Dark launch:** `FeatureManagement:use_configurable_state_backend` (default `false`) plus `StateBackend:ConfigPath`. When the path is set, YAML loads and validates at startup. Traffic stays on MEF plugins until the flag is enabled (AppConfig can flip it without a restart). Enabling the flag with an empty path fails the request.
- **Follow-up:** move `Core/StateBackends/Configuration/` into `Infrastructure.StateBackends` (ADR-0002: Core should not carry HTTP concepts). Deferred so this stack does not reshuffle types.
- **Validation:** the DC wrapper surface is complete; CO UAT smoke testing is underway. Test green is still substantially mock-based (MockHttp + self-authored fixtures) — the adapter is unvalidated against production traffic.
- **Config trust model:** the YAML defines egress targets and constants. It is deployment-owned config, sitting inside the same trust boundary as appsettings secrets. It is not user- or state-supplied input.

## Key files

- Ports: [`SEBT.Portal.Core/StateBackends/`](../SEBT.Portal.Core/StateBackends/) — `IHouseholdLookupBackend`, `ICardReplacementBackend`, `IAddressUpdateBackend`, `IEnrollmentCheckBackend`, `IStateBackendHealth`
- Config model: [`SEBT.Portal.Core/StateBackends/Configuration/`](../SEBT.Portal.Core/StateBackends/Configuration/)
- Loader: [`StateBackendConfigurationLoader`](./Configuration/StateBackendConfigurationLoader.cs)
- Validator: [`StateBackendConfigurationValidator`](./Configuration/StateBackendConfigurationValidator.cs)
- Primitive implementations: [`Mapping/`](./Mapping/) and [`Auth/`](./Auth/)
- Sample configs: [`dc.sample.yaml`](../../test/SEBT.Portal.Tests/Unit/Infrastructure/StateBackends/ConfigSamples/dc.sample.yaml), [`co.sample.yaml`](../../test/SEBT.Portal.Tests/Unit/Infrastructure/StateBackends/ConfigSamples/co.sample.yaml)
- Authoring guide: [CONFIG-AUTHORING.md](./CONFIG-AUTHORING.md)
- ADR: [`docs/adr/0020-config-driven-state-backend-adapter.md`](../../../../docs/adr/0020-config-driven-state-backend-adapter.md)
