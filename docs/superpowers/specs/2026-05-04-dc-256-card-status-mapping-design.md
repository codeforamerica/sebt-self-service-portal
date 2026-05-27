# DC-256 — Card Status Mapping & Lifecycle Cleanup — Design

**Date:** 2026-05-04
**Status:** Draft (conversation-driven brainstorming, 2026-05-04)
**Ticket:** DC-256 — Card statuses are not mapped correctly

## Goal

Fix incorrect card-status display by:

1. Mapping all relevant CBMS values for Colorado per the DC-95 UI spec.
2. Deriving DC's card status from data shape (presence of an issue date) rather than a non-existent backend status string.
3. Deleting the dead `CardStatus` enum value and four card-timeline timestamp fields from `Application`. Card data lives only on `SummerEbtCase`.
4. Replacing `SummerEbtCase.EbtCardStatus` (raw `string?`) with a typed `CardStatus?` enum that serializes as a string over the API.
5. Simplifying the `CardStatusTimeline` component to a single-purpose "card replacement requested" notice gated by the existing cooldown window.

## Non-Goals

- **Auditing DC's full backend status set.** DC currently exposes only one signal (`EbtCardIssueDate`); a wider audit is blocked on partner spec acquisition. Filed as follow-up.
- **DAMAGED, AUTO REISSUE bespoke UX.** Mapped to `Damaged` for now; a dedicated "replacement in progress" UI is a UX/product decision filed as follow-up.
- **Removing the `SummerEbtCase.CardRequestedAt` field.** That field is the cooldown-tracking timestamp owned by the portal DB (per the 2026-04-23 cooldown-persistence ADR) and is unrelated to the dead Application timeline fields being deleted here.
- **Renaming any other field, refactoring `PluginHouseholdDataMapper` beyond the CardStatus path, or expanding API contracts to other consumers.** Scope-control: only the changes the bug requires.

## Design Decisions

### 1. Treat the four Application card-timeline fields as dead code, not relocate

`Application.CardStatus`, `CardRequestedAt`, `CardMailedAt`, `CardActivatedAt`, `CardDeactivatedAt` are populated by mock data and tests but **no UI component reads them in production**. The frontend's `ChildCard` component reads timeline fields off `SummerEbtCase`, not `Application`. Of the four `Application` timeline fields, three (`CardMailedAt`/`CardActivatedAt`/`CardDeactivatedAt`) are not populated by either connector at any layer.

Verified by repo-wide grep across all four repos. See "Safety check" notes in PR description.

**Decision:** Delete from Interface, Core, API response, frontend Zod schema, mock data, factories, contract tests. Do not relocate to `SummerEbtCase` — there is no signal source to populate them.

### 2. Card status as a typed enum on `SummerEbtCase`, serialized as a string

Today, `SummerEbtCase.EbtCardStatus` is `string?` — a raw connector-provided value with no normalization (DC sends raw DB strings; CO sends `MapCardStatus(...).ToString()`). The frontend re-parses with a brittle `Record<number, string>` lookup table.

**Decision:** Change to `CardStatus?` enum (nullable). Apply `[JsonConverter(typeof(JsonStringEnumConverter))]` to **only the `CardStatus` enum** so it serializes as `"Active"` / `"Lost"` / etc. over the API. Other enums (`ApplicationStatus`, `IssuanceType`, etc.) keep their int serialization unchanged.

**Why scoped to one enum:** Avoids a global serialization-format change. The portal API is a backend-for-frontend with one consumer (the Next.js web app), so this is an atomic contract change with no other deployment concerns. Acknowledged: this creates a deliberate API consistency tradeoff (one enum string, others int) — this is documented intent, not drift.

### 3. DC mapping derives from data presence, not a status string

DC's backend does not reliably emit a card-status string. It does emit `EbtCardIssueDate` when a card has been issued. Per the DC-95 UI spec, DC's only displayed status is "Processed on [date]".

**Decision:** Replace `DcSummerEbtCaseService.MapCardStatus(string?)` with:

```csharp
EbtCardStatus = r.EbtCardIssueDate.HasValue ? CardStatus.Processed : null
```

