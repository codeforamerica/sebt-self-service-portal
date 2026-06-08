# DC-256 — Card Status Mapping & Lifecycle Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix CO's broken CBMS card-status mapping per the DC-95 spec, derive DC's status from issue-date presence, delete dead `Application` card-lifecycle fields, change `SummerEbtCase.EbtCardStatus` to a string-serialized enum, and simplify the cooldown timeline UI.

**Architecture:** Coordinated four-PR change across `state-connector` (interface contracts), `dc-connector` and `co-connector` (state-specific mapping), and the portal (Core models, API response, frontend schema/UI). Build order: state-connector → DC + CO connectors → portal. Each repo gets its own worktree on `feature/DC-256-card-status-mapping`.

**Tech Stack:** C# / .NET 10, MEF (System.Composition), xUnit, NSubstitute, Bogus, Testcontainers (MSSQL); Next.js 16, React 19, Zod, Vitest.

**Jira:** DC-256

**Spec:** `docs/superpowers/specs/2026-05-04-dc-256-card-status-mapping-design.md`

---

## File Structure

| Repo | File | Action | Responsibility |
|------|------|--------|----------------|
| state-connector | `src/SEBT.Portal.StatesPlugins.Interfaces/Models/Household/CardStatus.cs` | Modify | Remove `Requested`/`Mailed`/`Deactivated`; add `[JsonStringEnumConverter]`; renumber 0..9 |
| state-connector | `src/SEBT.Portal.StatesPlugins.Interfaces/Models/Household/Application.cs` | Modify | Remove `CardStatus`, `CardRequestedAt`, `CardMailedAt`, `CardActivatedAt`, `CardDeactivatedAt` |
| state-connector | `src/SEBT.Portal.StatesPlugins.Interfaces/Data/Cases/SummerEbtCase.cs` | Modify | `EbtCardStatus`: `string?` → `CardStatus?` |
| state-connector | `src/SEBT.Portal.StatesPlugins.Interfaces.Tests/ModelContractTests.cs` | Modify | Update `Application` property assertions |
| dc-connector | `src/SEBT.Portal.StatePlugins.DC/DcSummerEbtCaseService.cs` | Modify | Replace `MapCardStatus` with `EbtCardIssueDate.HasValue ? Processed : null`; drop card fields from `MapToApplication` |
| dc-connector | `test/SEBT.Portal.StatePlugins.DC.Tests/DcSummerEbtCaseServiceTests.cs` | Modify | Tests for new derivation rule |
| co-connector | `src/SEBT.Portal.StatePlugins.CO/Cbms/CbmsResponseMapper.cs` | Modify | Update `MapCardStatus` to 9-value table + LogError fallthrough; drop `.ToString()` on `EbtCardStatus` assignment |
| co-connector | `src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/CbmsResponseMapperTests.cs` | Modify | Tests for all 9 mapped values + fallthrough error logging |
| portal | `src/SEBT.Portal.Core/Models/Household/CardStatus.cs` | Modify | Mirror Interface enum |
| portal | `src/SEBT.Portal.Core/Models/Household/Application.cs` | Modify | Remove `CardStatus` + 4 timeline fields |
| portal | `src/SEBT.Portal.Core/Models/Household/SummerEbtCase.cs` | Modify | `EbtCardStatus`: `string?` → `CardStatus?`; remove computed getter |
| portal | `src/SEBT.Portal.Infrastructure/Repositories/PluginHouseholdDataMapper.cs` | Modify | Add `ConvertEnum<T>` helper; remove Application card-field mappings; convert Case `EbtCardStatus` via name parse |
| portal | `src/SEBT.Portal.Infrastructure/Repositories/MockHouseholdRepository.cs` | Modify | Remove all `app.CardX = …` assignments |
| portal | `src/SEBT.Portal.Api/Models/Household/ApplicationResponse.cs` | Modify | Remove `CardStatus` + 4 timeline fields |
| portal | `src/SEBT.Portal.Api/Models/Household/SummerEbtCaseResponse.cs` | Modify | `EbtCardStatus`: `string?` → `CardStatus?` |
| portal | `src/SEBT.Portal.Api/Models/Household/HouseholdDataResponseMapper.cs` | Modify | Drop deleted Application field mappings |
| portal | `src/SEBT.Portal.TestUtilities/Helpers/HouseholdFactory.cs` | Modify | Remove `application.CardX` setters |
| portal | `test/SEBT.Portal.Tests/Unit/EnumParity/EnumParityTests.cs` | Create | Interface↔Core enum parity assertions |
| portal | `test/SEBT.Portal.Tests/Unit/Models/HouseholdDataResponseMapperTests.cs` | Modify | Update for removed/changed fields |
| portal | `test/SEBT.Portal.Tests/Unit/Helpers/HouseholdFactoryTests.cs` | Modify | Update for removed field shape |
| portal | `test/SEBT.Portal.Tests/Unit/Repositories/PluginHouseholdDataMapperTests.cs` | Create or modify | Tests for `ConvertEnum<T>` |
| portal | `src/SEBT.Portal.Web/src/features/household/api/schema.ts` | Modify | Update Zod schemas; drop `CARD_STATUS_MAP` |
| portal | `src/SEBT.Portal.Web/src/features/household/components/ChildCard/ChildCard.tsx` | Modify | Cooldown-gated timeline rendering |
| portal | `src/SEBT.Portal.Web/src/features/household/components/CardStatusTimeline/CardStatusTimeline.tsx` | Modify | Simplify to single-state cooldown notice |
| portal | `src/SEBT.Portal.Web/src/features/household/components/CardStatusTimeline/CardStatusTimeline.test.tsx` | Modify | Update for simplified prop shape |
| portal | `src/SEBT.Portal.Web/src/features/household/components/CardStatusDisplay/CardStatusDisplay.tsx` | Modify | Add `LABEL_FALLBACK`; remove `Deactivated` entries |
| portal | `src/SEBT.Portal.Web/src/features/household/components/CardStatusDisplay/CardStatusDisplay.test.tsx` | Modify | Test new fallback behavior |
| portal | `src/SEBT.Portal.Web/src/mocks/handlers.ts` | Modify | Remove deleted Application fields; convert `ebtCardStatus` to enum string |
| portal | `src/SEBT.Portal.Web/e2e/fixtures/household-data.ts` | Modify | Remove deleted fields from fixtures |
| portal | Various component test files | Modify | Update test fixtures (see Phase 5) |
| portal | `docs/missing-locale-strings.md` | Modify | Refresh i18n status notes |

---

## Phase 0 — Setup (worktrees for connector repos)

The portal worktree already exists at `.worktrees/DC-256-card-status-mapping`. Create matching worktrees in the three other repos so each PR can be developed in isolation.

### Task 0: Create worktrees in state-connector, dc-connector, co-connector

**Files:** None (workspace setup)

- [ ] **Step 0.1: Fetch latest origin/main in all three repos**

```bash
git -C /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal-state-connector fetch origin main
git -C /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal-dc-connector fetch origin main
git -C /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal-co-connector fetch origin main
```

- [ ] **Step 0.2: Create worktree in state-connector**

```bash
git -C /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal-state-connector worktree add /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal-state-connector/.worktrees/DC-256-card-status-mapping -b feature/DC-256-card-status-mapping origin/main
```

Expected: "Preparing worktree (new branch 'feature/DC-256-card-status-mapping')"

- [ ] **Step 0.3: Create worktree in dc-connector**

```bash
git -C /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal-dc-connector worktree add /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal-dc-connector/.worktrees/DC-256-card-status-mapping -b feature/DC-256-card-status-mapping origin/main
```

- [ ] **Step 0.4: Create worktree in co-connector**

```bash
git -C /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal-co-connector worktree add /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/DC-256-card-status-mapping -b feature/DC-256-card-status-mapping origin/main
```

- [ ] **Step 0.5: Verify all four worktrees**

```bash
git -C /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal worktree list
git -C /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal-state-connector worktree list
git -C /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal-dc-connector worktree list
git -C /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal-co-connector worktree list
```

Expected: each repo lists a worktree at `.worktrees/DC-256-card-status-mapping` on branch `feature/DC-256-card-status-mapping`.

- [ ] **Step 0.6: Clean stale NuGet packages from local store**

Per memory note `feedback_worktree_nuget_cleanup`: stale `.nupkg` files cause worktree builds to resolve outdated state-connector versions.

```bash
ls ~/nuget-store/SEBT.Portal.StatesPlugins.Interfaces.0.0.1-dev*.nupkg 2>/dev/null
rm ~/nuget-store/SEBT.Portal.StatesPlugins.Interfaces.0.0.1-dev*.nupkg 2>/dev/null
```

