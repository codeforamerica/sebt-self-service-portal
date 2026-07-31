# Config-driven state-backend adapter

One adapter — [`ConfigurableStateBackend`](./ConfigurableStateBackend.cs) — implements all five per-operation Core ports, driven entirely by a per-state YAML bundle. There's no per-state logic code: a state is config plus credentials. Adding a state means authoring a YAML file and supplying its secrets, not writing and deploying a plugin. See [CONFIG-AUTHORING.md](./CONFIG-AUTHORING.md) for the step-by-step authoring guide.

## The thesis

Every state backend speaks JSON over HTTP. Per-state variation — different endpoints, field names, enum tokens, batch shapes, match rules — collapses to config plus a closed catalog of narrow named bricks. DC's SQL sprocs sit behind a thin REST wrapper, so DC speaks JSON too. Once every state is JSON-over-HTTP, the only thing that varies is the mapping, and mapping is data.

## The ports

The Core ports are transport-free — Core has no HTTP or plugin-contract dependencies. They define the operations; the adapter decides how to serialize and transport them. One port per operation:

- [`IHouseholdLookupBackend`](../SEBT.Portal.Core/StateBackends/IHouseholdLookupBackend.cs) — resolve a household from identity signals (email, phone, IC, DOB, …) plus caller context into canonical household data.
- [`ICardReplacementBackend`](../SEBT.Portal.Core/StateBackends/ICardReplacementBackend.cs) — request replacement cards for a batch of opaque `caseId` tokens; the driver fans out one call per decoded token, failing fast on the first non-success.
- [`IAddressUpdateBackend`](../SEBT.Portal.Core/StateBackends/IAddressUpdateBackend.cs) — household-routed mailing-address update: the envelope carries the household identifier plus the household's opaque `caseId` tokens (which may be empty).
- [`IEnrollmentCheckBackend`](../SEBT.Portal.Core/StateBackends/IEnrollmentCheckBackend.cs) — check enrollment eligibility for a batch of children; one match verdict per child.
- [`IStateBackendHealth`](../SEBT.Portal.Core/StateBackends/IStateBackendHealth.cs) — liveness probe; returns healthy/unhealthy. The shared handler chain applies the state's auth scheme to health calls too — DC's open `/health` ignores it, but a backend may require it.

`Capabilities` is derived from which operations the config declares — the presence of an operation *is* its capability.

## The closed brick catalog

Config picks from a fixed set of narrow, named bricks. It never exposes comparison or boolean operators. Detail and worked YAML live in [CONFIG-AUTHORING.md](./CONFIG-AUTHORING.md).

- **Auth schemes.** `api_key` (header + `keyRef`) or `client_credentials` (`tokenUrl` + `clientId` + `clientSecretRef`). Secrets are key *references*, resolved from env / `/run/secrets` — never inlined.
- **Field mapping.** `from` (source property), optional exact date `format`, optional named `enum` table. LHS is our canonical field name, RHS is the state's flavor.
- **Enum tables.** Top-level `enums:` — domain-centered `OurValue: [state tokens]` plus an optional `default` (absent default + unlisted token fails loud). Inverted to a token→our-value lookup at load.
- **`keywordRules`.** Ordered, first-match-wins, case-insensitive substring-contains over one or more `from` sources. Used for DC issuance-type inference. No regex, no conditionals.
- **Disaggregation.** Group records into applications and decide case inclusion via a closed `rule` (`presence` / `valueInSet`) and named `caseInclusion` predicates — not an expression DSL.
- **Opaque `caseId`.** A self-describing token: config lists the routing `fields` (response columns) plus `fromContext` entries — a closed vocabulary of caller-context names, today only `householdIdentifier` — for identifiers a write routes by that the response never echoes. Encode/decode is fixed platform code. The portal and UI treat it as opaque.
- **Request binding.** `constants` (fixed literals), `map` (our input → dotted target path, fail-loud when unresolved), `mapOptional` (bind-if-present / omit-if-absent; rejected on write ops), and the two batch shapes `shared` (one value across the batch, fail-loud on disagreement) and `collect` (per-case values into an array).
- **Result classifier.** Ordered, first-match-wins `conditions`, each exactly one closed kind (`statusIn` / `valueIn`+`field` / `messageContains`+`messageField`), plus a `default`.
- **Enrollment.** `callMode` (`batch` / `perChild`), closed candidate `expand` (`transposeMonthDay`), and named match strategies (`anyRowValueIn` / `confidenceThreshold`).