Delete the existing `MapCardStatus(string?)` method entirely. No string-to-enum mapping needed for DC. No fallthrough logging needed (no string source to fall through).

**Behavior change risk:** If any production DC data has a non-null `EbtCardStatus` of `'DEACTIVATED'` (or similar) **with** `EbtCardIssueDate` populated, today's mapping shows the deactivation; new mapping shows `Processed`. Acceptable risk per tech-lead direction (no sample data available; follow up if real data surfaces issues).

### 4. CO mapping covers only DC-95 spec values; everything else is `Unknown` + alert

The CBMS RAML spec lists 17 possible `ebtCardSts` values. The DC-95 UI spec only enumerates 9 of them. Mapping the other 8 requires UX/product decisions that are out of scope for DC-256.

**Decision:** Map only the 9 DC-95-specified raw values. All other values fall through to `CardStatus.Unknown` and emit a **structured error log** with the raw value. Alerting fires on this; mappings can grow as the spec grows.

Mapping table (case-insensitive):

| CBMS Raw Value | → Enum |
|---|---|
| `ACTIVE` | `Active` |
| `LOST` | `Lost` |
| `STOLEN` | `Stolen` |
| `DAMAGED` | `Damaged` |
| `STATUSED BY STATE, NO REISSUE` | `DeactivatedByState` |
| `DEACTIVATED BY STATE` | `DeactivatedByState` |
| `NOT ACTIVATED` | `NotActivated` |
| `FROZEN` | `Frozen` |
| `UNDELIVERABLE` | `Undeliverable` |
| _everything else_ (incl. `DAMAGED, AUTO REISSUE`, `RETURNED`, `DEACTIVATED`, `DEACTIVATED, NO REISSUE`, `DEACTIVATED/CANCELLED`, `CANCELED BY PRIMARY NO REISSUE`, `UNAUTHORIZED USE, NO REISSUE`, `COMPROMISED, NO REISSUE`) | `Unknown` + **error log** with raw value |

### 5. Drop `Requested`, `Mailed`, and `Deactivated` from the `CardStatus` enum

After this work, neither connector emits these values:
- `Requested` and `Mailed` were only emitted by DC's old string-based mapping (now deleted).
- `Deactivated` is unreached by both states' new mappings (CO uses `DeactivatedByState` for state-initiated deactivations; nothing maps to bare `Deactivated`).

**Decision:** Remove all three values. Final enum is alphabetized for clarity (int values reset 0..9 since we serialize as strings):

```csharp
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CardStatus
{
    Active,
    Damaged,
    DeactivatedByState,
    Frozen,
    Lost,
    NotActivated,
    Processed,
    Stolen,
    Undeliverable,
    Unknown
}
```

### 6. Replace int-cast enum mapping with name-based parsing for `CardStatus` only

`PluginHouseholdDataMapper.cs` currently casts enum values across the Interface→Core boundary using `(CoreEnum)(int)InterfaceEnum`. This works only because both enums share identical int assignments, and breaks silently if the int values diverge. Removing `Requested = 0` and `Mailed = 1` shifts every other value, making the int-cast unsafe.

**Decision:** Add a `ConvertEnum<T>` helper that resolves by member name:

```csharp
private static T? ConvertEnum<T>(object? value) where T : struct, Enum
{
    if (value == null) return null;
    return Enum.TryParse<T>(value.ToString(), ignoreCase: true, out var result)
        ? result
        : null;
}
```

Apply only to `CardStatus`. Other enum casts (`ApplicationStatus`, `IssuanceType`, `BenefitIssuanceType`) retain their int-cast pattern — out of scope for this work.

### 7. `CardStatusTimeline` simplified to a cooldown-gated single-purpose notice

The `CardStatusTimeline` component currently renders a 5-step timeline (Requested → Mailed → Processed → Active → Deactivated) but its trigger is `summerEbtCase.cardRequestedAt != null` — which is set only after a user has previously requested a replacement (cooldown DB hydration). Most of its branches are unreachable. The "Mailed"/"Processed"/"Deactivated" dates were sourced from frontend-Zod-schema fields that the API never populates, so the component renders literal `[MM/DD/YYYY]` placeholder text for those branches in production.