Expected: any stale dev-build packages removed; the next state-connector build will produce the canonical version.

---

## Phase 1 — State-connector contract changes

All work below in `/Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal-state-connector/.worktrees/DC-256-card-status-mapping`.

### Task 1: Update `CardStatus` enum

**Files:**
- Modify: `src/SEBT.Portal.StatesPlugins.Interfaces/Models/Household/CardStatus.cs`

- [ ] **Step 1.1: Replace the enum file contents**

Replace the entire file with:

```csharp
using System.Text.Json.Serialization;

namespace SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

/// <summary>
/// Represents the status of a benefit card.
/// Connectors map raw backend statuses to these values so the portal
/// can render appropriate UI and determine available self-service actions.
/// Serialized as the member name (e.g., "Active", "Lost") over the API
/// per JsonStringEnumConverter, so member identifiers are the wire contract.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CardStatus
{
    Active = 0,
    Damaged = 1,
    DeactivatedByState = 2,
    Frozen = 3,
    Lost = 4,
    NotActivated = 5,
    Processed = 6,
    Stolen = 7,
    Undeliverable = 8,
    Unknown = 9
}
```

- [ ] **Step 1.2: Build the project to confirm the enum compiles standalone**

```bash
cd /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal-state-connector/.worktrees/DC-256-card-status-mapping
dotnet build src/SEBT.Portal.StatesPlugins.Interfaces/SEBT.Portal.StatesPlugins.Interfaces.csproj
```

