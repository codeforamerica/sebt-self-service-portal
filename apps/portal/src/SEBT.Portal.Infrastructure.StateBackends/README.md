# Config-driven state-backend adapter

One `IStateBackend` implementation — [`ConfigurableStateBackend`](./ConfigurableStateBackend.cs) — driven entirely by a per-state YAML bundle. There's no per-state logic code: a state is config plus credentials. Adding a state means authoring a YAML file and supplying its secrets, not writing and deploying a plugin. See [CONFIG-AUTHORING.md](./CONFIG-AUTHORING.md) for the step-by-step authoring guide.

## The thesis

Every state backend speaks JSON over HTTP. Per-state variation — different endpoints, field names, enum tokens, batch shapes, match rules — collapses to config plus a closed catalog of narrow named bricks. DC's SQL sprocs sit behind a thin REST wrapper, so DC speaks JSON too. Once every state is JSON-over-HTTP, the only thing that varies is the mapping, and mapping is data.

## The port

[`IStateBackend`](../SEBT.Portal.Core/StateBackends/IStateBackend.cs) is transport-free — Core has no HTTP or plugin-contract dependencies. It defines the operations; the adapter decides how to serialize and transport them.

- **`GetHealthAsync`** — unauthenticated liveness probe; returns healthy/unhealthy.
- **`LookupHouseholdAsync`** — resolve a household from search signals (email, phone, IC, DOB) into canonical household data.
- **`RequestCardReplacementAsync`** — request a replacement card for one case, routed by an opaque `caseId`.
- **`UpdateAddressAsync`** — update a household's mailing address across a batch of opaque `caseId`s.
- **`CheckEnrollmentAsync`** — check enrollment eligibility for a batch of children; one match verdict per child.

`Capabilities` is derived from which operations the config declares — the presence of an operation *is* its capability.

## The closed brick catalog

Config picks from a fixed set of narrow, named bricks. It never exposes comparison or boolean operators. Detail and worked YAML live in [CONFIG-AUTHORING.md](./CONFIG-AUTHORING.md).

- **Auth schemes.** `api_key` (header + `keyRef`) or `client_credentials` (`tokenUrl` + `clientId` + `clientSecretRef`). Secrets are key *references*, resolved from env / `/run/secrets` — never inlined.
- **Field mapping.** `from` (source property), optional exact date `format`, optional named `enum` table. LHS is our canonical field name, RHS is the state's flavor.
- **Enum tables.** Top-level `enums:` — domain-centered `OurValue: [state tokens]` plus a `default`. Inverted to a token→our-value lookup at load.
- **`keywordRules`.** Ordered, first-match-wins, case-insensitive substring-contains over one or more `from` sources. Used for DC issuance-type inference. No regex, no conditionals.
- **Disaggregation.** Group records into applications and decide case inclusion via a closed `rule` (`presence` / `valueInSet`) and named `caseInclusion` predicates — not an expression DSL.
- **Opaque `caseId`.** A self-describing token: config lists the routing `fields`; encode/decode is fixed platform code. The portal and UI treat it as opaque.
- **Request binding.** `constants` (fixed literals), `map` (our input → dotted target path), and the two batch shapes `shared` (one value across the batch, fail-loud on disagreement) and `collect` (per-case values into an array).
- **Result classifier.** Ordered, first-match-wins `conditions`, each exactly one closed kind (`statusIn` / `valueIn`+`field` / `messageContains`+`messageField`), plus a `default`.
- **Enrollment.** `callMode` (`batch` / `perChild`), closed candidate `expand` (`transposeMonthDay`), and named match strategies (`anyRowValueIn` / `confidenceThreshold`).

## Load and validate

Config loads from YAML via YamlDotNet in [`StateBackendConfigurationLoader`](./Configuration/StateBackendConfigurationLoader.cs). Immediately after deserialization, [`StateBackendConfigurationValidator`](./Configuration/StateBackendConfigurationValidator.cs) runs and fails loud — a bad config throws at load, not on the first request. It checks enum tables / keyword rules, every configured write op's result classifier, and the enrollment op's `callMode` / `indexField` / `expand` / `match` combination. Every check is a function of the config alone, so startup surfaces the failure.

## The anti-DSL discipline

This is the load-bearing rule. Config exposes a **closed set of narrow named bricks** — never comparison operators, boolean combinators, or an expression language. The `>` in `confidenceThreshold`, the `contains` in `keywordRules`, the argmax in the match strategy all live in fixed code; config only names the brick and supplies its parameters.

When a real state needs something no brick covers, you **stop** and add a **new named brick** in code plus tests — the promotion rule. You don't add operators or a mini-language to the YAML. This keeps every config finite and auditable: the set of behaviors a config can express equals the set of bricks that exist in code.

## Status

This is a spike / prototype (DC-568). Be honest about what that means:

- It's **green against MockHttp and self-authored fixtures** — not yet tested against a real state backend. That's phase 5.
- It's **not yet wired into the running portal.** Nothing in production dispatches through `ConfigurableStateBackend`. That's phase 4.
- It **coexists with the existing MEF plugins**, which still serve all real traffic. This adapter is additive and dark.
- Some declared shapes assume backends that don't exist yet — e.g. DC's single-child enrollment endpoint (`{ isEligible: bool }`) is not built; the config declares the shape the driver will drive once it lands.

## References

- Port: [`IStateBackend`](../SEBT.Portal.Core/StateBackends/IStateBackend.cs)
- Config model: [`SEBT.Portal.Core/StateBackends/Configuration/`](../SEBT.Portal.Core/StateBackends/Configuration/)
- Loader: [`StateBackendConfigurationLoader`](./Configuration/StateBackendConfigurationLoader.cs)
- Validator: [`StateBackendConfigurationValidator`](./Configuration/StateBackendConfigurationValidator.cs)
- Brick implementations: [`Mapping/`](./Mapping/) and [`Auth/`](./Auth/)
- Sample configs: [`dc.sample.yaml`](../../test/SEBT.Portal.Tests/Unit/Infrastructure/StateBackends/ConfigSamples/dc.sample.yaml), [`co.sample.yaml`](../../test/SEBT.Portal.Tests/Unit/Infrastructure/StateBackends/ConfigSamples/co.sample.yaml)
- Spike plan: [`docs/spikes/dc-568/json-adapter-prototype-plan.md`](../../../../docs/spikes/dc-568/json-adapter-prototype-plan.md)
- Authoring guide: [CONFIG-AUTHORING.md](./CONFIG-AUTHORING.md)