**Decision:** Simplify to a single-purpose "card replacement requested" notice, gated by the cooldown window itself rather than ever-existence of `cardRequestedAt`:

```tsx
{summerEbtCase.allowCardReplacement && (
  isWithinCooldownPeriod(cardRequestedAt) ? (
    <CardStatusTimeline cardRequestedAt={cardRequestedAt} />
  ) : (
    <CardStatusDisplay cardStatus={ebtCardStatus} />
  )
)}
```

Component keeps only the `cardRequestedAt` prop; renders status header + "Card replacement requested on {date}" + reassurance message. Drops `cardStatus`, `cardMailedAt`, `cardActivatedAt`, `cardDeactivatedAt` props. Drops `STATUS_CONFIG`, `statusLabels`, `statusDates`, branch logic. Final component ~30 lines.

After cooldown ends, `CardStatusDisplay` takes over and shows the connector's actual status; the user can re-request a replacement if needed.

### 8. Add fallback label for the new `Processed` UI bucket

`CardStatusDisplay.tsx` already has a `DESCRIPTION_FALLBACK` for missing/empty translations, including `Processed: 'Your card has been processed and is on its way.'`. But the component's *label* lookup at line 76 uses `t(labelKey)` with no fallback — if `cardTableStatusProcessed` is missing from the locale (which it is in both DC and CO CSVs), the literal key string renders to the user.

**Decision:** Add a parallel `LABEL_FALLBACK` map matching the existing `DESCRIPTION_FALLBACK` pattern, with sensible English defaults for all five UI buckets. The `Processed` entry is the one this work needs; the others are belt-and-suspenders.

### 9. State-neutralize the existing `CardStatusTimeline` JS fallback

The current `cardTableStatusMessageRequested1` JS fallback hardcodes "DC SUN Bucks card" — fine for DC, wrong for CO. After our change, this fallback is reached for any cooldown-state user whose locale CSV is missing the key (currently both DC and CO).

**Decision:** Change the fallback wording to be state-neutral ("...new card..." instead of "...new DC SUN Bucks card..."). Pre-empts a CO bug; content team can supply state-specific copy via the spreadsheet later.

### 10. Add Interface↔Core enum parity tests

The Interface and Core layers each declare independent copies of `CardStatus`, `ApplicationStatus`, `IssuanceType`, and `BenefitIssuanceType`. Drift between them (a value added to one but not the other) breaks `PluginHouseholdDataMapper` silently.

**Decision:** Add tests in `SEBT.Portal.Tests` (the only project that can reference both layers) asserting `Enum.GetNames<InterfaceX>()` equals `Enum.GetNames<CoreX>()` for all four enums. Plain `using` aliases — no `extern alias` needed because the `Tests` project doesn't set `Aliases="Core"` on its project reference.

```csharp
using CoreCardStatus = SEBT.Portal.Core.Models.Household.CardStatus;
using InterfaceCardStatus = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CardStatus;

[Fact]
public void CardStatus_InterfaceAndCore_HaveIdenticalMembers()
{
    Assert.Equal(
        Enum.GetNames<InterfaceCardStatus>().OrderBy(n => n),
        Enum.GetNames<CoreCardStatus>().OrderBy(n => n));
}
```

Repeated for the other three enums. These tests fail loudly the moment a future PR adds a value in only one place.

## Concrete File Changes by Repo

### `sebt-self-service-portal-state-connector`

- `src/SEBT.Portal.StatesPlugins.Interfaces/Models/Household/CardStatus.cs`
  - Apply `[JsonConverter(typeof(JsonStringEnumConverter))]`
  - Remove `Requested = 0`, `Mailed = 1`, `Deactivated = 3`
  - Renumber remaining members 0..9 alphabetically
- `src/SEBT.Portal.StatesPlugins.Interfaces/Models/Household/Application.cs`
  - Remove `CardStatus`, `CardRequestedAt`, `CardMailedAt`, `CardActivatedAt`, `CardDeactivatedAt`