Expected: BUILD SUCCEEDED. (May warn about Application.cs references — that's Task 2.)

### Task 2: Remove card lifecycle fields from `Application`

**Files:**
- Modify: `src/SEBT.Portal.StatesPlugins.Interfaces/Models/Household/Application.cs`

- [ ] **Step 2.1: Read the existing file**

Inspect `src/SEBT.Portal.StatesPlugins.Interfaces/Models/Household/Application.cs` to find the 5 fields to remove.

- [ ] **Step 2.2: Delete the 5 properties**

Remove these properties (lines may differ slightly):
- `public CardStatus CardStatus { get; set; }`
- `public DateTime? CardRequestedAt { get; set; }`
- `public DateTime? CardMailedAt { get; set; }`
- `public DateTime? CardActivatedAt { get; set; }`
- `public DateTime? CardDeactivatedAt { get; set; }`

Plus any using directives that become unused.

- [ ] **Step 2.3: Build the Interfaces project**

```bash
dotnet build src/SEBT.Portal.StatesPlugins.Interfaces/SEBT.Portal.StatesPlugins.Interfaces.csproj
```

Expected: BUILD SUCCEEDED.

### Task 3: Change `SummerEbtCase.EbtCardStatus` to typed enum

**Files:**
- Modify: `src/SEBT.Portal.StatesPlugins.Interfaces/Data/Cases/SummerEbtCase.cs`

- [ ] **Step 3.1: Update the property type**

Find:
```csharp
public string? EbtCardStatus { get; init; }
```

Replace with:
```csharp
public CardStatus? EbtCardStatus { get; init; }
```

- [ ] **Step 3.2: Build the project**

```bash
dotnet build src/SEBT.Portal.StatesPlugins.Interfaces/SEBT.Portal.StatesPlugins.Interfaces.csproj
```

Expected: BUILD SUCCEEDED.

### Task 4: Update `ModelContractTests`

**Files:**
- Modify: `src/SEBT.Portal.StatesPlugins.Interfaces.Tests/ModelContractTests.cs`

- [ ] **Step 4.1: Run tests to verify they fail on the deleted properties**

```bash
dotnet test src/SEBT.Portal.StatesPlugins.Interfaces.Tests/SEBT.Portal.StatesPlugins.Interfaces.Tests.csproj
```

Expected: FAIL — assertions on `CardStatus`, `CardRequestedAt`, `CardMailedAt`, `CardActivatedAt`, `CardDeactivatedAt` (lines 82-86) miss; `Assert.Equal(15, names.Length)` fails because count is now 10.

- [ ] **Step 4.2: Update the `Application_has_expected_properties` test**

Locate the test and remove these `Assert.Contains` lines:
- `Assert.Contains("CardStatus", names);`
- `Assert.Contains("CardRequestedAt", names);`
- `Assert.Contains("CardMailedAt", names);`
- `Assert.Contains("CardActivatedAt", names);`
- `Assert.Contains("CardDeactivatedAt", names);`

Update the count assertion from `Assert.Equal(15, names.Length);` to `Assert.Equal(10, names.Length);`.

- [ ] **Step 4.3: Run tests to confirm they pass**

```bash
dotnet test src/SEBT.Portal.StatesPlugins.Interfaces.Tests/SEBT.Portal.StatesPlugins.Interfaces.Tests.csproj
```

Expected: PASS.

### Task 5: Build and verify NuGet package output

**Files:** None (verification only)

- [ ] **Step 5.1: Build solution to package the new contracts**

```bash
cd /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal-state-connector/.worktrees/DC-256-card-status-mapping
dotnet build
```

Expected: BUILD SUCCEEDED. The csproj has `GeneratePackageOnBuild=true`, so a new `.nupkg` lands in `~/nuget-store/`.

- [ ] **Step 5.2: Verify the new package exists**

```bash
ls -t ~/nuget-store/SEBT.Portal.StatesPlugins.Interfaces.0.0.1-dev*.nupkg | head -3
```

Expected: a freshly-timestamped `.nupkg` is present.

### Task 6: Commit Phase 1

- [ ] **Step 6.1: Review diff**

```bash
git -C /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal-state-connector/.worktrees/DC-256-card-status-mapping diff
```

- [ ] **Step 6.2: Commit**

```bash
git -C /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal-state-connector/.worktrees/DC-256-card-status-mapping add -A
git -C /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal-state-connector/.worktrees/DC-256-card-status-mapping commit -m "DC-256: Update CardStatus enum and remove dead Application card fields

Drop Requested/Mailed/Deactivated values, add JsonStringEnumConverter
attribute, and renumber alphabetically. Remove CardStatus + 4 timeline
fields from Application (dead code, no production consumer). Change
SummerEbtCase.EbtCardStatus to typed enum.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Phase 2 — DC-connector adoption

All work below in `/Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal-dc-connector/.worktrees/DC-256-card-status-mapping`.

### Task 7: Restore NuGet to pick up new state-connector package

**Files:** None (dependency refresh)

- [ ] **Step 7.1: Restore packages**

```bash
cd /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal-dc-connector/.worktrees/DC-256-card-status-mapping
dotnet restore
```

Expected: Restore SUCCEEDED. The wildcard package reference (`0.0.1-dev-*`) picks up the freshly-built state-connector package.

- [ ] **Step 7.2: Build to confirm — expect failures from contract changes**

```bash
dotnet build
```

Expected: BUILD FAILED. References to `Application.CardStatus`, `Application.CardRequestedAt`, etc. break. References to `MapCardStatus(...)` may still compile. The `EbtCardStatus = ...ToString()` assignment will fail because Case now expects `CardStatus?`.

### Task 8: Replace string-based card status mapping with derivation

**Files:**
- Modify: `src/SEBT.Portal.StatePlugins.DC/DcSummerEbtCaseService.cs`

- [ ] **Step 8.1: Add the new derivation in `MapToCase`**

In the `MapToCase` method (around line 280-320), find the `EbtCardStatus = r.EbtCardStatus,` line and replace it with:

```csharp
EbtCardStatus = r.EbtCardIssueDate.HasValue ? CardStatus.Processed : null,
```

- [ ] **Step 8.2: Remove the `MapCardStatus(string?)` method**

Delete the entire `MapCardStatus` private method (around line 359-370).

- [ ] **Step 8.3: Remove card-status references from `MapToApplication`**

In `MapToApplication` (around line 322-344), delete the line:

```csharp
CardStatus = MapCardStatus(first.EbtCardStatus),
```

(There are no `CardRequestedAt`/`CardMailedAt`/etc. references in DC's `MapToApplication` — only `CardStatus`.)

- [ ] **Step 8.4: Build to confirm**

```bash
dotnet build
```

Expected: BUILD SUCCEEDED. (CompileWarnings about unused `r.EbtCardStatus` in the row type may appear; that field is still read from SQL and may be useful for logging — leave it on the row type.)

### Task 9: Update DC service tests

**Files:**
- Modify: `test/SEBT.Portal.StatePlugins.DC.Tests/DcSummerEbtCaseServiceTests.cs`

- [ ] **Step 9.1: Run tests to identify what fails**

```bash
dotnet test
```

Expected: FAIL — any test that asserts `EbtCardStatus == "ACTIVE"` or similar string comparisons. Tests that mock `HouseholdMemberRow` with `EbtCardStatus = "ACTIVE"` need to be updated to set `EbtCardIssueDate` for the new derivation.

- [ ] **Step 9.2: Update tests that exercise card status**

For each failing test, change the assertion shape:

Before:
```csharp
Assert.Equal("ACTIVE", result.EbtCardStatus);
```

After:
```csharp
Assert.Equal(CardStatus.Processed, result.EbtCardStatus);
```

For tests that need a "no card" case, ensure `EbtCardIssueDate = null` and assert `Assert.Null(result.EbtCardStatus)`.

- [ ] **Step 9.3: Add a focused test for the new derivation rule**

Add to the test class:

```csharp
[Fact]
public void MapToCase_WhenIssueDatePresent_DerivesProcessedStatus()
{
    var row = new HouseholdMemberRow
    {
        // ...minimal required fields...
        EbtCardIssueDate = new DateOnly(2026, 5, 15)
    };

    var result = DcSummerEbtCaseService.MapToCase(row, includeAddress: false);

    Assert.Equal(CardStatus.Processed, result.EbtCardStatus);
}

[Fact]
public void MapToCase_WhenIssueDateNull_LeavesStatusNull()
{
    var row = new HouseholdMemberRow
    {
        // ...minimal required fields...
        EbtCardIssueDate = null
    };

    var result = DcSummerEbtCaseService.MapToCase(row, includeAddress: false);

    Assert.Null(result.EbtCardStatus);
}
```

(Note: `MapToCase` is currently private. If the test class doesn't already use reflection or `[InternalsVisibleTo]`, you may need to exercise the rule via the public service interface that builds household data. Inspect existing test patterns and follow them.)

- [ ] **Step 9.4: Run tests to confirm green**

```bash
dotnet test
```

Expected: PASS, all DC connector tests green.

### Task 10: Commit Phase 2

- [ ] **Step 10.1: Review diff**

```bash
git -C /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal-dc-connector/.worktrees/DC-256-card-status-mapping diff
```

- [ ] **Step 10.2: Commit**

```bash
git -C /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal-dc-connector/.worktrees/DC-256-card-status-mapping add -A
git -C /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal-dc-connector/.worktrees/DC-256-card-status-mapping commit -m "DC-256: Derive card status from issue date presence

Replace MapCardStatus string mapping with EbtCardIssueDate.HasValue
derivation. Card with issue date renders as 'Processed on [date]' per
DC-95 spec; no issue date means no status. Remove dead Application
card-status references.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Phase 3 — CO-connector adoption

All work below in `/Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/DC-256-card-status-mapping`.

### Task 11: Restore NuGet to pick up new state-connector package

- [ ] **Step 11.1: Restore packages**

```bash
cd /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/DC-256-card-status-mapping
dotnet restore
```

Expected: Restore SUCCEEDED.

- [ ] **Step 11.2: Build — expect failures**

```bash
dotnet build
```

Expected: BUILD FAILED. The `.ToString()` assignment on `EbtCardStatus` and references to removed enum values (`CardStatus.Requested`, `CardStatus.Mailed`, `CardStatus.Deactivated`) break.

### Task 12: Update `MapCardStatus` to the 9-value DC-95 mapping

**Files:**
- Modify: `src/SEBT.Portal.StatePlugins.CO/Cbms/CbmsResponseMapper.cs`

- [ ] **Step 12.1: Replace the `MapCardStatus` method**

Find the existing method (around line 191-208) and replace it with:

```csharp
private static CardStatus MapCardStatus(string? ebtCardSts, ILogger? logger = null)
{
    if (string.IsNullOrEmpty(ebtCardSts)) return CardStatus.Unknown;
    return ebtCardSts.ToUpperInvariant() switch
    {
        "ACTIVE" => CardStatus.Active,
        "LOST" => CardStatus.Lost,
        "STOLEN" => CardStatus.Stolen,
        "DAMAGED" => CardStatus.Damaged,
        "STATUSED BY STATE, NO REISSUE" => CardStatus.DeactivatedByState,
        "DEACTIVATED BY STATE" => CardStatus.DeactivatedByState,
        "NOT ACTIVATED" => CardStatus.NotActivated,
        "FROZEN" => CardStatus.Frozen,
        "UNDELIVERABLE" => CardStatus.Undeliverable,
        _ => LogAndReturnUnknown(ebtCardSts, logger)
    };
}

private static CardStatus LogAndReturnUnknown(string raw, ILogger? logger)
{
    logger?.LogError(
        "CBMS returned unmapped ebtCardSts token {Token}; falling back to CardStatus.Unknown. " +
        "If this token represents a real status, add it to the DC-95 mapping table.",
        raw);
    return CardStatus.Unknown;
}
```

The `LogError` upgrade (from `LogInformation`) lets alerting fire on unmapped tokens.

- [ ] **Step 12.2: Drop the `.ToString()` cast on the assignment**

In `MapToSummerEbtCase` (around line 91), change:

```csharp
EbtCardStatus = MapCardStatus(s.EbtCardSts, logger).ToString(),
```

to:

```csharp
EbtCardStatus = MapCardStatus(s.EbtCardSts, logger),
```

`SummerEbtCase.EbtCardStatus` now expects the enum directly.

- [ ] **Step 12.3: Build to confirm**

```bash
dotnet build
```

Expected: BUILD SUCCEEDED.

### Task 13: Update CO mapper tests

**Files:**
- Modify: `test/SEBT.Portal.StatePlugins.CO.Tests/Cbms/CbmsResponseMapperTests.cs` (or wherever `CbmsResponseMapperTests` lives)

- [ ] **Step 13.1: Run existing tests to identify breakage**

```bash
dotnet test
```

Expected: FAIL — existing tests asserting on string values like `"Active"` need to compare to `CardStatus.Active`. Tests that asserted `MapCardStatus("REQUESTED") == CardStatus.Requested` etc. now fail because `Requested` is gone.

- [ ] **Step 13.2: Update existing tests for the enum-typed Case property**

For each existing test asserting on `EbtCardStatus`:

Before:
```csharp
Assert.Equal("Active", result.EbtCardStatus);
```

After:
```csharp
Assert.Equal(CardStatus.Active, result.EbtCardStatus);
```

Delete or update tests that exercised `REQUESTED`, `MAILED`, or bare `DEACTIVATED` mappings — these strings no longer map to specific enum values and now go to `Unknown`.

- [ ] **Step 13.3: Add a parameterised test for the 9-value mapping**

Add to the test class:

```csharp
[Theory]
[InlineData("ACTIVE", CardStatus.Active)]
[InlineData("LOST", CardStatus.Lost)]
[InlineData("STOLEN", CardStatus.Stolen)]
[InlineData("DAMAGED", CardStatus.Damaged)]
[InlineData("STATUSED BY STATE, NO REISSUE", CardStatus.DeactivatedByState)]
[InlineData("DEACTIVATED BY STATE", CardStatus.DeactivatedByState)]
[InlineData("NOT ACTIVATED", CardStatus.NotActivated)]
[InlineData("FROZEN", CardStatus.Frozen)]
[InlineData("UNDELIVERABLE", CardStatus.Undeliverable)]
[InlineData("active", CardStatus.Active)]                                    // case-insensitive
[InlineData("undeliverable", CardStatus.Undeliverable)]                      // case-insensitive
public void MapCardStatus_DcSpecValues_MapsCorrectly(string raw, CardStatus expected)
{
    var response = BuildResponseWithCardStatus(raw); // existing helper or build inline
    var result = CbmsResponseMapper.MapToHouseholdData(response, "+13035551234", PiiVisibility.Full, logger: null);
    Assert.Equal(expected, result.SummerEbtCases.First().EbtCardStatus);
}
```

If a `BuildResponseWithCardStatus` helper doesn't exist, follow the test fixture patterns already in the file. The mapper is internal — the test must exercise the public `MapToHouseholdData` entry point.

- [ ] **Step 13.4: Add a test for the unmapped-token error log**

```csharp
[Theory]
[InlineData("DAMAGED, AUTO REISSUE")]
[InlineData("RETURNED")]
[InlineData("DEACTIVATED")]
[InlineData("DEACTIVATED, NO REISSUE")]
[InlineData("DEACTIVATED/CANCELLED")]
[InlineData("CANCELED BY PRIMARY NO REISSUE")]
[InlineData("UNAUTHORIZED USE, NO REISSUE")]
[InlineData("COMPROMISED, NO REISSUE")]
[InlineData("SOMETHING NEW THAT CBMS MIGHT EMIT")]
public void MapCardStatus_UnmappedToken_ReturnsUnknownAndLogsError(string raw)
{
    var logger = Substitute.For<ILogger>();
    var response = BuildResponseWithCardStatus(raw);

    var result = CbmsResponseMapper.MapToHouseholdData(response, "+13035551234", PiiVisibility.Full, logger);

    Assert.Equal(CardStatus.Unknown, result.SummerEbtCases.First().EbtCardStatus);
    logger.Received(1).Log(
        LogLevel.Error,
        Arg.Any<EventId>(),
        Arg.Is<object>(o => o.ToString()!.Contains(raw)),
        Arg.Any<Exception>(),
        Arg.Any<Func<object, Exception?, string>>());
}
```

(Adapt the NSubstitute `logger.Received(...)` shape to whatever pattern the existing test file uses. If the existing tests don't assert on logger calls, add an `using Microsoft.Extensions.Logging;` and an `using NSubstitute;` and follow the Microsoft.Extensions.Logging logging-mock pattern.)

- [ ] **Step 13.5: Run tests to confirm green**

```bash
dotnet test
```

Expected: PASS, all CO connector tests green.

### Task 14: Commit Phase 3

- [ ] **Step 14.1: Commit**

```bash
git -C /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/DC-256-card-status-mapping add -A
git -C /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/DC-256-card-status-mapping commit -m "DC-256: Map CBMS card statuses per DC-95 spec; alert on unknowns

Cover the 9 raw values listed in DC-95 (Active, Lost, Stolen, Damaged,
Statused/Deactivated by state, Not activated, Frozen, Undeliverable).
All other CBMS values fall through to Unknown and emit LogError so
alerting can fire when CBMS returns a status outside the mapped set.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Phase 4 — Portal backend adoption

All work below in `/Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal/.worktrees/DC-256-card-status-mapping`.

### Task 15: Restore portal NuGet packages

- [ ] **Step 15.1: Restore**

```bash
cd /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal/.worktrees/DC-256-card-status-mapping
dotnet restore
```

Expected: Restore SUCCEEDED. The portal references the state-connector package via local NuGet store.

- [ ] **Step 15.2: Rebuild DC and CO connectors so their DLLs are copied to portal plugin folders**

```bash
cd /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal-dc-connector/.worktrees/DC-256-card-status-mapping
dotnet build

cd /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/DC-256-card-status-mapping
dotnet build
```

The post-build target on each connector copies DLLs into `sebt-self-service-portal/src/SEBT.Portal.Api/plugins-{state}/`.

⚠️ **Warning:** the connector post-build copies into the **canonical portal repo's** `plugins-*` directories (per the relative path in the connector csprojs), NOT the worktree's. For testing you may need to manually copy DLLs into the worktree, OR temporarily set the `PluginDestDir` env var on the connector build to point at the worktree path. Either approach works — use whatever the team currently does for worktree workflows.

### Task 16: Mirror enum changes in `Core.CardStatus`

**Files:**
- Modify: `src/SEBT.Portal.Core/Models/Household/CardStatus.cs`

- [ ] **Step 16.1: Replace the file contents**

```csharp
using System.Text.Json.Serialization;

namespace SEBT.Portal.Core.Models.Household;

/// <summary>
/// Represents the status of a benefit card. Mirrors the Interface enum
/// member-for-member; parity is enforced by EnumParityTests.
/// Serialized as the member name over the API per JsonStringEnumConverter.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CardStatus
{
    Active = 0,
    Damaged = 1,
    DeactivatedByState = 2,
    Frozen = 3,
    Lost = 4,
    NotActivated = 5,
    Processed = 6,
    Stolen = 7,
    Undeliverable = 8,
    Unknown = 9
}
```

- [ ] **Step 16.2: Build Core**

```bash
dotnet build src/SEBT.Portal.Core/SEBT.Portal.Core.csproj
```

Expected: BUILD SUCCEEDED.

### Task 17: Remove dead card fields from `Core.Application`

**Files:**
- Modify: `src/SEBT.Portal.Core/Models/Household/Application.cs`

- [ ] **Step 17.1: Delete properties**

Remove (currently lines 47-68):
- `public CardStatus CardStatus { get; set; }`
- `public DateTime? CardRequestedAt { get; set; }`
- `public DateTime? CardMailedAt { get; set; }`
- `public DateTime? CardActivatedAt { get; set; }`
- `public DateTime? CardDeactivatedAt { get; set; }`

Plus their XML doc comments.

### Task 18: Change `Core.SummerEbtCase.EbtCardStatus` type

**Files:**
- Modify: `src/SEBT.Portal.Core/Models/Household/SummerEbtCase.cs`

- [ ] **Step 18.1: Update the property**

Find:
```csharp
public string? EbtCardStatus { get; set; }
```

Replace with:
```csharp
public CardStatus? EbtCardStatus { get; set; }
```

- [ ] **Step 18.2: Delete the computed `CardStatus` getter**

Remove the property at lines 107-115:
```csharp
public CardStatus CardStatus =>
    Enum.TryParse<CardStatus>(EbtCardStatus, ignoreCase: true, out var s)
        ? s
        : CardStatus.Unknown;
```

This computed getter was a workaround for the string-typed source; no longer needed.

- [ ] **Step 18.3: Build Core**

```bash
dotnet build src/SEBT.Portal.Core/SEBT.Portal.Core.csproj
```

Expected: BUILD SUCCEEDED. (Errors elsewhere are expected for now — they'll be fixed in subsequent tasks.)

### Task 19: Update `PluginHouseholdDataMapper`

**Files:**
- Modify: `src/SEBT.Portal.Infrastructure/Repositories/PluginHouseholdDataMapper.cs`

- [ ] **Step 19.1: Add the `ConvertEnum<T>` helper**

After the existing `GetProp<T>` helper (around line 172), add:

```csharp
private static T? ConvertEnum<T>(object? value) where T : struct, Enum
{
    if (value == null) return null;
    return Enum.TryParse<T>(value.ToString(), ignoreCase: true, out var result)
        ? result
        : null;
}
```

- [ ] **Step 19.2: Remove Application card-field reflection lines**

In `ToCoreApplication` (around lines 125-145), delete:
- `CardStatus = (CardStatus)(GetProp(t, source, nameof(Application.CardStatus)) ?? (int)CardStatus.Requested),`
- `CardRequestedAt = GetProp<DateTime?>(t, source, nameof(Application.CardRequestedAt)),`
- `CardMailedAt = GetProp<DateTime?>(t, source, nameof(Application.CardMailedAt)),`
- `CardActivatedAt = GetProp<DateTime?>(t, source, nameof(Application.CardActivatedAt)),`
- `CardDeactivatedAt = GetProp<DateTime?>(t, source, nameof(Application.CardDeactivatedAt)),`

- [ ] **Step 19.3: Update `ToCoreSummerEbtCase` to convert the enum**

In `ToCoreSummerEbtCase` (around line 92), find:

```csharp
EbtCardStatus = GetProp<string>(t, source, "EbtCardStatus"),
```

Replace with:

```csharp
EbtCardStatus = ConvertEnum<CardStatus>(GetProp(t, source, "EbtCardStatus")),
```

(Note the change from `GetProp<string>(...)` to `GetProp(...)` — the untyped overload returns `object?`, which `ConvertEnum<T>` then resolves by name.)

- [ ] **Step 19.4: Build Infrastructure**

```bash
dotnet build src/SEBT.Portal.Infrastructure/SEBT.Portal.Infrastructure.csproj
```

Expected: BUILD SUCCEEDED.

### Task 20: Strip Application card-field assignments from `MockHouseholdRepository`

**Files:**
- Modify: `src/SEBT.Portal.Infrastructure/Repositories/MockHouseholdRepository.cs`

- [ ] **Step 20.1: Remove all `app.CardX = ...` assignments**

Remove every line matching one of these patterns:
- `app.CardStatus = ...;`
- `app.CardRequestedAt = ...;`
- `app.CardMailedAt = ...;`
- `app.CardActivatedAt = ...;`
- `app.CardDeactivatedAt = ...;`
- Inline initializers in `new Application { CardStatus = ..., CardRequestedAt = ..., ... }` — drop those property assignments inside the object initializer.

Affected lines per the safety-check grep: ~lines 430, 694-696, 709, 743-744, 784-785, 867-869, 913-915, 959-961, 1005-1007, 1048-1049, 1248-1251.

- [ ] **Step 20.2: Build to confirm**

```bash
dotnet build src/SEBT.Portal.Infrastructure/SEBT.Portal.Infrastructure.csproj
```

Expected: BUILD SUCCEEDED.

### Task 21: Update API response models

**Files:**
- Modify: `src/SEBT.Portal.Api/Models/Household/ApplicationResponse.cs`
- Modify: `src/SEBT.Portal.Api/Models/Household/SummerEbtCaseResponse.cs`
- Modify: `src/SEBT.Portal.Api/Models/Household/HouseholdDataResponseMapper.cs`

- [ ] **Step 21.1: `ApplicationResponse` — remove 5 properties**

Delete from `ApplicationResponse.cs`:
- `public CardStatus CardStatus { get; init; }`
- `public DateTime? CardRequestedAt { get; init; }`
- `public DateTime? CardMailedAt { get; init; }`
- `public DateTime? CardActivatedAt { get; init; }`
- `public DateTime? CardDeactivatedAt { get; init; }`

Plus any unused `using` directives for `Core::SEBT.Portal.Core.Models.Household.CardStatus`.

- [ ] **Step 21.2: `SummerEbtCaseResponse` — change EbtCardStatus type**

Find:
```csharp
public string? EbtCardStatus { get; init; }
```

Replace with:
```csharp
public Core::SEBT.Portal.Core.Models.Household.CardStatus? EbtCardStatus { get; init; }
```

If the file already aliases `using CardStatus = Core::SEBT.Portal.Core.Models.Household.CardStatus;` at the top, you can use `CardStatus?` directly.

- [ ] **Step 21.3: `HouseholdDataResponseMapper` — drop deleted Application mappings**

In `HouseholdDataResponseMapper.cs`, find the `Application → ApplicationResponse` mapping (around lines 85-95) and delete:
- `CardStatus = domain.CardStatus,`
- `CardRequestedAt = domain.CardRequestedAt,`
- `CardMailedAt = domain.CardMailedAt,`
- `CardActivatedAt = domain.CardActivatedAt,`
- `CardDeactivatedAt = domain.CardDeactivatedAt,`

For the Case mapping at line 59, leave `CardRequestedAt = domain.CardRequestedAt,` alone — that's the cooldown field on `SummerEbtCase`, not the (deleted) Application timeline field.

The Case-side `EbtCardStatus = domain.EbtCardStatus,` requires no change — both sides are now `CardStatus?`.

- [ ] **Step 21.4: Build the API project**

```bash
dotnet build src/SEBT.Portal.Api/SEBT.Portal.Api.csproj
```

Expected: BUILD SUCCEEDED.

### Task 22: Update `HouseholdFactory` test utilities

**Files:**
- Modify: `src/SEBT.Portal.TestUtilities/Helpers/HouseholdFactory.cs`

- [ ] **Step 22.1: Remove all `application.CardX = ...` setters**

Affected lines per safety-check grep: ~171-173, 182, 191-194, 201, 210, 217-218.

For each, simply delete the assignment. If a method's only purpose was to set these fields, delete the method. If a method also does other useful work, keep the rest and just drop the card-field lines.

If any helper signatures included parameters like `DateTime requestedDate` purely to feed `CardRequestedAt`, remove those parameters and update callers.

- [ ] **Step 22.2: Build TestUtilities**

```bash
dotnet build src/SEBT.Portal.TestUtilities/SEBT.Portal.TestUtilities.csproj
```

Expected: BUILD SUCCEEDED.

### Task 23: Add enum parity tests

**Files:**
- Create: `test/SEBT.Portal.Tests/Unit/EnumParity/EnumParityTests.cs`

- [ ] **Step 23.1: Create the test file**

```csharp
using SEBT.Portal.Tests.Common;
using Xunit;

using CoreCardStatus = SEBT.Portal.Core.Models.Household.CardStatus;
using CoreApplicationStatus = SEBT.Portal.Core.Models.Household.ApplicationStatus;
using CoreIssuanceType = SEBT.Portal.Core.Models.Household.IssuanceType;
using CoreBenefitIssuanceType = SEBT.Portal.Core.Models.Household.BenefitIssuanceType;
using InterfaceCardStatus = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CardStatus;
using InterfaceApplicationStatus = SEBT.Portal.StatesPlugins.Interfaces.Models.ApplicationStatus;
using InterfaceIssuanceType = SEBT.Portal.StatesPlugins.Interfaces.Models.IssuanceType;
using InterfaceBenefitIssuanceType = SEBT.Portal.StatesPlugins.Interfaces.Models.BenefitIssuanceType;

namespace SEBT.Portal.Tests.Unit.EnumParity;

/// <summary>
/// Asserts that enums declared in both the StatesPlugins.Interfaces and Core layers
/// have identical members. PluginHouseholdDataMapper translates between the two
/// layers by member name, so any drift causes silent data loss.
/// </summary>
public class EnumParityTests
{
    [Fact]
    public void CardStatus_InterfaceAndCore_HaveIdenticalMembers()
    {
        AssertEnumNamesEqual<InterfaceCardStatus, CoreCardStatus>();
    }

    [Fact]
    public void ApplicationStatus_InterfaceAndCore_HaveIdenticalMembers()
    {
        AssertEnumNamesEqual<InterfaceApplicationStatus, CoreApplicationStatus>();
    }

    [Fact]
    public void IssuanceType_InterfaceAndCore_HaveIdenticalMembers()
    {
        AssertEnumNamesEqual<InterfaceIssuanceType, CoreIssuanceType>();
    }

    [Fact]
    public void BenefitIssuanceType_InterfaceAndCore_HaveIdenticalMembers()
    {
        AssertEnumNamesEqual<InterfaceBenefitIssuanceType, CoreBenefitIssuanceType>();
    }

    private static void AssertEnumNamesEqual<TInterface, TCore>()
        where TInterface : struct, Enum
        where TCore : struct, Enum
    {
        var interfaceNames = Enum.GetNames<TInterface>().OrderBy(n => n).ToArray();
        var coreNames = Enum.GetNames<TCore>().OrderBy(n => n).ToArray();
        Assert.Equal(interfaceNames, coreNames);
    }
}
```

If the actual namespaces for `ApplicationStatus`, `IssuanceType`, `BenefitIssuanceType` in either layer differ from the assumptions above, adjust the `using` aliases.

- [ ] **Step 23.2: Run the new tests**

```bash
dotnet test test/SEBT.Portal.Tests/SEBT.Portal.Tests.csproj --filter "FullyQualifiedName~EnumParity"
```

Expected: PASS — all 4 enums match.

### Task 24: Add `ConvertEnum<T>` tests

**Files:**
- Create or modify: `test/SEBT.Portal.Tests/Unit/Repositories/PluginHouseholdDataMapperTests.cs`

- [ ] **Step 24.1: Determine if a test file already exists**

```bash
ls test/SEBT.Portal.Tests/Unit/Repositories/PluginHouseholdDataMapperTests.cs 2>/dev/null
```

If it exists, append to it. If not, create it with the namespace pattern matching its peers in `Unit/Repositories`.

- [ ] **Step 24.2: Add tests for `ConvertEnum<T>`**

Since `ConvertEnum<T>` is private, exercise it via `PluginHouseholdDataMapper.ToCore(...)` with synthetic source objects:

```csharp
[Fact]
public void ToCore_WhenSummerEbtCaseHasInterfaceCardStatusActive_ReturnsCoreActive()
{
    var sourceCase = new InterfaceSummerEbtCase
    {
        ChildFirstName = "X",
        ChildLastName = "Y",
        ChildDateOfBirth = new DateOnly(2010, 1, 1),
        HouseholdType = "OSSE",
        EligibilityType = "NSLP",
        EbtCardStatus = InterfaceCardStatus.Active
    };
    var source = new InterfaceHouseholdData
    {
        Email = "test@example.com",
        SummerEbtCases = new List<InterfaceSummerEbtCase> { sourceCase }
    };

    var core = PluginHouseholdDataMapper.ToCore(source);

    Assert.Equal(CoreCardStatus.Active, core!.SummerEbtCases.Single().EbtCardStatus);
}

[Fact]
public void ToCore_WhenSummerEbtCaseEbtCardStatusNull_ReturnsNull()
{
    var sourceCase = new InterfaceSummerEbtCase
    {
        ChildFirstName = "X",
        ChildLastName = "Y",
        ChildDateOfBirth = new DateOnly(2010, 1, 1),
        HouseholdType = "OSSE",
        EligibilityType = "NSLP",
        EbtCardStatus = null
    };
    var source = new InterfaceHouseholdData
    {
        Email = "test@example.com",
        SummerEbtCases = new List<InterfaceSummerEbtCase> { sourceCase }
    };

    var core = PluginHouseholdDataMapper.ToCore(source);

    Assert.Null(core!.SummerEbtCases.Single().EbtCardStatus);
}
```

Adapt aliases (`using InterfaceSummerEbtCase = SEBT.Portal.StatesPlugins.Interfaces.Data.Cases.SummerEbtCase;` etc.) to whatever pattern this test project uses.

- [ ] **Step 24.3: Run the tests**

```bash
dotnet test test/SEBT.Portal.Tests/SEBT.Portal.Tests.csproj --filter "FullyQualifiedName~PluginHouseholdDataMapper"
```

Expected: PASS.

### Task 25: Update existing portal backend tests

**Files:**
- Modify: `test/SEBT.Portal.Tests/Unit/Models/HouseholdDataResponseMapperTests.cs`
- Modify: `test/SEBT.Portal.Tests/Unit/Helpers/HouseholdFactoryTests.cs`
- Modify: any other tests that reference the deleted fields

- [ ] **Step 25.1: Run full backend unit tests to find breakages**

```bash
pnpm api:test:unit
```

Expected: FAIL — list of compile and assertion failures referencing removed Application fields and the changed Case `EbtCardStatus` type.

- [ ] **Step 25.2: Update `HouseholdDataResponseMapperTests`**

For each test setting up an `Application` with card fields:
- Remove the lines `CardRequestedAt = ..., CardMailedAt = ..., CardActivatedAt = ..., CardDeactivatedAt = ..., CardStatus = ...`
- Remove corresponding `Assert.Equal(..., app.CardRequestedAt)` etc.

- [ ] **Step 25.3: Update `HouseholdFactoryTests`**

The `CreateSummerEbtCase_ShouldNotPopulateCardRequestedAtOrCardLastFour` test (line 368) exists because the factory historically didn't set these on Case. After our change, `SummerEbtCase.CardRequestedAt` is still a Case field (cooldown), so this test should still pass — but verify the assertion logic is for the cooldown field, not a deleted field.

Update any `application.Card*At` assertions to reflect the deletions.

- [ ] **Step 25.4: Update other failing tests**

For any test referencing `app.CardStatus`, `app.CardRequestedAt`, etc., delete those references. For any test asserting `case.EbtCardStatus == "Active"` (string), change to `case.EbtCardStatus == CardStatus.Active` (enum).

- [ ] **Step 25.5: Run unit tests until green**

```bash
pnpm api:test:unit
```

Expected: PASS, all 1049+ unit tests green.

### Task 26: Commit Phase 4

- [ ] **Step 26.1: Review diff**

```bash
git -C /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal/.worktrees/DC-256-card-status-mapping diff --stat
```

- [ ] **Step 26.2: Commit**

```bash
git -C /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal/.worktrees/DC-256-card-status-mapping add -A
git -C /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal/.worktrees/DC-256-card-status-mapping commit -m "DC-256: Adopt new state-connector contracts in portal backend

Mirror enum changes in Core. Remove dead Application card-lifecycle
fields and their reflective copy in PluginHouseholdDataMapper. Replace
the int-cast enum mapping for CardStatus with name-based ConvertEnum<T>
parsing — robust to future enum reshaping. Update API response models
and mappers. Add EnumParityTests to catch Interface/Core drift.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

Pre-commit hook will run `dotnet build` and `dotnet test` — expect green. If it fails, fix the failing test and amend with a NEW commit (not `--amend`).

---

## Phase 5 — Portal frontend changes

All work below in `/Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal/.worktrees/DC-256-card-status-mapping`.

### Task 27: Update Zod schemas

**Files:**
- Modify: `src/SEBT.Portal.Web/src/features/household/api/schema.ts`

- [ ] **Step 27.1: Inspect the existing schema**

```bash
sed -n '1,260p' src/SEBT.Portal.Web/src/features/household/api/schema.ts
```

Identify:
- `CARD_STATUS_MAP` (the `Record<number, string>` lookup)
- `CardStatusSchema` (current shape: parsed from int via the map)
- `ApplicationSchema` containing 5 card-related fields
- `SummerEbtCaseSchema` containing `cardRequestedAt`, `cardMailedAt`, `cardActivatedAt`, `cardDeactivatedAt`, `ebtCardStatus`

- [ ] **Step 27.2: Replace `CardStatusSchema` and supporting types**

Find:
```ts
const CARD_STATUS_MAP: Record<number, string> = {
  0: 'Requested',
  1: 'Mailed',
  // ...
}
```

Replace the schema definition with:

```ts
export const CARD_STATUSES = [
  'Active',
  'Damaged',
  'DeactivatedByState',
  'Frozen',
  'Lost',
  'NotActivated',
  'Processed',
  'Stolen',
  'Undeliverable',
  'Unknown'
] as const

export type CardStatus = (typeof CARD_STATUSES)[number]

export const CardStatusSchema = z.enum(CARD_STATUSES).nullable().optional()
```

Delete the `CARD_STATUS_MAP` lookup and any helper that converted int → string.

If a `UiCardStatus` type or `toUiCardStatus()` helper exists nearby, ensure they reference `CardStatus` directly (no longer needing int translation).

- [ ] **Step 27.3: Update `SummerEbtCaseSchema`**

Find the schema; remove these three fields (keep `cardRequestedAt`):
- `cardMailedAt: z.string().nullable().optional(),`
- `cardActivatedAt: z.string().nullable().optional(),`
- `cardDeactivatedAt: z.string().nullable().optional(),`

Change `ebtCardStatus`:
- From: `ebtCardStatus: z.string().nullable().optional(),`
- To: `ebtCardStatus: CardStatusSchema,`

- [ ] **Step 27.4: Update `ApplicationSchema`**

Remove all 5 card fields:
- `cardStatus: ...`
- `cardRequestedAt: ...`
- `cardMailedAt: ...`
- `cardActivatedAt: ...`
- `cardDeactivatedAt: ...`

- [ ] **Step 27.5: Update any `toUiCardStatus()` helper**

If `toUiCardStatus()` references `Requested`, `Mailed`, or `Deactivated` enum values, remove those branches. The function should now map only the 10 remaining values to UI buckets.

- [ ] **Step 27.6: Run frontend type-check**

```bash
cd src/SEBT.Portal.Web
pnpm typecheck
```

Expected: errors in dependent components — they will be fixed in subsequent tasks. List the errors so the engineer knows what to address.

### Task 28: Update `ChildCard` to gate the timeline by cooldown

**Files:**
- Modify: `src/SEBT.Portal.Web/src/features/household/components/ChildCard/ChildCard.tsx`

- [ ] **Step 28.1: Update the imports and helper**

Remove the `hasCardLifecycleTimeline` helper (lines 14-22). Ensure `isWithinCooldownPeriod` is imported from `@/features/cards/utils/cooldown` (it already is at line 7).

- [ ] **Step 28.2: Drop dead destructuring**

In the destructuring block (lines 76-87), remove `cardMailedAt` and `cardDeactivatedAt`. Keep `cardRequestedAt` (still used by `getReplacementLink`).

- [ ] **Step 28.3: Replace the timeline-rendering conditional**

Find the JSX block (around lines 147-157):

```tsx
{summerEbtCase.allowCardReplacement &&
  (hasCardLifecycleTimeline(summerEbtCase) ? (
    <CardStatusTimeline
      cardStatus={ebtCardStatus}
      cardRequestedAt={cardRequestedAt}
      cardMailedAt={cardMailedAt}
      cardDeactivatedAt={cardDeactivatedAt}
    />
  ) : (
    <CardStatusDisplay cardStatus={ebtCardStatus} />
  ))}
```

Replace with:

```tsx
{summerEbtCase.allowCardReplacement &&
  (isWithinCooldownPeriod(cardRequestedAt) ? (
    <CardStatusTimeline cardRequestedAt={cardRequestedAt} />
  ) : (
    <CardStatusDisplay cardStatus={ebtCardStatus} />
  ))}
```

- [ ] **Step 28.4: Run typecheck**

```bash
pnpm typecheck
```

Expected: errors point to `CardStatusTimeline` (props mismatch — fixed in Task 29).

### Task 29: Simplify `CardStatusTimeline`

**Files:**
- Modify: `src/SEBT.Portal.Web/src/features/household/components/CardStatusTimeline/CardStatusTimeline.tsx`

- [ ] **Step 29.1: Replace the file contents**

```tsx
'use client'

import Image from 'next/image'
import { useTranslation } from 'react-i18next'

import { interpolateDate } from '../../api'

interface CardStatusTimelineProps {
  cardRequestedAt: string | null | undefined
}

const REQUESTED_LABEL_FALLBACK = 'Requested on [MM/DD/YYYY]'
const REQUESTED_MESSAGE_FALLBACK =
  "We've requested a new card that will arrive in the mail within 2–3 weeks. Check back here to see when the card has been mailed."

/**
 * Renders a single-state notice that a card replacement is in flight.
 * Shown only while the user is within the cooldown window after submitting
 * a replacement request — the gating decision lives in ChildCard so this
 * component does not need to know about cooldown duration.
 */
export function CardStatusTimeline({ cardRequestedAt }: CardStatusTimelineProps) {
  const { t, i18n } = useTranslation('dashboard')

  const rawLabel = t('cardTableStatusRequested') || REQUESTED_LABEL_FALLBACK
  const label = interpolateDate(rawLabel, cardRequestedAt ?? null, i18n.language)
  const message =
    t('cardTableStatusMessageRequested1', { defaultValue: '' }) || REQUESTED_MESSAGE_FALLBACK

  return (
    <div className="margin-top-2">
      <dt className="text-bold">{t('cardTableHeadingCardStatus')}</dt>
      <dd className="margin-left-0 margin-top-1">
        <div className="display-flex flex-align-center padding-1 border-left-1 border-info bg-info-lighter">
          <Image
            src="/icons/credit_card_clock.svg"
            width={21}
            height={19}
            className="usa-icon margin-right-1 flex-shrink-0"
            alt=""
            aria-hidden="true"
          />
          <span>{label}</span>
        </div>
        <p className="margin-top-2 margin-bottom-0">{message}</p>
      </dd>
    </div>
  )
}
```

Note the **state-neutral fallback** at `REQUESTED_MESSAGE_FALLBACK` — drops the "DC SUN Bucks card" wording.

- [ ] **Step 29.2: Run typecheck**

```bash
pnpm typecheck
```

Expected: PASS for this component. (Tests still need updating — Task 30.)

### Task 30: Update `CardStatusTimeline` tests

**Files:**
- Modify: `src/SEBT.Portal.Web/src/features/household/components/CardStatusTimeline/CardStatusTimeline.test.tsx`

- [ ] **Step 30.1: Replace the file contents**

```tsx
import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

import { CardStatusTimeline } from './CardStatusTimeline'

describe('CardStatusTimeline', () => {
  it('renders the card-status heading', () => {
    render(<CardStatusTimeline cardRequestedAt="2026-01-01T00:00:00Z" />)
    expect(screen.getByText(/card status/i)).toBeInTheDocument()
  })

  it('shows the requested-on label with interpolated date', () => {
    render(<CardStatusTimeline cardRequestedAt="2026-01-15T00:00:00Z" />)
    // i18n fallback: "Requested on [MM/DD/YYYY]" → interpolated to "Requested on 01/15/2026"
    expect(screen.getByText(/requested on 01\/15\/2026/i)).toBeInTheDocument()
  })

  it('shows the cooldown reassurance message', () => {
    render(<CardStatusTimeline cardRequestedAt="2026-01-01T00:00:00Z" />)
    expect(screen.getByText(/arrive in the mail within 2–3 weeks/i)).toBeInTheDocument()
  })

  it('uses state-neutral wording in the message fallback', () => {
    render(<CardStatusTimeline cardRequestedAt="2026-01-01T00:00:00Z" />)
    // Regression check: ensure the DC-flavored wording is gone
    expect(screen.queryByText(/DC SUN Bucks/i)).not.toBeInTheDocument()
  })

  it('renders without a date when cardRequestedAt is null', () => {
    render(<CardStatusTimeline cardRequestedAt={null} />)
    expect(screen.getByText(/card status/i)).toBeInTheDocument()
    // interpolateDate with null returns the label unchanged with placeholder
    expect(screen.getByText(/\[MM\/DD\/YYYY\]/i)).toBeInTheDocument()
  })
})
```

- [ ] **Step 30.2: Run the tests**

```bash
cd src/SEBT.Portal.Web
pnpm test CardStatusTimeline
```

Expected: PASS.

### Task 31: Add label fallback to `CardStatusDisplay`

**Files:**
- Modify: `src/SEBT.Portal.Web/src/features/household/components/CardStatusDisplay/CardStatusDisplay.tsx`

- [ ] **Step 31.1: Add `LABEL_FALLBACK`**

After the `STATUS_CONFIG` constant (around line 22), add:

```tsx
// Fallback English copy used when the generated locale string is missing or
// empty. Mirrors the existing DESCRIPTION_FALLBACK pattern. Keep these in
// sync with the source spreadsheet — they exist to render reasonable copy
// when the content team hasn't yet populated the per-state CSV column.
const LABEL_FALLBACK: Record<UiCardStatus, string> = {
  Active: 'Active',
  Processed: 'Processed on [MM/DD/YYYY]',
  Inactive: 'Inactive',
  Frozen: 'Frozen',
  Undeliverable: 'Undeliverable'
}
```

- [ ] **Step 31.2: Wire the fallback into the label lookup**

Around line 76, change:

```tsx
const statusLabel = t(labelKey)
```

to:

```tsx
const statusLabel = t(labelKey, { defaultValue: '' }) || LABEL_FALLBACK[uiStatus]
```

- [ ] **Step 31.3: Remove `Deactivated` entries**

In `DESCRIPTION_KEY` (line 24-35), remove the `Deactivated:` line. In `DESCRIPTION_FALLBACK` (line 43-61), remove the `Deactivated:` line.

- [ ] **Step 31.4: Add `Processed` to `DESCRIPTION_FALLBACK` if missing**

Verify that `DESCRIPTION_FALLBACK` already has a `Processed:` entry (it should — line 46). If not, add:

```tsx
Processed: 'Your card has been processed and is on its way.',
```

- [ ] **Step 31.5: Update the early-return for `Requested` and `Mailed`**

Currently lines 66-72:
```tsx
if (
  !cardStatus ||
  cardStatus === 'Unknown' ||
  cardStatus === 'Requested' ||
  cardStatus === 'Mailed'
)
  return null
```

After our enum changes, `Requested` and `Mailed` are no longer in the `CardStatus` type — they'd cause a type error. Replace with:

```tsx
if (!cardStatus || cardStatus === 'Unknown') return null
```

- [ ] **Step 31.6: Run typecheck and tests**

```bash
pnpm typecheck
pnpm test CardStatusDisplay
```

Expected: PASS. If `CardStatusDisplay.test.tsx` exists and exercised `Requested`/`Mailed`/`Deactivated` early-return cases, those test cases should be removed; verify and update.

### Task 32: Update mocks and fixtures

**Files:**
- Modify: `src/SEBT.Portal.Web/src/mocks/handlers.ts`
- Modify: `src/SEBT.Portal.Web/e2e/fixtures/household-data.ts`
- Modify: `src/SEBT.Portal.Web/src/features/household/testing/fixtures.ts`
- Modify: any component test file referencing the removed fields (per the safety-check grep)

- [ ] **Step 32.1: `handlers.ts` — drop deleted Application fields and update Case ebtCardStatus**

In the MSW handler that returns mocked household data:
- Remove `cardRequestedAt`, `cardMailedAt`, `cardActivatedAt`, `cardDeactivatedAt`, `cardStatus` from any Application object.
- For Case-level `cardMailedAt`, `cardActivatedAt`, `cardDeactivatedAt` (lines ~90-93): remove.
- Change `ebtCardStatus: 'Active'` (already a string) to remain a string but ensure the value matches one of the 10 enum names. If existing fixtures used `"Requested"` or `"Mailed"`, change to `"Active"` or `"Processed"`.

- [ ] **Step 32.2: `e2e/fixtures/household-data.ts` — same treatment**

For the `makeApplication` and `makeSummerEbtCase` factories (lines 34-37, 103, 118-121, etc.):
- Drop `cardMailedAt`, `cardActivatedAt`, `cardDeactivatedAt` from the type and factory output.
- Keep `cardRequestedAt` on Case (cooldown).
- For any `applications: [makeApplication({ cardRequestedAt: ... })]` calls in e2e specs, the option becomes irrelevant — `Application` no longer has `cardRequestedAt`. Change those tests to set up the cooldown via `makeSummerEbtCase({ cardRequestedAt: ... })` instead.

- [ ] **Step 32.3: Component test files**

For each file in this list, drop references to the deleted fields and update fixtures:
- `src/features/household/components/ApplicationsSection/ApplicationsSection.test.tsx`
- `src/features/household/components/HouseholdSummary/HouseholdSummary.test.tsx`
- `src/features/household/components/UserProfileCard/UserProfileCard.test.tsx`
- `src/features/household/components/ChildCard/ChildCard.test.tsx`
- `src/features/household/api/useHouseholdData.test.tsx`

For each, find lines like `cardMailedAt: '2026-01-05T00:00:00Z'` and remove them. If a test was specifically checking the timeline rendering of those fields, update the test to reflect the new simplified component behavior (or remove the test if it's no longer meaningful).

- [ ] **Step 32.4: e2e card-replacement specs**

Files: `e2e/card-replacement/address-flow.spec.ts`, `e2e/card-replacement/standalone-flow.spec.ts`, `e2e/card-replacement/child-card.spec.ts`.

These tests use `makeApplication({ cardRequestedAt: OLD_CARD_DATE })` to simulate prior card requests. Since `Application.cardRequestedAt` is gone, the cooldown trigger now comes only from `makeSummerEbtCase({ cardRequestedAt })`. Update fixture setup accordingly.

- [ ] **Step 32.5: Run frontend tests**

```bash
cd src/SEBT.Portal.Web
pnpm test
```

Expected: PASS, all Vitest tests green.

- [ ] **Step 32.6: Run lint and typecheck**

```bash
pnpm typecheck
pnpm lint
```

Expected: PASS, no type errors, no new lint errors.

### Task 33: Refresh `docs/missing-locale-strings.md`

**Files:**
- Modify: `docs/missing-locale-strings.md`

- [ ] **Step 33.1: Update the doc**

Change the date at the top to today (`2026-05-04`).

In the section "DC — Empty Keys" → "Not referenced by any component", **remove** these two rows (they ARE now referenced):
- `cardTableStatusMessageRequested1`
- `cardTableStatusMessageRequested2` — actually, this one stays unreferenced post-change

Actually: only `cardTableStatusMessageRequested1` is now used; `cardTableStatusMessageRequested2` remains unused. Move `Requested1` from the "Not referenced" table to a new entry in the "Used by code" table:

```markdown
| `cardTableStatusMessageRequested1` | "We've requested a new card... within 2–3 weeks." | Now used by simplified CardStatusTimeline; both DC and CO English Current columns empty in CSV |
```

Add a new section near "Keys Missing from CSV":

```markdown
### Both states — DC-256 introduces `Processed` UI bucket

| Key | Suggested English | Notes |
| --- | --- | --- |
| `cardTableStatusProcessed` | "Processed on [MM/DD/YYYY]" | Label for DC's primary card status (data shows when issue date present). Code falls back to `LABEL_FALLBACK` until CSV row is added. |
| `cardTableStatusMessageProcessed` | "Your card has been processed and is on its way." | Description. Code falls back to `DESCRIPTION_FALLBACK`. |

Note: as of DC-256, `cardTableStatusMailed`, `cardTableStatusMessageMailed`, and `cardTableStatusIssued` are no longer referenced by any component — they can be retired from the source spreadsheet.
```

Update the summary table at the bottom of the doc to reflect the new state.

- [ ] **Step 33.2: Verify the doc renders**

```bash
sed -n '1,50p' docs/missing-locale-strings.md
```

Expected: header and DC section open correctly. (No automated check; visual review.)

### Task 34: Final pre-commit verification

- [ ] **Step 34.1: Run full backend unit tests**

```bash
pnpm api:test:unit
```

Expected: PASS.

- [ ] **Step 34.2: Run full frontend tests**

```bash
cd src/SEBT.Portal.Web
pnpm test
pnpm typecheck
pnpm lint
```

Expected: PASS.

- [ ] **Step 34.3: Confirm portal builds clean**

```bash
cd /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal/.worktrees/DC-256-card-status-mapping
pnpm api:build
```

Expected: BUILD SUCCEEDED.

### Task 35: Commit Phase 5

- [ ] **Step 35.1: Review diff stat**

```bash
git -C /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal/.worktrees/DC-256-card-status-mapping status
git -C /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal/.worktrees/DC-256-card-status-mapping diff --stat
```

- [ ] **Step 35.2: Commit**

```bash
git -C /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal/.worktrees/DC-256-card-status-mapping add -A
git -C /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal/.worktrees/DC-256-card-status-mapping commit -m "DC-256: Simplify card-status UI; align frontend with enum-typed API

Replace the unreachable multi-step CardStatusTimeline with a single-purpose
cooldown notice gated by isWithinCooldownPeriod. Drop dead Application
timeline fields from Zod schema and fixtures. Update CardStatusDisplay
with a LABEL_FALLBACK matching the existing DESCRIPTION_FALLBACK pattern,
removing the literal-key-name render bug for the new Processed status.
Refresh docs/missing-locale-strings.md.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Phase 6 — Cross-repo integration verification

### Task 36: End-to-end smoke test in mock data mode

**Files:** None (verification)

- [ ] **Step 36.1: Build all four repos in order**

```bash
cd /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal-state-connector/.worktrees/DC-256-card-status-mapping
dotnet build

cd /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal-dc-connector/.worktrees/DC-256-card-status-mapping
dotnet build

cd /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal-co-connector/.worktrees/DC-256-card-status-mapping
dotnet build

cd /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal/.worktrees/DC-256-card-status-mapping
pnpm api:build
```

Expected: each build succeeds.

- [ ] **Step 36.2: Run portal in dev mode for DC**

```bash
cd /Users/jblair@codeforamerica.org/Projects/SEBT/sebt-self-service-portal/.worktrees/DC-256-card-status-mapping
pnpm dev:dc
```

In a browser (or via mock-data login), confirm a DC household with `EbtCardIssueDate` populated renders **"Processed on {date}"** card status. Confirm a DC household with no issue date renders no card-status box.

- [ ] **Step 36.3: Run portal in dev mode for CO**

```bash
pnpm dev:co
```

Confirm CO mock households render the expected statuses for at least one Active, one Lost (with replacement link), and one Frozen scenario.

- [ ] **Step 36.4: Test the simplified cooldown timeline**

Submit a replacement request flow (any state) to set `cardRequestedAt` in the portal DB. Confirm:
- Within cooldown window → "Card replacement requested on {date}" notice renders, "Request replacement" link hidden.
- After cooldown (force a backdated request via mock data) → status display returns, replacement link visible.

- [ ] **Step 36.5: Inspect API JSON shape**

Hit `/api/household/me` (authenticated) and confirm the JSON response:
- `summerEbtCases[i].ebtCardStatus` is a string ("Active", "Processed", etc.) — not an int.
- `applications[i]` does not include `cardStatus`, `cardRequestedAt`, `cardMailedAt`, `cardActivatedAt`, or `cardDeactivatedAt`.

### Task 37: Run integration test suite

- [ ] **Step 37.1: Full backend test suite (with Testcontainers)**

```bash
docker compose up -d
pnpm api:test
```

Expected: PASS, including Integration / SqlServer tests.

### Task 38: Push branches and open draft PRs

This task is **manual** — the human creates the PRs through GitHub UI or `gh pr create`. Per `CLAUDE.local.md`, never push or comment on PRs without explicit user instruction.

- [ ] **Step 38.1: Push each repo's branch**

For each of the four worktrees, run:

```bash
git -C <worktree-path> push -u origin feature/DC-256-card-status-mapping
```

- [ ] **Step 38.2: Open four draft PRs**

In each repo, open a draft PR from `feature/DC-256-card-status-mapping` → `main`. Use the project PR template (see `.github/pull_request_template.md`).

PR description must include:
- Jira link to DC-256
- Summary
- Cross-links to the other three PRs
- The "i18n gaps" callout from the spec's "PR Description Notes" section
- The "Behavioral change callouts" from the same section
- Completion checklist per the template

- [ ] **Step 38.3: Wait for CI on all four; mark all four ready when green; merge in order**

Merge order: state-connector → dc-connector & co-connector (either order) → portal.

---

## Self-Review Notes

**Spec coverage:** All 10 design decisions in the spec have at least one task. The "out of scope" items (DC mapping audit, DAMAGED-AUTO-REISSUE UX, locale CSV updates, locale linting) are correctly absent from the plan.

**Type consistency:** `ConvertEnum<T>` signature is consistent across Tasks 19 and 24. `CardStatus` member set matches between the Interface (Task 1) and Core (Task 16) enum definitions. The frontend `CARD_STATUSES` array (Task 27) lists the same 10 names as the C# enum.

**Worktree path consistency:** All `git -C` commands use absolute paths per the `feedback_absolute_paths_in_worktrees` memory.