## Load and validate

Config loads from YAML via YamlDotNet in [`StateBackendConfigurationLoader`](./Configuration/StateBackendConfigurationLoader.cs). Immediately after deserialization, [`StateBackendConfigurationValidator`](./Configuration/StateBackendConfigurationValidator.cs) runs and fails loud — a bad config throws at load, not on the first request. Every check is a function of the config alone, so startup surfaces the failure. What it checks:

- **Field mappings** — every canonical target is a known field; date-typed targets carry an exact `format`.
- **Enum tables** — the referenced table exists, targets an enum-typed field, every canonical key is a real enum member, and no state token is listed under two canonical values.
- **`keywordRules`** — enum-typed target, `order` covers every `map` key, and every named value (including `default`) is a real enum member.
- **Result classifiers** (each configured write op) — every condition is exactly one closed kind; `valueIn` names a `field`; `messageContains` names a `messageField`.
- **`caseId` compositions** — every `fromContext` entry names a known context name; no token field is sourced from both `fields` and `fromContext`.
- **`mapOptional` on writes** — rejected on `cardReplacement` / `addressUpdate` (the write body builders don't read it; a silent no-op would be worse).
- **Enrollment coherence** — `batch` requires an `indexField` on both sides; `perChild` forbids one and forbids `expand`; each match strategy carries its required params, and `confidenceThreshold`'s optional eligibility check takes `field` + `valueIn` together or not at all.

## The anti-DSL discipline

This is the load-bearing rule. Config exposes a **closed set of narrow named bricks** — never comparison operators, boolean combinators, or an expression language. The `>` in `confidenceThreshold`, the `contains` in `keywordRules`, the argmax in the match strategy all live in fixed code; config only names the brick and supplies its parameters.

When a real state needs something no brick covers, you **stop** and add a **new named brick** in code plus tests — the promotion rule. You don't add operators or a mini-language to the YAML. This keeps every config finite and auditable: the set of behaviors a config can express equals the set of bricks that exist in code.

## Status

This is a spike / prototype (DC-568). Be honest about what that means:

- **Phase-4 dark wiring is done.** The write/enrollment ports and the household read path flip atomically behind the `FeatureManagement:use_configurable_state_backend` flag — read per resolve, so a toggle takes effect without a restart. The YAML path comes from `StateBackend:ConfigPath`.
- **MEF plugins remain the default** and serve all traffic while the flag is off. The adapter coexists with them; nothing dispatches through `ConfigurableStateBackend` until the flag flips.
- **Real-backend testing (phase 5) is in progress.** The DC wrapper surface is complete; CO UAT smoke testing is underway. Green is still substantially mock-based — MockHttp and self-authored fixtures.
- **Config trust model:** the YAML defines egress targets and constants. It is deployment-owned config, sitting inside the same trust boundary as appsettings secrets — not user- or state-supplied input.

## References

- Ports: [`SEBT.Portal.Core/StateBackends/`](../SEBT.Portal.Core/StateBackends/)
- Config model: [`SEBT.Portal.Core/StateBackends/Configuration/`](../SEBT.Portal.Core/StateBackends/Configuration/)
- Loader: [`StateBackendConfigurationLoader`](./Configuration/StateBackendConfigurationLoader.cs)
- Validator: [`StateBackendConfigurationValidator`](./Configuration/StateBackendConfigurationValidator.cs)
- Brick implementations: [`Mapping/`](./Mapping/) and [`Auth/`](./Auth/)
- Sample configs: [`dc.sample.yaml`](../../test/SEBT.Portal.Tests/Unit/Infrastructure/StateBackends/ConfigSamples/dc.sample.yaml), [`co.sample.yaml`](../../test/SEBT.Portal.Tests/Unit/Infrastructure/StateBackends/ConfigSamples/co.sample.yaml)
- Spike plan: [`docs/spikes/dc-568/json-adapter-prototype-plan.md`](../../../../docs/spikes/dc-568/json-adapter-prototype-plan.md)
- Authoring guide: [CONFIG-AUTHORING.md](./CONFIG-AUTHORING.md)