- `src/SEBT.Portal.StatesPlugins.Interfaces/Data/Cases/SummerEbtCase.cs`
  - Change `EbtCardStatus` from `string?` to `CardStatus?`
- `src/SEBT.Portal.StatesPlugins.Interfaces.Tests/ModelContractTests.cs`
  - Remove the four `Application.CardX` property-presence assertions (lines 83-86)

### `sebt-self-service-portal-co-connector`

- `src/SEBT.Portal.StatePlugins.CO/Cbms/CbmsResponseMapper.cs`
  - Implement the 9-value mapping from Decision 4
  - Add structured error log for the fallthrough branch (`logger.LogError("Unmapped CBMS card status: {RawValue}", raw)`)
  - Set `EbtCardStatus = MapCardStatus(s.EbtCardSts)` (direct enum, no `.ToString()`)
- `test/SEBT.Portal.StatePlugins.CO.Tests/Cbms/CbmsResponseMapperTests.cs`
  - Tests covering all 9 mapped values + fallthrough behavior

### `sebt-self-service-portal-dc-connector`

- `src/SEBT.Portal.StatePlugins.DC/DcSummerEbtCaseService.cs`
  - Delete `MapCardStatus(string?)` method
  - In `MapToCase`: `EbtCardStatus = r.EbtCardIssueDate.HasValue ? CardStatus.Processed : null`
  - In `MapToApplication`: drop `CardStatus`, `CardRequestedAt`, etc. (fields no longer exist)
- `test/SEBT.Portal.StatePlugins.DC.Tests/DcSummerEbtCaseServiceTests.cs`
  - Tests for the new derivation rule (issue date present → `Processed`; null → `null`)

### `sebt-self-service-portal`

- `src/SEBT.Portal.Core/Models/Household/CardStatus.cs`
  - Same enum changes as Interface (mirror)
- `src/SEBT.Portal.Core/Models/Household/Application.cs`
  - Same field removals as Interface
- `src/SEBT.Portal.Core/Models/Household/SummerEbtCase.cs`
  - Change `EbtCardStatus` to `CardStatus?` (stored property, not computed)
  - Delete the computed `CardStatus` getter (lines 112-115)
- `src/SEBT.Portal.Infrastructure/Repositories/PluginHouseholdDataMapper.cs`
  - Add `ConvertEnum<T>` helper (Decision 6)
  - Replace `Application.CardStatus` int-cast with `ConvertEnum<CardStatus>`... actually delete that line entirely since the field is being removed
  - Replace `SummerEbtCase.EbtCardStatus` string-pull with `ConvertEnum<CardStatus>` (the new enum path)
  - Remove the four `CardX_At` reflection lines on `Application`
- `src/SEBT.Portal.Infrastructure/Repositories/MockHouseholdRepository.cs`
  - Remove all `app.CardRequestedAt = ...` / `CardMailedAt = ...` / etc. assignments
- `src/SEBT.Portal.Api/Models/Household/ApplicationResponse.cs`
  - Remove `CardStatus`, `CardRequestedAt`, `CardMailedAt`, `CardActivatedAt`, `CardDeactivatedAt`
- `src/SEBT.Portal.Api/Models/Household/SummerEbtCaseResponse.cs`
  - Change `EbtCardStatus` from `string?` to `CardStatus?`
- `src/SEBT.Portal.Api/Models/Household/HouseholdDataResponseMapper.cs`
  - Drop the 5 deleted Application field mappings
- `src/SEBT.Portal.TestUtilities/Helpers/HouseholdFactory.cs`
  - Remove all `application.CardX = ...` setters
- `test/SEBT.Portal.Tests/Unit/Models/HouseholdDataResponseMapperTests.cs`
  - Update assertions for removed fields
- `test/SEBT.Portal.Tests/Unit/Helpers/HouseholdFactoryTests.cs`
  - `CreateSummerEbtCase_ShouldNotPopulateCardRequestedAtOrCardLastFour` — adjust for new field shape if needed
