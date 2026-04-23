# Configurable Self-Service Rules

## Problem Statement / Intent

Portal users want to do two self-service actions: update their mailing address and request a replacement EBT card. Which actions are available to a given user depends on their state and on per-case properties (issuance type, card status) that differ between states.

DC's policy is shaped by co-loaded cases: benefits loaded onto a SNAP or TANF EBT card cannot be self-serviced through the portal. The user must call FIS (Fidelity Information Services) instead. Non-co-loaded and applicant cases get standard self-service.

CO's policy is shaped by card status: cards in certain lifecycle states (deactivated by the state, never activated, marked for no reissue) cannot be replaced through the portal. Most other statuses are eligible.

Future states will have their own policies. The portal needs to enforce these without per-state code branches, and product/program staff need a path to tune policies over time without a code deploy.

---

## Design Decisions

### Facts vs. Policy

Same split as [Minimum IAL Determination](./minimum-ial-determination.md):

1. **Facts** (reported by state plugins): each `SummerEbtCase` carries an `IssuanceType` enum (SummerEbt, SnapEbtCard, TanfEbtCard, Unknown) and an `EbtCardStatus` string that the plugin maps into the shared `CardStatus` enum. These are objective statements about the case.
2. **Policy** (owned by Core, configured per state): `SelfServiceRulesSettings` maps each `(action, issuance type)` pair to an `enabled` flag plus an allowlist of `CardStatus` values for which the action is permitted.

Plugins never encode policy. The portal's `SelfServiceEvaluator` reads settings and produces a single `AllowedActions` record (household-level aggregate) attached to the household response sent to the frontend. A future enhancement may split this into `HouseholdAllowedActions` + per-case `CaseAllowedActions` (see Open Items below); not in scope for DC-157.

### Allowlist, not blocklist

`IssuanceTypeRuleSettings.AllowedCardStatuses` is an allowlist. An empty list means "all statuses allowed" (subject to the issuance-type `Enabled` flag). This was chosen because DC's policy is naturally allowlist-shaped (co-loaded issuance types are entirely disabled, `SummerEbt` is allowlist-anything). CO's policy is naturally blocklist-shaped but fits by enumeration of the complement.

**Known fragility:** when a new `CardStatus` enum value is added, every state's allowlist must be reviewed. A future enhancement (not in DC-157) may add a `BlockedCardStatuses` field for policies that are natively blocklist-shaped.

### Permissive household aggregation

`EvaluateHousehold` returns `true` for an action if *any* case in the household qualifies. This matches the AC: "if the user has a mix of co-loaded and non-co-loaded cases, the user will see the CTA but when they go to select their card they should not be able to select their co-loaded cards." The household-level `true` lets the CTA render; per-case filtering at the selection step blocks the ineligible cards.

Per-case filtering in the UI is currently handled by `CardSelection` (hardcoded issuance-type exclusion for SNAP/TANF co-loaded cases). A follow-up may extend `SelfServiceEvaluator` to emit per-case `CaseAllowedActions` so the UI filter becomes config-driven instead of hardcoded.

### Hot reload via IOptionsMonitor

`SelfServiceEvaluator` takes `IOptionsMonitor<SelfServiceRulesSettings>`, not `IOptions`. The evaluator re-reads `options.CurrentValue` on each call, so an AppConfig-pushed config change takes effect on the next request without an API restart. `IOptionsMonitor` (singleton) was chosen over `IOptionsSnapshot` (scoped) to match the DC-255 pattern used by `SmartyAddressUpdateService`'s HttpClient factory, where a singleton factory cannot resolve scoped options. See `Dependencies.cs` for the DI wiring comment.

