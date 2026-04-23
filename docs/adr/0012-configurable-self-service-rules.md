# 12. Configurable Self-Service Rules — Config-Driven Action Gating

Date: 2026-04-14

## Status

Accepted

## Context

Portal users can update their mailing address and request a replacement EBT card, but eligibility rules differ by state. DC excludes co-loaded cases (SNAP/TANF issuance types) from self-service entirely. CO restricts card replacement by card status (cards in certain lifecycle states cannot be replaced). Future states will have their own policies.

Encoding per-state policy as conditional code branches would bind the portal to every state's program details, require a code deploy for any policy tuning, and make testing fragmented. The portal team also wants program/product staff to be able to tune policy without engineering involvement.

## Decision

Express self-service policy as configuration, not code. Three layers:

1. **Facts** in the data model. Each `SummerEbtCase` carries an `IssuanceType` enum and an `EbtCardStatus` string that state plugins map to the shared `CardStatus` enum. Plugins never encode policy.
2. **Policy** in `SelfServiceRulesSettings`, bound from `appsettings.json` (shared defaults) and `appsettings.{state}.json` (per-state overrides). Validated at startup via `IValidateOptions<T>`.
3. **Evaluation** in `SelfServiceEvaluator`, which reads `IOptionsMonitor<SelfServiceRulesSettings>` and produces the `AllowedActions` record attached to the household response. The evaluator injects into every write handler; handlers return `Result.PreconditionFailed(PreconditionFailedReason.NotAllowed, …)` (HTTP 412) on denial, matching the Kernel result pattern already established for `NotFound` and `ConcurrencyMismatch`. The API response includes `allowedActions` so the frontend can gate UI accordingly.

Policy is expressed as `(action) → (issuance type) → (enabled flag, allowed card statuses)`. An empty card-status allowlist means "any status permitted" subject to the issuance-type toggle. Issuance types not listed in the config deny by default.

Household-level evaluation is permissive (any qualifying case unlocks the CTA); per-case evaluation is strict (each case decides individually). This matches the AC pattern "user sees CTA but cannot select ineligible cards."

## Consequences

- **Policy tuning is a config edit.** No code change, no deploy pipeline, no migration. `IOptionsMonitor` means the evaluator re-reads `CurrentValue` on each call, so AppConfig-pushed changes take effect without an API restart. (Local-dev file-edit reload depends on `IConfiguration`'s file watcher, which on macOS atomic-rename editor saves may require a manual restart.)
- **Backend is authoritative.** Every write endpoint enforces the policy independently of the frontend. A modified client or curl cannot bypass gating.
- **Frontend has no policy logic.** Components read `allowedActions` flags and render accordingly. Adding a new policy dimension requires changes to the settings shape and the evaluator, but not to every component.
- **Allowlist semantics have a known fragility.** When a new `CardStatus` enum value is added, every state's `AllowedCardStatuses` list must be reviewed or the new value silently disallows. A future `BlockedCardStatuses` option may be added for natively-blocklist-shaped policies; not in scope for DC-157.
- **Plugin contract is unchanged.** Plugins continue to report facts (issuance type, card status) through the existing `IStateMetadataService` / `ISummerEbtCaseService` contracts. Policy changes never require plugin changes.
- **Per-state overrides can be absent.** A state's overlay may omit `SelfServiceRules` entirely and inherit the shared defaults. This is how DC currently operates.

See also:
- ADR-0007 (Multi-state plugin approach) — the per-state config overlay pattern this decision builds on.
- `docs/tdd/self-service-rules.md` — end-to-end mechanism description for maintainers.