- `test/SEBT.Portal.Tests/Unit/EnumParity/EnumParityTests.cs` (**new**)
  - Decision 10's parity tests
- `src/SEBT.Portal.Web/src/features/household/api/schema.ts`
  - `ApplicationSchema`: remove `cardStatus`, `cardRequestedAt`, `cardMailedAt`, `cardActivatedAt`, `cardDeactivatedAt`
  - `SummerEbtCaseSchema`: remove `cardMailedAt`, `cardActivatedAt`, `cardDeactivatedAt`; change `ebtCardStatus` to `z.enum([...]).nullable()`
  - Drop `CARD_STATUS_MAP` (the int→string lookup; no longer needed)
  - Drop `Requested` and `Mailed` from any `CardStatus` literal type / UI bucket map
- `src/SEBT.Portal.Web/src/features/household/components/ChildCard/ChildCard.tsx`
  - Remove `hasCardLifecycleTimeline()` helper
  - Replace timeline trigger with `isWithinCooldownPeriod(cardRequestedAt)`
  - Drop `cardMailedAt`, `cardDeactivatedAt` destructuring
- `src/SEBT.Portal.Web/src/features/household/components/CardStatusTimeline/CardStatusTimeline.tsx`
  - Simplify per Decision 7
  - State-neutralize the `cardTableStatusMessageRequested1` fallback
- `src/SEBT.Portal.Web/src/features/household/components/CardStatusTimeline/CardStatusTimeline.test.tsx`
  - Update for simplified prop shape
- `src/SEBT.Portal.Web/src/features/household/components/CardStatusDisplay/CardStatusDisplay.tsx`
  - Remove `Deactivated` entries from `DESCRIPTION_KEY` and `DESCRIPTION_FALLBACK`
  - Add `LABEL_FALLBACK` map with English defaults for all 5 UI buckets (Decision 8)
  - Wire `t(labelKey, { defaultValue: '' }) || LABEL_FALLBACK[uiStatus]`
- `src/SEBT.Portal.Web/src/mocks/handlers.ts`, `e2e/fixtures/household-data.ts`, component tests
  - Update fixtures for new field shape
- `docs/missing-locale-strings.md`
  - Refresh: remove stale "Not used by any component" note on `cardTableStatusMessageRequested1`; add `cardTableStatusProcessed`/`cardTableStatusMessageProcessed` as new gaps; mark `cardTableStatusMessageRequested2`, `cardTableStatusMailed`, `cardTableStatusMessageMailed`, `cardTableStatusIssued` as no-longer-used

## Testing Strategy

- **Unit tests per repo** for the new mapping behavior:
  - CO: 9 mapped CBMS values + ≥3 fallthrough cases (varied unmatched strings, including the 8 explicitly out-of-spec values, plus one wholly novel value)
  - DC: issue-date-present → `Processed`; issue-date-null → `null`
- **`PluginHouseholdDataMapper` tests** for `ConvertEnum<T>`: name match, case-insensitive match, null input, unparseable input
- **Enum parity tests** per Decision 10
- **`CardStatusTimeline` Vitest** for the simplified prop shape; existing `CardStatusDisplay` tests should pass with minor updates for `LABEL_FALLBACK`
- **Integration test** (Testcontainers MSSQL) confirms end-to-end card-status flow for at least one DC case (issue-date-derived → `Processed` over wire)
- **Manual smoke test** in mock-data mode for both states confirming UI rendering of Active/Processed/Inactive(replacement)/Inactive(no replacement)/Frozen/Undeliverable buckets

## Coordinated Rollout

Four PRs land coordinated. Build order (each consumer references its dependency via NuGet local store / DLL copy):

1. `sebt-self-service-portal-state-connector` — interface + enum changes
2. `sebt-self-service-portal-dc-connector` — consumes new interface
3. `sebt-self-service-portal-co-connector` — consumes new interface
4. `sebt-self-service-portal` — consumes connectors via DLL copy; updated frontend

Open all four as draft. Cross-link each PR description. Mark ready when all four are green. Merge order: state-connector → DC + CO connectors (either order) → portal.

## Risks and Known Limitations