(Local-dev file-edit reload depends on `IConfiguration`'s built-in file watcher. On macOS atomic-rename editor saves, the watcher may not fire reliably; a manual API restart is the reliable fallback during local iteration.)

### Backend is authoritative

Every write endpoint (`GetHouseholdDataQueryHandler`, `UpdateAddressCommandHandler`, `RequestCardReplacementCommandHandler`) calls the evaluator before doing work and returns `Result.PreconditionFailed(PreconditionFailedReason.NotAllowed, …)` (HTTP 412) on denial. This matches the Kernel result pattern already established for `NotFound` and `ConcurrencyMismatch` denials. The frontend reads `allowedActions` for UX but cannot bypass the backend. A crafted request from a modified client or curl will 412 with the configured `DisabledMessageKey` as the response body.

### Server-driven UI gating

The portal response includes `allowedActions` at the household level. Frontend components (`ActionButtons`, `HouseholdSummary`) consume those flags directly. `ChildCard` and `CardSelection` currently apply per-case filtering based on issuance type (hardcoded) and cooldown-window checks. There is no client-side policy evaluation — the frontend merely renders what the backend says is allowed. This means policy tuning is config-only; a content change in `DisabledMessageKey` propagates through the i18n system without a component edit.

---

## Data Flow

```
appsettings.json           →  SelfServiceRulesSettings (validated at startup)
appsettings.{state}.json   →     overrides merge in via ASP.NET config pipeline
                                        ↓
                           IOptionsMonitor<SelfServiceRulesSettings>
                                        ↓
                           SelfServiceEvaluator.Evaluate(...)
                                        ↓
                                  AllowedActions
                                        ↓
                           API response (HouseholdData DTO)
                                        ↓
                  Frontend: ActionButtons, HouseholdSummary
                  (CardSelection + ChildCard apply per-case filtering
                   from issuance type + cooldown locally)
```

---

## Config Shape

```jsonc
"SelfServiceRules": {
  "AddressUpdate": {
    "Enabled": true,                                  // top-level toggle
    "DisabledMessageKey": "i18nKeyForDenialCopy",
    "ByIssuanceType": {
      "SummerEbt":   { "Enabled": true, "AllowedCardStatuses": [] },   // [] = any status
      "TanfEbtCard": { "Enabled": false },                              // disabled entirely
      "SnapEbtCard": { "Enabled": false },
      "Unknown":     { "Enabled": false }                               // safety default
    }
  },
  "CardReplacement": {
    "Enabled": true,
    "DisabledMessageKey": "i18nKeyForDenialCopy",
    "ByIssuanceType": {
      "SummerEbt":   { "Enabled": true, "AllowedCardStatuses": ["Lost", "Stolen", "Damaged"] },
      "TanfEbtCard": { "Enabled": false },
      "SnapEbtCard": { "Enabled": false },
      "Unknown":     { "Enabled": false }
    }
  }
}
```

Issuance types not listed in `ByIssuanceType` deny by default.
`AllowedCardStatuses: []` means any status (subject to `Enabled`).
`AllowedCardStatuses` is case-insensitive; unparseable statuses deny by default.

---

## Per-State Policy (as of 2026-04-14)

### DC

- `AddressUpdate`: enabled for `SummerEbt` (any card status), disabled for co-loaded types.
- `CardReplacement`: enabled for `SummerEbt` with card status in `{Lost, Stolen, Damaged}`, disabled for co-loaded types.

DC inherits these values from the defaults in `appsettings.json`.

### CO

- `AddressUpdate`: enabled for `SummerEbt` (any card status). CO has no co-loaded pattern, so `SnapEbtCard` / `TanfEbtCard` entries are mostly irrelevant — they're included for safety.
- `CardReplacement`: enabled for `SummerEbt` with an allowlist that excludes `DeactivatedByState` and `NotActivated`. "Statused by state no reissue" per the AC is pending PM confirmation (likely synonymous with `DeactivatedByState`).

CO's policy lives in `appsettings.co.json`. See the punch list at `docs/.local/branch-context/DC-157/` for open items pending PM input and CBMS readiness.

---

## How Plugins Contribute

Plugins contribute only facts. Each state's `CbmsResponseMapper` (or equivalent) translates the backend's raw card-status string into a `CardStatus` enum value. If a plugin maps a token to `CardStatus.Unknown`, the configured policy decides whether Unknown is allowed — typically it is not, as a safety default.

See `sebt-self-service-portal-co-connector/src/SEBT.Portal.StatePlugins.CO/Cbms/CbmsResponseMapper.cs` for the CO mapping. The mapper logs at information level when a token falls through to `Unknown`, so new CBMS tokens show up in logs and can be mapped without guessing.

---

## Testing

See `docs/.local/branch-context/DC-157/testing-summary.md` for the full breakdown. Key points:

- `SelfServiceEvaluatorTests` runs both a DC-shaped and a CO-shaped fixture through every scenario, proving the mechanism responds to config changes.
- `SelfServiceRulesSettingsValidatorTests` asserts that misconfigurations fail startup.
- Handler tests cover the 412 path when the evaluator denies.
- Frontend unit + Playwright tests drive CTAs and selection UI under varied issuance types and permission shapes.

---

## Migration and Policy Tuning

Policy changes require only a config edit at the source (AppConfig in deployed environments, `appsettings.{state}.json` in dev). Because `SelfServiceEvaluator` uses `IOptionsMonitor`, an AppConfig-pushed change takes effect on the next request. Local file-edit reload works in principle but on macOS may need a manual API restart because of FileSystemWatcher behavior on atomic-rename saves. No code, no migration, no deploy beyond config.

When adding a new state:
1. Add `appsettings.{newstate}.json` with the state's `SelfServiceRules` block (or omit to inherit defaults).
2. Write tests that pair against the existing DC and CO tests — same inputs, expected divergence.
3. Confirm the state's connector maps its backend card-status strings into the shared `CardStatus` enum.

When adding a new enum value to `CardStatus`:
1. Add to the enum in `sebt-self-service-portal-state-connector` and `sebt-self-service-portal`.
2. Rebuild the state-connector NuGet package; connectors pick it up on next build.
3. Each connector maps its new backend token, if applicable.
4. Each state's `appsettings.{state}.json` reviews its `AllowedCardStatuses` lists and adds the new value if it should be permitted.

Step 4 is the fragile one and is the reason we may eventually add a first-class blocklist option.