- **DC behavior change.** A DC case with `EbtCardStatus = 'DEACTIVATED'` AND populated `EbtCardIssueDate` would, today, show "Deactivated"; new logic shows `Processed`. No production sample data available to confirm or refute this scenario. Accepted risk.
- **`DAMAGED, AUTO REISSUE` UX.** Maps to `Damaged`, which shows the standard "Request a replacement card" link. Auto-reissue is in flight, so the user might request a duplicate. Filed as follow-up; not a regression (today's mapping shows `Unknown` for this value, which is worse).
- **Locale gaps.** New keys `cardTableStatusProcessed`, `cardTableStatusMessageProcessed`, and existing-but-empty `cardTableStatusMessageRequested1` (both states) need content-team population. Code falls back to English defaults; Spanish content is genuinely missing for the cooldown notice and DC's primary status until the spreadsheet is updated.

## Out of Scope / Follow-up Tickets

1. **DC backend status spec acquisition** — audit DC mapping completeness once partner spec is available.
2. **`DAMAGED, AUTO REISSUE` UX** — bespoke "replacement in progress" treatment (UX/product decision).
3. **Locale CSV updates** — content-team task to populate missing `cardTableStatusProcessed`, `cardTableStatusMessageProcessed`, `cardTableStatusMessageRequested1` rows for DC and CO English + Spanish.
4. **Locale linting** — a build-time check that fails when a referenced i18n key has empty values across all locales (broader anti-regression, not specific to this work).

## PR Description Notes (memorialized for the eventual PR)

Each repo's PR description will include the standard project template (Jira link, summary, related PRs, completion checklist). The portal PR additionally needs:

### i18n gaps to call out

This PR introduces new i18n keys and changes the usage state of two existing keys. Content-team work is required to fully localize but is non-blocking — code falls back to English defaults.

**New keys (need rows added to source Google Sheet, both DC and CO, English + Spanish):**

- `S2 - Portal Dashboard - Card Table - Status Processed` — label rendering DC's primary card status, e.g., "Processed on [MM/DD/YYYY]"
- `S2 - Portal Dashboard - Card Table - Status Message Processed` — description for the Processed state

**Existing keys whose state changes:**

- `cardTableStatusMessageRequested1` (`S2 - Portal Dashboard - Card Table - Status Message Requested 1`): was unreferenced; **now used** by the simplified `CardStatusTimeline` component. Both DC and CO English Current columns are empty in the CSV today — needs population. Code fallback is English-only and now state-neutral.
- `cardTableStatusMessageRequested2` (`Status Message Requested 2`): was previously listed in `docs/missing-locale-strings.md` as unused; **remains unused** after this work. Can be retired from the spreadsheet.
- `cardTableStatusMailed`, `cardTableStatusMessageMailed`, `cardTableStatusIssued`: now unused. Can be retired from the spreadsheet.

### Behavioral change callouts

- **API contract:** `EbtCardStatus` changes from `string` to a string-serialized enum. Single consumer (web frontend) is updated in lockstep in this PR. No external consumers documented.
- **DC card status:** Now derived from `EbtCardIssueDate.HasValue`. Cards with an issue date will show "Processed on [date]"; cards without will show no status box. Replaces the prior 5-string mapping.
- **Deleted enum values:** `Requested`, `Mailed`, `Deactivated` removed from `CardStatus`. Verified no production code path emits or consumes them after this change.
- **Deleted Application fields:** `CardStatus` plus the four timeline timestamps removed. Verified no UI consumer; the frontend reads card data exclusively from `SummerEbtCase`.

### Safety check evidence

Repo-wide grep across all four repos confirmed no UI/business-logic consumers of the deleted Application timeline fields. Mock data, factories, and tests do reference them; updated in this PR. See "Concrete file changes" above.

## Decision History

- 2026-04-17 to 2026-04-20: initial exploration session — surfaced the multi-layer mapping pipeline, discovered the Application/Case duplicate `CardRequestedAt` problem, ended in proposal phase without committing code.
- 2026-05-04: brainstorming resumed; design finalized in this document.
