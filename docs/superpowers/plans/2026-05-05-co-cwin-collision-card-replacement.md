# DC-358: CO Card Replacement Cwin Collision + DD Filter — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix CO card replacement failing with `CASES_NOT_FOUND` when a household has a Denied Duplicate (DD) case sharing a `sebtChldCwin` with an active case.

**Architecture:** Two complementary changes. (1) Replace `CardReplacementRequest.CaseIds: IList<string>` with `CaseRefs: IList<CaseRef>` carrying `(SummerEbtCaseId, ApplicationId?, ApplicationStudentId?)`, so application-based active cases can be matched by their unique `(appId, childId)` pair. (2) Add a `CbmsCaseFilters.IsDeniedDuplicate` predicate inside the CO connector and pre-filter DD rows from the candidate pool — needed for auto-eligible (DIRC) active cases whose portal model carries null `ApplicationId`/`ApplicationStudentId` and so falls back to cwin matching.

**Tech Stack:** .NET 10 / C#, xUnit + NSubstitute + Bogus, Next.js 16 / React 19 / TypeScript, Zod, Vitest, MSW.

**Spec:** `docs/superpowers/specs/2026-05-04-co-cwin-collision-and-dd-filtering-design.md`

---

## Repos and branches

This plan touches four repos. Each gets its own feature branch, all named `DC-358-co-cwin-collision-card-replacement` for consistency.

| Repo | Path | Branch |
|---|---|---|
| state-connector | `~/Desktop/Code/sebt-self-service-portal-state-connector` | `DC-358-co-cwin-collision-card-replacement` |
| co-connector | `~/Desktop/Code/sebt-self-service-portal-co-connector` | `DC-358-co-cwin-collision-card-replacement` |
| dc-connector | `~/Desktop/Code/sebt-self-service-portal-dc-connector` | `DC-358-co-cwin-collision-card-replacement` |
| portal | `~/Desktop/Code/sebt-self-service-portal` | `DC-358-co-cwin-collision-card-replacement` (already exists, spec already committed) |

**Cross-repo merge order** (per project convention): state-connector → co-connector + dc-connector → portal.

---

## File inventory

| Phase | File | Action | Responsibility |
|---|---|---|---|
| 1 | `state-connector/src/SEBT.Portal.StatesPlugins.Interfaces/Models/Household/CaseRef.cs` | Create | New state-agnostic case key carrier |
| 1 | `state-connector/src/SEBT.Portal.StatesPlugins.Interfaces/Models/Household/CardReplacementRequest.cs` | Modify | Replace `CaseIds` with `CaseRefs` |
| 1 | `state-connector/src/SEBT.Portal.StatesPlugins.Interfaces.Tests/ModelContractTests.cs` | Modify | Add `CaseRef` constructibility test |
| 2 | `co-connector/src/SEBT.Portal.StatePlugins.CO/Cbms/CbmsCaseFilters.cs` | Create | DD-recognition predicate |
| 2 | `co-connector/src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/CbmsCaseFiltersTests.cs` | Create | Tests for the predicate |
| 2 | `co-connector/src/SEBT.Portal.StatePlugins.CO.CbmsApi/TestData/CbmsMocks/get-account-details.dd-collision.json` | Create | Sanitized AP+DD fixture |
| 2 | `co-connector/src/SEBT.Portal.StatePlugins.CO/ColoradoCardReplacementService.cs` | Modify | Re-key matching, pre-filter DD |
| 2 | `co-connector/src/SEBT.Portal.StatePlugins.CO.Tests/ColoradoCardReplacementServiceTests.cs` | Modify | New tests for fix; existing tests updated for new request shape |
| 3 | `dc-connector/src/SEBT.Portal.StatePlugins.DC/DcCardReplacementService.cs` | Modify | Read `caseRef.SummerEbtCaseId` instead of bare string |
| 3 | `dc-connector/test/SEBT.Portal.StatePlugins.DC.Tests/DcCardReplacementServiceTests.cs` | Modify | Update test request shape |
| 4 | `portal/src/SEBT.Portal.UseCases/Household/RequestCardReplacement/RequestCardReplacementCommand.cs` | Modify | `CaseRefs` instead of `CaseIds` |
| 4 | `portal/src/SEBT.Portal.UseCases/Household/RequestCardReplacement/RequestCardReplacementCommandHandler.cs` | Modify | Build plugin `CaseRef` from matched cases |
| 4 | `portal/src/SEBT.Portal.UseCases/Household/RequestCardReplacement/RequestCardReplacementCommandValidator.cs` | Modify | Validate new shape |
| 4 | `portal/src/SEBT.Portal.Api/Models/Household/RequestCardReplacementRequest.cs` | Modify | Wire DTO with `CaseRefDto` |
| 4 | `portal/src/SEBT.Portal.Api/Controllers/Household/HouseholdController.cs` | Modify | Pass `CaseRefs` through to command |
| 4 | `portal/test/SEBT.Portal.Tests/Unit/UseCases/Household/RequestCardReplacementCommandHandlerTests.cs` | Modify | Update tests for new shape |
| 4 | `portal/src/SEBT.Portal.Web/src/features/cards/api/schema.ts` | Modify | New Zod request schema with `caseRefs` |
| 4 | `portal/src/SEBT.Portal.Web/src/features/cards/api/schema.test.ts` | Modify | Update tests |
| 4 | `portal/src/SEBT.Portal.Web/src/features/cards/components/ConfirmRequest/ConfirmRequest.tsx` | Modify | Build `caseRefs` from in-scope cases |
| 4 | `portal/src/SEBT.Portal.Web/src/features/cards/components/ConfirmRequest/ConfirmRequest.test.tsx` | Modify | Assert new wire shape |
| 4 | `portal/src/SEBT.Portal.Web/src/mocks/handlers.ts` | Modify | MSW handlers accept new shape |

Test file paths assume conventions seen in the repos. If a path doesn't match (e.g. command handler tests live elsewhere), use the actual path from the repo.

---

## Phase 1 — state-connector contract

### Task 1: Create the feature branch in the state-connector repo

**Files:** none yet

- [ ] **Step 1: Switch repos and create the branch**

```bash
cd ~/Desktop/Code/sebt-self-service-portal-state-connector
git status               # should be clean
git checkout main
git pull
git checkout -b DC-358-co-cwin-collision-card-replacement
```

Expected: new branch created, working tree clean.

---

### Task 2: Create `CaseRef` model

**Files:**
- Create: `src/SEBT.Portal.StatesPlugins.Interfaces/Models/Household/CaseRef.cs`

- [ ] **Step 1: Write the file**

```csharp
namespace SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

/// <summary>
/// Reference to a specific case for write-path operations (card replacement).
/// State-agnostic: each connector populates whichever subset of identifiers its upstream provides
/// and reads whichever subset it needs.
/// </summary>
public class CaseRef
{
    /// <summary>
    /// Primary case identifier (from <see cref="Data.Cases.SummerEbtCase.SummerEBTCaseID"/>).
    /// Always present.
    /// </summary>
    public required string SummerEbtCaseId { get; init; }

    /// <summary>
    /// Application identifier when the case is application-based; null for auto-eligible cases.
    /// CO populates from CBMS <c>sebtAppId</c> when <c>eligSrc ∈ {CBMS, PK}</c>.
    /// </summary>
    public string? ApplicationId { get; init; }

    /// <summary>
    /// Per-(case, child) identifier when the case is application-based; null for auto-eligible cases.
    /// CO populates from CBMS <c>sebtChldId</c> when <c>eligSrc ∈ {CBMS, PK}</c>.
    /// </summary>
    public string? ApplicationStudentId { get; init; }
}
```

- [ ] **Step 2: Build to confirm it compiles**

```bash
cd ~/Desktop/Code/sebt-self-service-portal-state-connector
dotnet build
```

Expected: build succeeds with no errors.

---

### Task 3: Update `CardReplacementRequest` to carry `CaseRefs`

**Files:**
- Modify: `src/SEBT.Portal.StatesPlugins.Interfaces/Models/Household/CardReplacementRequest.cs`

- [ ] **Step 1: Replace `CaseIds` with `CaseRefs`**

Replace the entire file contents with:

```csharp
namespace SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

/// <summary>
/// Request to issue a replacement card for one or more cases in a household.
/// </summary>
public class CardReplacementRequest
{
    /// <summary>The household identifier value (e.g., guardian email) resolved by the portal.</summary>
    public required string HouseholdIdentifierValue { get; init; }

    /// <summary>
    /// Case references the replacement applies to. Carries the unique-key triple
    /// (<see cref="CaseRef.SummerEbtCaseId"/>, <see cref="CaseRef.ApplicationId"/>,
    /// <see cref="CaseRef.ApplicationStudentId"/>) so connectors can resolve cases
    /// unambiguously even when multiple upstream rows share a per-child identifier.
    /// </summary>
    public required IReadOnlyList<CaseRef> CaseRefs { get; init; }

    /// <summary>Reason for the replacement request. <see cref="CardReplacementReason.Unspecified"/> when the UI does not collect one.</summary>
    public required CardReplacementReason Reason { get; init; }
}
```

- [ ] **Step 2: Build to confirm the contract compiles in isolation**

```bash
cd ~/Desktop/Code/sebt-self-service-portal-state-connector
dotnet build src/SEBT.Portal.StatesPlugins.Interfaces/SEBT.Portal.StatesPlugins.Interfaces.csproj
```

Expected: builds. (The test project will fail next; that's OK — fix in Task 4.)

---

### Task 4: Add `CaseRef` contract test; update existing model tests

**Files:**
- Modify: `src/SEBT.Portal.StatesPlugins.Interfaces.Tests/ModelContractTests.cs`

- [ ] **Step 1: Read the existing test file to see its conventions**

```bash
cat src/SEBT.Portal.StatesPlugins.Interfaces.Tests/ModelContractTests.cs
```

Expected output: a file using xUnit `[Fact]` tests. Match its style for the new test (e.g., reflective constructibility check or direct construction with assertions).

- [ ] **Step 2: Write the failing test for `CaseRef`**

Append to `ModelContractTests.cs` (before the closing class brace):

```csharp
[Fact]
public void CaseRef_Can_Be_Constructed_With_Only_Required_Fields()
{
    var caseRef = new CaseRef
    {
        SummerEbtCaseId = "ABC123"
    };

    Assert.Equal("ABC123", caseRef.SummerEbtCaseId);
    Assert.Null(caseRef.ApplicationId);
    Assert.Null(caseRef.ApplicationStudentId);
}

[Fact]
public void CaseRef_Can_Be_Constructed_With_All_Fields()
{
    var caseRef = new CaseRef
    {
        SummerEbtCaseId = "ABC123",
        ApplicationId = "APP-1",
        ApplicationStudentId = "STU-1"
    };

    Assert.Equal("ABC123", caseRef.SummerEbtCaseId);
    Assert.Equal("APP-1", caseRef.ApplicationId);
    Assert.Equal("STU-1", caseRef.ApplicationStudentId);
}
```

If the file has a `using` for the household namespace already, no change. Otherwise add `using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;` at the top.

- [ ] **Step 3: If `ModelContractTests.cs` references the old `CaseIds` field of `CardReplacementRequest`, update it to use `CaseRefs`**

Search the test file for `CaseIds`. If matched, update the construction to use `CaseRefs = new List<CaseRef> { new() { SummerEbtCaseId = "..." } }` instead.

```bash
grep -n "CaseIds" src/SEBT.Portal.StatesPlugins.Interfaces.Tests/ModelContractTests.cs
```

If the grep returns lines, edit those lines; if no output, skip.

- [ ] **Step 4: Run the test to verify it passes**

```bash
cd ~/Desktop/Code/sebt-self-service-portal-state-connector
dotnet test --filter "FullyQualifiedName~CaseRef_Can_Be"
```

Expected: 2 passed, 0 failed.

- [ ] **Step 5: Run the full test suite**

```bash
dotnet test
```

Expected: all tests pass. If any tests fail because they reference `CardReplacementRequest.CaseIds`, update them to `CaseRefs`. Re-run until all green.

---

### Task 5: Commit Phase 1 and push

**Files:** none changed in this step

- [ ] **Step 1: Commit**

```bash
cd ~/Desktop/Code/sebt-self-service-portal-state-connector
git add -A
git status         # verify only intended files staged
git commit -m "$(cat <<'EOF'
DC-358: Add CaseRef and replace CardReplacementRequest.CaseIds with CaseRefs

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 2: Build the interface package and verify it lands in the local NuGet store**

The state-connector publishes to `~/nuget-store/` so the connectors and portal can consume it.

```bash
cd ~/Desktop/Code/sebt-self-service-portal-state-connector
dotnet pack -c Release
ls ~/nuget-store/ | grep -i StatesPlugins
```

Expected: a `.nupkg` for the interfaces package with a fresh timestamp.

- [ ] **Step 3: Push the branch**

```bash
git push -u origin DC-358-co-cwin-collision-card-replacement
```

Open a PR on this repo. Wait for it to merge before proceeding to Phase 2 in the strict order — but for local development the connectors will pick up the freshly packed `.nupkg` immediately, so Phase 2 work can begin locally now.

---

## Phase 2 — co-connector

### Task 6: Create the feature branch in the co-connector repo

**Files:** none

- [ ] **Step 1: Switch repos and create the branch**

```bash
cd ~/Desktop/Code/sebt-self-service-portal-co-connector
git status               # should be clean
git checkout main
git pull
git checkout -b DC-358-co-cwin-collision-card-replacement
```

- [ ] **Step 2: Restore packages so the new state-connector contract is picked up**

```bash
dotnet restore
dotnet build
```

Expected: build fails with errors about `request.CaseIds` not existing on `CardReplacementRequest`. That's expected — we'll fix it in this phase.

---

### Task 7: Create `CbmsCaseFilters` predicate (TDD)

**Files:**
- Create: `src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/CbmsCaseFiltersTests.cs`
- Create: `src/SEBT.Portal.StatePlugins.CO/Cbms/CbmsCaseFilters.cs`

- [ ] **Step 1: Write the failing tests**

Create `src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/CbmsCaseFiltersTests.cs`:

```csharp
using SEBT.Portal.StatePlugins.CO.Cbms;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using Xunit;

namespace SEBT.Portal.StatePlugins.CO.Tests.Cbms;

public class CbmsCaseFiltersTests
{
    [Theory]
    [InlineData("DD")]
    [InlineData("dd")]
    [InlineData("Dd")]
    [InlineData("dD")]
    public void IsDeniedDuplicate_Returns_True_For_DD_Codes_Case_Insensitive(string code)
    {
        var row = new GetAccountStudentDetail { StdntEligSts = code };

        Assert.True(CbmsCaseFilters.IsDeniedDuplicate(row));
    }

    [Theory]
    [InlineData("AP")]
    [InlineData("DE")]
    [InlineData("OT")]
    [InlineData("AI")]
    [InlineData("PD")]
    [InlineData("PE")]
    [InlineData("")]
    [InlineData(null)]
    public void IsDeniedDuplicate_Returns_False_For_Non_DD_Codes(string? code)
    {
        var row = new GetAccountStudentDetail { StdntEligSts = code };

        Assert.False(CbmsCaseFilters.IsDeniedDuplicate(row));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
cd ~/Desktop/Code/sebt-self-service-portal-co-connector
dotnet test --filter "FullyQualifiedName~CbmsCaseFiltersTests"
```

Expected: compilation error — `CbmsCaseFilters` does not exist.

- [ ] **Step 3: Implement the predicate**

Create `src/SEBT.Portal.StatePlugins.CO/Cbms/CbmsCaseFilters.cs`:

```csharp
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;

namespace SEBT.Portal.StatePlugins.CO.Cbms;

/// <summary>
/// Single owner of CBMS row-level filtering predicates used by write-path services.
/// </summary>
internal static class CbmsCaseFilters
{
    /// <summary>
    /// True when the row is a Denied Duplicate. CBMS encodes this as <c>stdntEligSts="DD"</c>.
    /// DD rows must not be exposed to the frontend or be acted on by the
    /// card-replacement write path.
    /// </summary>
    public static bool IsDeniedDuplicate(GetAccountStudentDetail row) =>
        row is not null
        && string.Equals(row.StdntEligSts, "DD", StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

```bash
dotnet test --filter "FullyQualifiedName~CbmsCaseFiltersTests"
```

Expected: 12 passed, 0 failed.

- [ ] **Step 5: Commit**

```bash
git add src/SEBT.Portal.StatePlugins.CO/Cbms/CbmsCaseFilters.cs \
        src/SEBT.Portal.StatePlugins.CO.Tests/Cbms/CbmsCaseFiltersTests.cs
git commit -m "$(cat <<'EOF'
DC-358: Add CbmsCaseFilters.IsDeniedDuplicate predicate

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 8: Create the sanitized DD-collision fixture

**Files:**
- Create: `src/SEBT.Portal.StatePlugins.CO.CbmsApi/TestData/CbmsMocks/get-account-details.dd-collision.json`

- [ ] **Step 1: Write the fixture**

The fixture mirrors the bug shape captured from `review2`: one child has both an AP row (`eligSrc=DIRC`, auto-eligible) and a DD row (`eligSrc=PK`, application denied as duplicate), sharing the same `sebtChldCwin` with different `sebtAppId`/`sebtChldId`. Names, addresses, phones, emails are sanitized to match the existing `get-account-details.actual.json` style (clearly fake archaic-sounding names).

```json
{
    "stdntEnrollDtls": [
        {
            "gurdFstNm": "ELSPETH",
            "gurdLstNm": "WIBERLEY",
            "gurdPhnNm": "5557650100",
            "gurdEmailAddr": "sebt.co+ddcollision@codeforamerica.org",
            "sebtYear": 2026,
            "sebtAppId": 1199133,
            "stdFstNm": "OSWALD",
            "stdLstNm": "WIBERLEY",
            "stdDob": "2008-04-08",
            "stdntEligSts": "AP",
            "sebtAppSts": "PW",
            "eligSrc": "DIRC",
            "sebtChldId": 1200889,
            "sebtChldCwin": 3575922,
            "addrLn1": "1234 SAMPLE LN",
            "addrLn2": "",
            "cty": "DENVER",
            "staCd": "CO",
            "zip": "80206",
            "zip4": "",
            "ebtCardLastFour": "",
            "benAvalDt": "2026-04-06",
            "benExpDt": "2026-08-06",
            "ebtCardSts": "",
            "cardIssDt": "",
            "cardBal": 0,
            "cbmsCsId": "1BENFH4",
            "dircEligSrc": ""
        },
        {
            "gurdFstNm": "ALARIC",
            "gurdLstNm": "TREMAYNE",
            "gurdPhnNm": "5557650100",
            "gurdEmailAddr": "sebt.co+ddcollision@codeforamerica.org",
            "sebtYear": 2026,
            "sebtAppId": 1199934,
            "stdFstNm": "OSWALD",
            "stdLstNm": "WIBERLEY",
            "stdDob": "2008-04-08",
            "stdntEligSts": "DD",
            "sebtAppSts": "DU",
            "eligSrc": "PK",
            "sebtChldId": 1201813,
            "sebtChldCwin": 3575922,
            "addrLn1": "1234 SAMPLE LN",
            "addrLn2": "",
            "cty": "DENVER",
            "staCd": "",
            "zip": "80206",
            "zip4": "",
            "ebtCardLastFour": "",
            "benAvalDt": "",
            "benExpDt": "",
            "ebtCardSts": "",
            "cardIssDt": "",
            "cardBal": 0,
            "cbmsCsId": "",
            "dircEligSrc": ""
        }
    ],
    "respCd": "00",
    "respMsg": "Success"
}
```

The structurally important values (`sebtChldCwin=3575922` shared, distinct `sebtAppId`/`sebtChldId` per row, `stdntEligSts=AP|DD`, `eligSrc=DIRC|PK`) are preserved from the captured response. Everything else is fictional.

- [ ] **Step 2: Verify the file is valid JSON**

```bash
cd ~/Desktop/Code/sebt-self-service-portal-co-connector
python3 -m json.tool src/SEBT.Portal.StatePlugins.CO.CbmsApi/TestData/CbmsMocks/get-account-details.dd-collision.json > /dev/null && echo "valid JSON"
```

Expected: prints `valid JSON`.

- [ ] **Step 3: Commit**

```bash
git add src/SEBT.Portal.StatePlugins.CO.CbmsApi/TestData/CbmsMocks/get-account-details.dd-collision.json
git commit -m "$(cat <<'EOF'
DC-358: Add DD-collision CBMS fixture

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 9: Add failing card-replacement tests using the new fixture

**Files:**
- Modify: `src/SEBT.Portal.StatePlugins.CO.Tests/ColoradoCardReplacementServiceTests.cs`

- [ ] **Step 1: Read the existing test file to see its setup conventions**

```bash
cat src/SEBT.Portal.StatePlugins.CO.Tests/ColoradoCardReplacementServiceTests.cs | head -100
```

Note the patterns used: how `HouseholdCache` is mocked, how `CardReplacementRequest` is constructed today, how `GetAccountDetailsResponse` fixtures are loaded, what test-helper methods exist. Match those patterns when adding new tests.

- [ ] **Step 2: Add a helper for loading the new fixture (if there isn't one already)**

If existing tests load fixtures via a helper like `LoadFixture("get-account-details.actual.json")`, reuse that for the new fixture. If they use inline `JsonSerializer.Deserialize` calls, follow that pattern.

If no helper exists, add at top of the test class (or its base):

```csharp
private static GetAccountDetailsResponse LoadCbmsFixture(string fileName)
{
    var path = Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..",
        "SEBT.Portal.StatePlugins.CO.CbmsApi", "TestData", "CbmsMocks", fileName);
    var json = File.ReadAllText(path);
    return JsonSerializer.Deserialize<GetAccountDetailsResponse>(json,
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
        ?? throw new InvalidOperationException($"Failed to deserialize {fileName}");
}
```

The exact path resolution may need tweaking — verify the existing fixture is loaded correctly via the same helper before relying on it. If loading fails, debug the path; the file is committed and present.

- [ ] **Step 3: Write the new failing test for the bug shape**

Add to `ColoradoCardReplacementServiceTests.cs`:

```csharp
[Fact]
public async Task RequestCardReplacement_DDCollision_Matches_Only_AP_Row()
{
    // Fixture: one child has both an AP (DIRC) row and a DD (PK) row sharing sebtChldCwin=3575922.
    // Frontend sends a CaseRef carrying only the cwin (auto-eligible AP has null appId/childId).
    // Expected: DD pre-filter removes the DD row, cwin matches only the AP row, request succeeds.

    var cbmsResponse = LoadCbmsFixture("get-account-details.dd-collision.json");
    var (service, patchHandler) = BuildServiceWithCachedResponse(cbmsResponse, expectPatchSuccess: true);

    var request = new CardReplacementRequest
    {
        HouseholdIdentifierValue = "5557650100",
        CaseRefs = new List<CaseRef>
        {
            new()
            {
                SummerEbtCaseId = "3575922",
                ApplicationId = null,
                ApplicationStudentId = null
            }
        },
        Reason = CardReplacementReason.Unspecified
    };

    var result = await service.RequestCardReplacementAsync(request, CancellationToken.None);

    Assert.True(result.IsSuccess);
    // The PATCH body should include exactly the AP row's identifiers (sebtAppId=1199133, sebtChldId=1200889),
    // not the DD row's (sebtAppId=1199934, sebtChldId=1201813).
    var sentBodies = patchHandler.GetCapturedBodies();
    Assert.Single(sentBodies);
    Assert.Contains("1199133", sentBodies[0]);
    Assert.Contains("1200889", sentBodies[0]);
    Assert.DoesNotContain("1199934", sentBodies[0]);
    Assert.DoesNotContain("1201813", sentBodies[0]);
}
```

`BuildServiceWithCachedResponse` and `patchHandler.GetCapturedBodies()` are placeholders for the existing test-harness conventions. Use whatever the existing tests use for:
- Constructing `ColoradoCardReplacementService` with a mocked `HouseholdCache` returning `cbmsResponse`
- Capturing the JSON body sent to CBMS in the PATCH

Read existing tests for the exact API; reuse them.

- [ ] **Step 4: Run the test to verify it fails**

```bash
dotnet test --filter "FullyQualifiedName~RequestCardReplacement_DDCollision_Matches_Only_AP_Row"
```

Expected: compilation error — `CardReplacementRequest.CaseRefs` does not exist on the locally-built copy yet (the property the test sets has not been wired through the service). The compile error is *not* the same as the test failing in the desired way; it's expected and will resolve in the next task.

If for some reason the project builds, the test should fail — likely with `CASES_NOT_FOUND` since the service still uses `request.CaseIds` (which won't exist or won't be populated).

- [ ] **Step 5: Add a test for application-based active + application-based DD case**

Add a second test for the *other* shape (application-based active row, application-based DD row):

```csharp
[Fact]
public async Task RequestCardReplacement_ApplicationBased_AP_With_DD_Sibling_Matches_By_AppId_ChildId()
{
    // Synthetic fixture: child has an AP (CBMS) row and a DD (CBMS) row sharing cwin.
    // Active case is application-based, so frontend sends CaseRef with appId/childId populated.
    // Expected: matcher uses (appId, childId) → matches AP row only; DD filter is also defense-in-depth.

    var cbmsResponse = new GetAccountDetailsResponse
    {
        RespCd = "00",
        RespMsg = "Success",
        StdntEnrollDtls = new List<GetAccountStudentDetail>
        {
            new()
            {
                StdntEligSts = "AP",
                EligSrc = "CBMS",
                SebtAppId = 5001,
                SebtChldId = 6001,
                SebtChldCwin = 7001,
                StdFstNm = "OSWALD",
                StdLstNm = "WIBERLEY",
                StdDob = "2008-04-08",
                AddrLn1 = "1234 SAMPLE LN",
                Cty = "DENVER",
                StaCd = "CO",
                Zip = "80206"
            },
            new()
            {
                StdntEligSts = "DD",
                EligSrc = "CBMS",
                SebtAppId = 5002,
                SebtChldId = 6002,
                SebtChldCwin = 7001,
                StdFstNm = "OSWALD",
                StdLstNm = "WIBERLEY",
                StdDob = "2008-04-08"
            }
        }
    };

    var (service, patchHandler) = BuildServiceWithCachedResponse(cbmsResponse, expectPatchSuccess: true);

    var request = new CardReplacementRequest
    {
        HouseholdIdentifierValue = "5557650100",
        CaseRefs = new List<CaseRef>
        {
            new()
            {
                SummerEbtCaseId = "7001",
                ApplicationId = "5001",
                ApplicationStudentId = "6001"
            }
        },
        Reason = CardReplacementReason.Unspecified
    };

    var result = await service.RequestCardReplacementAsync(request, CancellationToken.None);

    Assert.True(result.IsSuccess);
    var sentBodies = patchHandler.GetCapturedBodies();
    Assert.Single(sentBodies);
    Assert.Contains("5001", sentBodies[0]);
    Assert.Contains("6001", sentBodies[0]);
    Assert.DoesNotContain("5002", sentBodies[0]);
}
```

- [ ] **Step 6: Add a test for the rejection branch**

```csharp
[Fact]
public async Task RequestCardReplacement_When_CaseRef_Matches_No_Rows_Returns_CASES_NOT_FOUND()
{
    var cbmsResponse = LoadCbmsFixture("get-account-details.dd-collision.json");
    var (service, _) = BuildServiceWithCachedResponse(cbmsResponse, expectPatchSuccess: false);

    var request = new CardReplacementRequest
    {
        HouseholdIdentifierValue = "5557650100",
        CaseRefs = new List<CaseRef>
        {
            new()
            {
                SummerEbtCaseId = "9999999",      // doesn't match any cwin in fixture
                ApplicationId = null,
                ApplicationStudentId = null
            }
        },
        Reason = CardReplacementReason.Unspecified
    };

    var result = await service.RequestCardReplacementAsync(request, CancellationToken.None);

    Assert.False(result.IsSuccess);
    Assert.True(result.IsPolicyRejection);
    Assert.Equal("CASES_NOT_FOUND", result.ErrorCode);
    Assert.Contains("Requested 1", result.ErrorMessage);
    Assert.Contains("only 0 matched", result.ErrorMessage);
    Assert.Contains("refresh and try again", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 7: Tests will not yet run because the implementation hasn't been changed**

We'll run them after Task 10 implements the fix.

---

### Task 10: Update existing card-replacement tests to use the new request shape

**Files:**
- Modify: `src/SEBT.Portal.StatePlugins.CO.Tests/ColoradoCardReplacementServiceTests.cs`

- [ ] **Step 1: Find existing tests that build `CardReplacementRequest` with `CaseIds`**

```bash
cd ~/Desktop/Code/sebt-self-service-portal-co-connector
grep -n "CaseIds" src/SEBT.Portal.StatePlugins.CO.Tests/ColoradoCardReplacementServiceTests.cs
```

- [ ] **Step 2: Update each occurrence**

For each match, replace:

```csharp
CaseIds = new List<string> { "SOME-ID", ... }
```

with:

```csharp
CaseRefs = new List<CaseRef>
{
    new() { SummerEbtCaseId = "SOME-ID" }
}
```

If the test was specifically about cwin-only matching (auto-eligible cases), leave `ApplicationId`/`ApplicationStudentId` unset (they default to `null`). If a test specifically exercises application-based matching, populate `ApplicationId` and `ApplicationStudentId` from whatever the test fixture uses.

Add `using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;` at the top if not already imported (`CaseRef` is in the same namespace as `CardReplacementRequest`).

- [ ] **Step 3: Verify with the compiler — do not run tests yet**

```bash
dotnet build src/SEBT.Portal.StatePlugins.CO.Tests/SEBT.Portal.StatePlugins.CO.Tests.csproj
```

Expected: build succeeds (or fails in the *production* code path — `ColoradoCardReplacementService` still references `CaseIds`). The test code itself should compile cleanly.

---

### Task 11: Implement the fix in `ColoradoCardReplacementService`

**Files:**
- Modify: `src/SEBT.Portal.StatePlugins.CO/ColoradoCardReplacementService.cs`

- [ ] **Step 1: Update the empty-CaseRefs guard (around lines 85–90)**

Replace:

```csharp
if (request.CaseIds is null || request.CaseIds.Count == 0)
{
    return CardReplacementResult.PolicyRejected(
        "INVALID_CASE_IDS",
        "At least one case id is required.");
}
```

with:

```csharp
if (request.CaseRefs is null || request.CaseRefs.Count == 0)
{
    return CardReplacementResult.PolicyRejected(
        "INVALID_CASE_IDS",
        "At least one case reference is required.");
}
```

- [ ] **Step 2: Replace the cwin-matching block (around lines 142–166)**

Replace:

```csharp
var students = accountResponse.StdntEnrollDtls ?? [];
if (students.Count == 0)
{
    return CardReplacementResult.PolicyRejected(
        "HOUSEHOLD_NOT_FOUND",
        "CBMS get-account-details returned no enrollment rows for the household identifier.");
}

var requestedCwins = request.CaseIds.ToHashSet(StringComparer.Ordinal);
var matched = students
    .Select(row => (Row: row, Cwin: row.SebtChldCwin?.ToString(System.Globalization.CultureInfo.InvariantCulture)))
    .Where(x => x.Cwin is not null && requestedCwins.Contains(x.Cwin))
    .Select(x => (x.Row, Ids: CbmsGetAccountStudentDetailIds.Resolve(x.Row)))
    .Where(x => CbmsGetAccountStudentDetailIds.CanBuildUpdatePayload(x.Ids))
    .ToList();

if (matched.Count != request.CaseIds.Count)
{
    return CardReplacementResult.PolicyRejected(
        "CASES_NOT_FOUND",
        $"Requested {request.CaseIds.Count} case(s), but only {matched.Count} matched usable CBMS enrollment row(s). " +
        "Portal case list may be stale; ask the user to refresh and retry.");
}
```

with:

```csharp
var students = accountResponse.StdntEnrollDtls ?? [];
if (students.Count == 0)
{
    return CardReplacementResult.PolicyRejected(
        "HOUSEHOLD_NOT_FOUND",
        "CBMS get-account-details returned no enrollment rows for the household identifier.");
}

// Pre-filter DD rows. They must never be acted on regardless of how the request resolves to them.
var actionableStudents = students
    .Where(s => !CbmsCaseFilters.IsDeniedDuplicate(s))
    .ToList();

// Resolve each requested CaseRef to at most one row.
// Prefer the (appId, childId) pair when present (application-based active cases — uniquely keyed).
// Fall back to cwin only when the CaseRef carries no app/child ids (auto-eligible active cases —
// DD doesn't apply to DIRC/CDE rows so cwin is unambiguous in that branch after the DD filter).
var matched = new List<(GetAccountStudentDetail Row, CbmsGetAccountStudentDetailIds.ResolvedIds Ids)>();
foreach (var caseRef in request.CaseRefs)
{
    var row = actionableStudents.FirstOrDefault(s => MatchesCaseRef(s, caseRef));
    if (row is null) continue;

    var ids = CbmsGetAccountStudentDetailIds.Resolve(row);
    if (CbmsGetAccountStudentDetailIds.CanBuildUpdatePayload(ids))
        matched.Add((row, ids));
}

// FirstOrDefault guarantees matched.Count <= request.CaseRefs.Count, so a strict less-than check.
if (matched.Count < request.CaseRefs.Count)
{
    return CardReplacementResult.PolicyRejected(
        "CASES_NOT_FOUND",
        $"Requested {request.CaseRefs.Count} case(s), but only {matched.Count} matched usable CBMS enrollment row(s). " +
        "The case list may have changed since you loaded this page — refresh and try again. " +
        "If the problem persists, contact support.");
}
```

- [ ] **Step 3: Add the `MatchesCaseRef` helper as a private static method on the class**

Add (e.g. right above the `BackendErrorFromApiException` static method):

```csharp
private static bool MatchesCaseRef(GetAccountStudentDetail row, CaseRef caseRef)
{
    // Prefer the unique (sebtAppId, sebtChldId) pair when both are present on the CaseRef.
    if (!string.IsNullOrEmpty(caseRef.ApplicationId)
        && !string.IsNullOrEmpty(caseRef.ApplicationStudentId))
    {
        return string.Equals(
                row.SebtAppId?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                caseRef.ApplicationId, StringComparison.Ordinal)
            && string.Equals(
                row.SebtChldId?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                caseRef.ApplicationStudentId, StringComparison.Ordinal);
    }
    // Fallback for auto-eligible cases (DIRC/CDE) — cwin is the only identifier the portal
    // model exposes to the frontend for these. DD does not apply to auto-eligible rows,
    // and the DD pre-filter has already removed any DD-coded rows from the candidate pool,
    // so cwin is unambiguous here.
    return string.Equals(
        row.SebtChldCwin?.ToString(System.Globalization.CultureInfo.InvariantCulture),
        caseRef.SummerEbtCaseId, StringComparison.Ordinal);
}
```

- [ ] **Step 4: Update remaining references to `request.CaseIds` in this file**

```bash
grep -n "request\.CaseIds\|caseId" src/SEBT.Portal.StatePlugins.CO/ColoradoCardReplacementService.cs
```

For each match outside the block we already changed, replace `request.CaseIds.Count` with `request.CaseRefs.Count`. Update the log line around the patch dispatch (`updateBodies.Count` is unchanged because that logs the post-resolution count).

- [ ] **Step 5: Add `using` imports if needed**

Confirm the file has:

```csharp
using SEBT.Portal.StatePlugins.CO.Cbms;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;
```

(They likely already exist; the new code references `CbmsCaseFilters` (CO.Cbms namespace) and `CaseRef` (state-connector household namespace).)

- [ ] **Step 6: Build the project**

```bash
cd ~/Desktop/Code/sebt-self-service-portal-co-connector
dotnet build
```

Expected: builds cleanly.

- [ ] **Step 7: Run the new tests**

```bash
dotnet test --filter "FullyQualifiedName~ColoradoCardReplacementServiceTests"
```

Expected: all tests pass, including:
- `RequestCardReplacement_DDCollision_Matches_Only_AP_Row`
- `RequestCardReplacement_ApplicationBased_AP_With_DD_Sibling_Matches_By_AppId_ChildId`
- `RequestCardReplacement_When_CaseRef_Matches_No_Rows_Returns_CASES_NOT_FOUND`
- All previously-existing tests after the request-shape updates.

If any test fails, debug the failure before continuing.

- [ ] **Step 8: Run the full test suite to confirm no other regressions**

```bash
dotnet test
```

Expected: all tests pass.

- [ ] **Step 9: Commit**

```bash
git add src/SEBT.Portal.StatePlugins.CO/ColoradoCardReplacementService.cs \
        src/SEBT.Portal.StatePlugins.CO.Tests/ColoradoCardReplacementServiceTests.cs
git commit -m "$(cat <<'EOF'
DC-358: Re-key CO card replacement matching by (appId, childId) with DD pre-filter

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 12: Push and PR the co-connector changes

- [ ] **Step 1: Pack the connector and push**

```bash
cd ~/Desktop/Code/sebt-self-service-portal-co-connector
dotnet build -c Release
git push -u origin DC-358-co-cwin-collision-card-replacement
```

Open a PR. May be reviewed in parallel with the dc-connector PR.

---

## Phase 3 — dc-connector

### Task 13: Create the feature branch in the dc-connector repo

**Files:** none

- [ ] **Step 1: Switch repos and create the branch**

```bash
cd ~/Desktop/Code/sebt-self-service-portal-dc-connector
git status
git checkout main
git pull
git checkout -b DC-358-co-cwin-collision-card-replacement
dotnet restore
```

---

### Task 14: Adapt `DcCardReplacementService` to read `CaseRefs`

**Files:**
- Modify: `src/SEBT.Portal.StatePlugins.DC/DcCardReplacementService.cs`

- [ ] **Step 1: Update the empty-list guard (around lines 77–80)**

Replace:

```csharp
if (request.CaseIds is null || request.CaseIds.Count == 0)
{
    throw new ArgumentException("CaseIds must contain at least one case id.", nameof(request));
}
```

with:

```csharp
if (request.CaseRefs is null || request.CaseRefs.Count == 0)
{
    throw new ArgumentException("CaseRefs must contain at least one case reference.", nameof(request));
}
```

- [ ] **Step 2: Update the logging line that reports the count (around line 84–87)**

Replace:

```csharp
_logger?.LogInformation(
    "DcCardReplacement: request received for {CaseCount} case(s), reason {Reason}",
    request.CaseIds.Count,
    request.Reason);
```

with:

```csharp
_logger?.LogInformation(
    "DcCardReplacement: request received for {CaseCount} case(s), reason {Reason}",
    request.CaseRefs.Count,
    request.Reason);
```

- [ ] **Step 3: Update the foreach loop and SP-call site (around lines 109–134)**

Replace:

```csharp
foreach (var caseId in request.CaseIds)
{
    _logger?.LogInformation(
        "DcCardReplacement: dispatching SP {ProcName} for case {CaseId}, reason {ReasonSerialized}",
        procName,
        caseId,
        reasonString);

    var caseResult = await InvokeProcForCaseAsync(
        connection,
        procName,
        request.HouseholdIdentifierValue,
        caseId,
        reasonString,
        cancellationToken);

    if (!caseResult.IsSuccess)
    {
        _logger?.LogWarning(
            "DcCardReplacement: stopping at case {CaseId} — non-success result IsPolicyRejection={IsPolicyRejection} ErrorCode={ErrorCode}",
            caseId,
            caseResult.IsPolicyRejection,
            caseResult.ErrorCode);
        return caseResult;
    }
}

_logger?.LogInformation(
    "DcCardReplacement: success for all {CaseCount} case(s)",
    request.CaseIds.Count);
```

with:

```csharp
foreach (var caseRef in request.CaseRefs)
{
    var caseId = caseRef.SummerEbtCaseId;
    _logger?.LogInformation(
        "DcCardReplacement: dispatching SP {ProcName} for case {CaseId}, reason {ReasonSerialized}",
        procName,
        caseId,
        reasonString);

    var caseResult = await InvokeProcForCaseAsync(
        connection,
        procName,
        request.HouseholdIdentifierValue,
        caseId,
        reasonString,
        cancellationToken);

    if (!caseResult.IsSuccess)
    {
        _logger?.LogWarning(
            "DcCardReplacement: stopping at case {CaseId} — non-success result IsPolicyRejection={IsPolicyRejection} ErrorCode={ErrorCode}",
            caseId,
            caseResult.IsPolicyRejection,
            caseResult.ErrorCode);
        return caseResult;
    }
}

_logger?.LogInformation(
    "DcCardReplacement: success for all {CaseCount} case(s)",
    request.CaseRefs.Count);
```

- [ ] **Step 4: Build the project**

```bash
cd ~/Desktop/Code/sebt-self-service-portal-dc-connector
dotnet build
```

Expected: build succeeds (test project may still fail; fix in next step).

---

### Task 15: Update `DcCardReplacementServiceTests` for new request shape

**Files:**
- Modify: `test/SEBT.Portal.StatePlugins.DC.Tests/DcCardReplacementServiceTests.cs`

- [ ] **Step 1: Find every occurrence of `CaseIds` in the test file**

```bash
cd ~/Desktop/Code/sebt-self-service-portal-dc-connector
grep -n "CaseIds" test/SEBT.Portal.StatePlugins.DC.Tests/DcCardReplacementServiceTests.cs
```

- [ ] **Step 2: For each occurrence, update the request construction**

Replace patterns like:

```csharp
CaseIds = new List<string> { "SEBT-001" }
```

with:

```csharp
CaseRefs = new List<CaseRef>
{
    new() { SummerEbtCaseId = "SEBT-001" }
}
```

DC tests don't need to populate `ApplicationId`/`ApplicationStudentId` — DC doesn't read them.

Add `using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;` at the top if not already imported.

- [ ] **Step 3: Run the tests**

```bash
dotnet test
```

Expected: all tests pass. Coverage is unchanged; we just updated request construction.

- [ ] **Step 4: Commit**

```bash
git add src/SEBT.Portal.StatePlugins.DC/DcCardReplacementService.cs \
        test/SEBT.Portal.StatePlugins.DC.Tests/DcCardReplacementServiceTests.cs
git commit -m "$(cat <<'EOF'
DC-358: Adapt DcCardReplacementService to CardReplacementRequest.CaseRefs

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 5: Push**

```bash
git push -u origin DC-358-co-cwin-collision-card-replacement
```

Open a PR.

---

## Phase 4 — portal

### Task 16: Switch back to the portal repo

**Files:** none

- [ ] **Step 1: Switch repos and verify state**

```bash
cd ~/Desktop/Code/sebt-self-service-portal
git status
git branch          # should already show DC-358-co-cwin-collision-card-replacement (created earlier)
```

- [ ] **Step 2: Rebuild plugins so the local DLLs reflect the connector changes**

```bash
pnpm api:build-co
pnpm api:build-dc
```

Expected: both builds succeed; DLLs copied into `src/SEBT.Portal.Api/plugins-co/` and `src/SEBT.Portal.Api/plugins-dc/`.

- [ ] **Step 3: Confirm the portal builds against the new connector contract**

```bash
pnpm api:build
```

Expected: build fails — the use-case handler still references `request.CaseIds`. We'll fix this through the rest of Phase 4.

---

### Task 17: Update `RequestCardReplacementCommand` and validator

**Files:**
- Modify: `src/SEBT.Portal.UseCases/Household/RequestCardReplacement/RequestCardReplacementCommand.cs`
- Modify: `src/SEBT.Portal.UseCases/Household/RequestCardReplacement/RequestCardReplacementCommandValidator.cs`

- [ ] **Step 1: Read the current command file**

```bash
cat src/SEBT.Portal.UseCases/Household/RequestCardReplacement/RequestCardReplacementCommand.cs
```

- [ ] **Step 2: Define `CaseRefDto` and update the command shape**

Replace the file contents with:

```csharp
using System.Security.Claims;
using SEBT.Portal.Kernel;

namespace SEBT.Portal.UseCases.Household;

/// <summary>
/// Portal-side reference to a specific case for the card-replacement command.
/// Mirrors the state-connector <c>CaseRef</c> but lives in the use-cases layer
/// so the inner layers don't depend on plugin contracts.
/// </summary>
public record CaseRefDto(string SummerEbtCaseId, string? ApplicationId, string? ApplicationStudentId);

public record RequestCardReplacementCommand(
    ClaimsPrincipal User,
    IReadOnlyList<CaseRefDto> CaseRefs) : ICommand;
```

If the existing file has different `using`s, base classes, or interfaces, preserve those — match the existing pattern. The change is purely the property shape.

- [ ] **Step 3: Read the current validator**

```bash
cat src/SEBT.Portal.UseCases/Household/RequestCardReplacement/RequestCardReplacementCommandValidator.cs
```

- [ ] **Step 4: Update the validator to validate `CaseRefs`**

Look for any reference to `CaseIds` and update it to `CaseRefs`. The existing validation likely checks "at least one element"; update to:

```csharp
RuleFor(x => x.CaseRefs).NotEmpty().WithMessage("At least one case reference is required.");
RuleForEach(x => x.CaseRefs).ChildRules(caseRef =>
{
    caseRef.RuleFor(c => c.SummerEbtCaseId)
        .NotEmpty()
        .WithMessage("SummerEbtCaseId is required for each case reference.");
});
```

If the project uses the existing kernel's lightweight validator pattern instead of FluentValidation, match the existing style. Read the file first; adapt accordingly.

- [ ] **Step 5: Build to confirm no syntax errors**

```bash
pnpm api:build
```

Expected: build still fails (handler needs updating) but the use-cases project should compile.

---

### Task 18: Update `RequestCardReplacementCommandHandler`

**Files:**
- Modify: `src/SEBT.Portal.UseCases/Household/RequestCardReplacement/RequestCardReplacementCommandHandler.cs`

- [ ] **Step 1: Add a `using` alias for the plugin `CaseRef`**

At the top of the file, alongside the existing `using PluginCardReplacementRequest = ...` alias, add:

```csharp
using PluginCaseRef = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CaseRef;
```

- [ ] **Step 2: Update the household-cases match**

Find lines 90–92:

```csharp
var requestedCases = household.SummerEbtCases
    .Where(c => c.SummerEBTCaseID != null && command.CaseIds.Contains(c.SummerEBTCaseID))
    .ToList();
```

Replace with:

```csharp
var requestedSummerEbtCaseIds = command.CaseRefs.Select(r => r.SummerEbtCaseId).ToHashSet();
var requestedCases = household.SummerEbtCases
    .Where(c => c.SummerEBTCaseID != null && requestedSummerEbtCaseIds.Contains(c.SummerEBTCaseID))
    .ToList();
```

- [ ] **Step 3: Update the cooldown loop**

Find the loop that hashes `caseId` for cooldown checks (around lines 139–153):

```csharp
foreach (var caseId in command.CaseIds)
{
    var caseHash = identifierHasher.Hash(caseId);
    if (householdHash != null && caseHash != null)
    {
        var hasCooldown = await cardReplacementRepo.HasRecentRequestAsync(
            householdHash, caseHash, CooldownPeriod, cancellationToken);
        if (hasCooldown)
        {
            cooldownErrors.Add(new ValidationError(
                "CaseIds",
                $"A card replacement was requested for this case within the last 14 days."));
        }
    }
}
```

Replace with:

```csharp
foreach (var caseRef in command.CaseRefs)
{
    var caseHash = identifierHasher.Hash(caseRef.SummerEbtCaseId);
    if (householdHash != null && caseHash != null)
    {
        var hasCooldown = await cardReplacementRepo.HasRecentRequestAsync(
            householdHash, caseHash, CooldownPeriod, cancellationToken);
        if (hasCooldown)
        {
            cooldownErrors.Add(new ValidationError(
                "CaseRefs",
                $"A card replacement was requested for this case within the last 14 days."));
        }
    }
}
```

- [ ] **Step 4: Build the plugin request from matched cases**

Find the `pluginRequest` construction (around line 171):

```csharp
var pluginRequest = new PluginCardReplacementRequest
{
    HouseholdIdentifierValue = identifier.Value,
    CaseIds = command.CaseIds,
    Reason = StatesPlugins.Interfaces.Models.Household.CardReplacementReason.Unspecified,
};
```

Replace with:

```csharp
var pluginCaseRefs = requestedCases.Select(c => new PluginCaseRef
{
    SummerEbtCaseId = c.SummerEBTCaseID!,
    ApplicationId = c.ApplicationId,
    ApplicationStudentId = c.ApplicationStudentId,
}).ToList();

var pluginRequest = new PluginCardReplacementRequest
{
    HouseholdIdentifierValue = identifier.Value,
    CaseRefs = pluginCaseRefs,
    Reason = StatesPlugins.Interfaces.Models.Household.CardReplacementReason.Unspecified,
};
```

- [ ] **Step 5: Update the post-success persist loop**

Find the loop that persists cooldown records (around lines 233–241):

```csharp
foreach (var caseId in command.CaseIds)
{
    var caseHash = identifierHasher.Hash(caseId);
    if (householdHash != null && caseHash != null)
    {
        await cardReplacementRepo.CreateAsync(
            householdHash, caseHash, userId.Value, cancellationToken);
    }
}
```

Replace with:

```csharp
foreach (var caseRef in command.CaseRefs)
{
    var caseHash = identifierHasher.Hash(caseRef.SummerEbtCaseId);
    if (householdHash != null && caseHash != null)
    {
        await cardReplacementRepo.CreateAsync(
            householdHash, caseHash, userId.Value, cancellationToken);
    }
}
```

- [ ] **Step 6: Update remaining `command.CaseIds.Count` references for logging**

```bash
grep -n "command\.CaseIds" src/SEBT.Portal.UseCases/Household/RequestCardReplacement/RequestCardReplacementCommandHandler.cs
```

Replace each with `command.CaseRefs.Count` (or `command.CaseRefs` for the iteration form).

- [ ] **Step 7: Build to confirm**

```bash
pnpm api:build
```

Expected: API project builds cleanly. Test project may still fail; fix in Task 19.

---

### Task 19: Update API DTO and controller binding

**Files:**
- Modify: `src/SEBT.Portal.Api/Models/Household/RequestCardReplacementRequest.cs`
- Modify: `src/SEBT.Portal.Api/Controllers/Household/HouseholdController.cs`

- [ ] **Step 1: Update the API DTO**

Replace `src/SEBT.Portal.Api/Models/Household/RequestCardReplacementRequest.cs` contents:

```csharp
using System.ComponentModel.DataAnnotations;

namespace SEBT.Portal.Api.Models.Household;

/// <summary>
/// Request model for requesting replacement cards for one or more cases.
/// </summary>
public record RequestCardReplacementRequest
{
    /// <summary>Case references identifying which cards to replace.</summary>
    [Required(ErrorMessage = "At least one case reference is required.")]
    [MinLength(1, ErrorMessage = "At least one case reference is required.")]
    public required List<CaseRefRequestDto> CaseRefs { get; init; }
}

public record CaseRefRequestDto
{
    /// <summary>Primary case identifier (from <c>SummerEbtCase.summerEBTCaseID</c>).</summary>
    [Required(ErrorMessage = "summerEbtCaseId is required for each case reference.")]
    public required string SummerEbtCaseId { get; init; }

    /// <summary>Application identifier when the case is application-based; null for auto-eligible cases.</summary>
    public string? ApplicationId { get; init; }

    /// <summary>Per-(case, child) identifier when the case is application-based; null for auto-eligible cases.</summary>
    public string? ApplicationStudentId { get; init; }
}
```

- [ ] **Step 2: Update the controller action**

Locate the `RequestCardReplacement` action in `HouseholdController.cs`:

```bash
grep -n "RequestCardReplacement" src/SEBT.Portal.Api/Controllers/Household/HouseholdController.cs
```

In the action, find where it constructs the command from the request DTO. It currently does something like:

```csharp
var command = new RequestCardReplacementCommand(User, request.CaseIds);
```

Replace the construction so it maps the DTO `CaseRefs` to use-case-layer `CaseRefDto`:

```csharp
var caseRefs = request.CaseRefs
    .Select(r => new CaseRefDto(r.SummerEbtCaseId, r.ApplicationId, r.ApplicationStudentId))
    .ToList();
var command = new RequestCardReplacementCommand(User, caseRefs);
```

- [ ] **Step 3: Build**

```bash
pnpm api:build
```

Expected: API builds cleanly.

---

### Task 20: Update `RequestCardReplacementCommandHandlerTests`

**Files:**
- Modify: `test/SEBT.Portal.Tests/Unit/UseCases/Household/RequestCardReplacementCommandHandlerTests.cs`

(Path may differ; locate via `find`.)

- [ ] **Step 1: Find the test file**

```bash
find . -name "RequestCardReplacementCommandHandlerTests.cs" -not -path "*/bin/*" -not -path "*/obj/*"
```

- [ ] **Step 2: Find every reference to `CaseIds`**

```bash
grep -n "CaseIds" $(find . -name "RequestCardReplacementCommandHandlerTests.cs" -not -path "*/bin/*" -not -path "*/obj/*")
```

- [ ] **Step 3: Update each occurrence**

Replace patterns building the command:

```csharp
new RequestCardReplacementCommand(user, new List<string> { "SEBT-001" })
```

with:

```csharp
new RequestCardReplacementCommand(user, new List<CaseRefDto>
{
    new("SEBT-001", null, null)
})
```

For tests that should specifically cover application-based cases, populate the `ApplicationId`/`ApplicationStudentId` arguments.

- [ ] **Step 4: Add a new test asserting the handler builds the plugin request with the populated case-ref fields**

```csharp
[Fact]
public async Task Handle_Maps_ApplicationId_And_ApplicationStudentId_Through_To_Plugin_Request()
{
    // Arrange: a household with an application-based case carrying both fields populated.
    var summerEbtCase = new SummerEbtCase
    {
        SummerEBTCaseID = "CWIN-1",
        ApplicationId = "APP-1",
        ApplicationStudentId = "STU-1",
        ChildFirstName = "Test",
        ChildLastName = "Child",
        ChildDateOfBirth = new DateOnly(2010, 1, 1),
        HouseholdType = "SEBT",
        EligibilityType = "NSLP",
        ApplicationStatus = ApplicationStatus.Approved,
        IssuanceType = IssuanceType.SummerEbt,
        IsCoLoaded = false,
    };
    var household = new HouseholdData
    {
        SummerEbtCases = new[] { summerEbtCase }.ToList(),
        Applications = new List<Application>(),
        BenefitIssuanceType = BenefitIssuanceType.SummerEbt,
    };

    // Substitute setup: GetHouseholdByIdentifierAsync returns `household`,
    // ID-proofing/self-service rules permit, cooldown clear, plugin returns success.
    // ... follow whatever pattern the existing tests use.

    var command = new RequestCardReplacementCommand(
        TestUserPrincipal,
        new List<CaseRefDto> { new("CWIN-1", "APP-1", "STU-1") });

    // Act
    var result = await sut.Handle(command, CancellationToken.None);

    // Assert: the plugin received a request whose CaseRefs include the populated fields.
    Assert.True(result.IsSuccess);
    await cardReplacementService.Received(1).RequestCardReplacementAsync(
        Arg.Is<PluginCardReplacementRequest>(r =>
            r.CaseRefs.Count == 1
            && r.CaseRefs[0].SummerEbtCaseId == "CWIN-1"
            && r.CaseRefs[0].ApplicationId == "APP-1"
            && r.CaseRefs[0].ApplicationStudentId == "STU-1"),
        Arg.Any<CancellationToken>());
}
```

The test setup boilerplate (mocks for `IHouseholdRepository`, `IIdProofingService`, `ISelfServiceEvaluator`, `ICardReplacementService`, `ICardReplacementRequestRepository`, `IDistributedLockProvider`, etc.) should follow the existing tests in the file. Read one passing test in the same file to copy the harness setup verbatim, then customize the assertions.

- [ ] **Step 5: Run the tests**

```bash
pnpm api:test:unit -- --filter "FullyQualifiedName~RequestCardReplacementCommandHandlerTests"
```

Expected: all pass.

- [ ] **Step 6: Run the full backend unit tests**

```bash
pnpm api:test:unit
```

Expected: all pass.

- [ ] **Step 7: Commit backend changes**

```bash
git add src/SEBT.Portal.UseCases src/SEBT.Portal.Api test/SEBT.Portal.Tests
git status
git commit -m "$(cat <<'EOF'
DC-358: Wire CaseRefs through portal command, handler, and API DTO

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 21: Update frontend Zod schema

**Files:**
- Modify: `src/SEBT.Portal.Web/src/features/cards/api/schema.ts`
- Modify: `src/SEBT.Portal.Web/src/features/cards/api/schema.test.ts`

- [ ] **Step 1: Update the schema**

Replace the contents of `src/SEBT.Portal.Web/src/features/cards/api/schema.ts`:

```typescript
import { z } from 'zod'

export const CaseRefSchema = z.object({
  summerEbtCaseId: z.string().min(1, 'summerEbtCaseId is required.'),
  applicationId: z.string().nullable().optional(),
  applicationStudentId: z.string().nullable().optional()
})

export type CaseRef = z.infer<typeof CaseRefSchema>

export const RequestCardReplacementSchema = z.object({
  caseRefs: z.array(CaseRefSchema).min(1, 'At least one case reference is required.')
})

export type RequestCardReplacementRequest = z.infer<typeof RequestCardReplacementSchema>
```

- [ ] **Step 2: Read the existing schema test to learn its conventions**

```bash
cat src/SEBT.Portal.Web/src/features/cards/api/schema.test.ts
```

- [ ] **Step 3: Update / add tests for the new schema**

Replace the existing tests that validate `RequestCardReplacementSchema` with:

```typescript
import { describe, expect, it } from 'vitest'

import { CaseRefSchema, RequestCardReplacementSchema } from './schema'

describe('CaseRefSchema', () => {
  it('accepts a CaseRef with only summerEbtCaseId', () => {
    const result = CaseRefSchema.parse({ summerEbtCaseId: 'CWIN-1' })
    expect(result.summerEbtCaseId).toBe('CWIN-1')
    expect(result.applicationId).toBeUndefined()
    expect(result.applicationStudentId).toBeUndefined()
  })

  it('accepts a CaseRef with all three fields', () => {
    const result = CaseRefSchema.parse({
      summerEbtCaseId: 'CWIN-1',
      applicationId: 'APP-1',
      applicationStudentId: 'STU-1'
    })
    expect(result.summerEbtCaseId).toBe('CWIN-1')
    expect(result.applicationId).toBe('APP-1')
    expect(result.applicationStudentId).toBe('STU-1')
  })

  it('accepts null applicationId and applicationStudentId', () => {
    const result = CaseRefSchema.parse({
      summerEbtCaseId: 'CWIN-1',
      applicationId: null,
      applicationStudentId: null
    })
    expect(result.applicationId).toBeNull()
    expect(result.applicationStudentId).toBeNull()
  })

  it('rejects an empty summerEbtCaseId', () => {
    expect(() => CaseRefSchema.parse({ summerEbtCaseId: '' })).toThrow()
  })

  it('rejects a missing summerEbtCaseId', () => {
    expect(() => CaseRefSchema.parse({})).toThrow()
  })
})

describe('RequestCardReplacementSchema', () => {
  it('accepts a request with one CaseRef', () => {
    const result = RequestCardReplacementSchema.parse({
      caseRefs: [{ summerEbtCaseId: 'CWIN-1' }]
    })
    expect(result.caseRefs).toHaveLength(1)
  })

  it('accepts a request with multiple CaseRefs', () => {
    const result = RequestCardReplacementSchema.parse({
      caseRefs: [
        { summerEbtCaseId: 'CWIN-1' },
        { summerEbtCaseId: 'CWIN-2', applicationId: 'APP-2', applicationStudentId: 'STU-2' }
      ]
    })
    expect(result.caseRefs).toHaveLength(2)
  })

  it('rejects an empty caseRefs array', () => {
    expect(() => RequestCardReplacementSchema.parse({ caseRefs: [] })).toThrow()
  })

  it('rejects missing caseRefs', () => {
    expect(() => RequestCardReplacementSchema.parse({})).toThrow()
  })
})
```

- [ ] **Step 4: Run the schema tests**

```bash
cd src/SEBT.Portal.Web
pnpm test -- src/features/cards/api/schema.test.ts
```

Expected: all pass.

---

### Task 22: Update `ConfirmRequest.tsx` to build `caseRefs`

**Files:**
- Modify: `src/SEBT.Portal.Web/src/features/cards/components/ConfirmRequest/ConfirmRequest.tsx`
- Modify: `src/SEBT.Portal.Web/src/features/cards/components/ConfirmRequest/ConfirmRequest.test.tsx`

- [ ] **Step 1: Update `ConfirmRequest.tsx`**

Find the line (currently around line 31):

```typescript
const caseIds = cases.map((c) => c.summerEBTCaseID).filter((id): id is string => id != null)
```

Replace with:

```typescript
const caseRefs = cases
  .filter((c): c is SummerEbtCase & { summerEBTCaseID: string } => c.summerEBTCaseID != null)
  .map((c) => ({
    summerEbtCaseId: c.summerEBTCaseID,
    applicationId: c.applicationId ?? null,
    applicationStudentId: c.applicationStudentId ?? null
  }))
```

Find the mutation call (around lines 35–36):

```typescript
mutation.mutate(
  { caseIds },
  {
```

Replace with:

```typescript
mutation.mutate(
  { caseRefs },
  {
```

- [ ] **Step 2: Read the existing test file**

```bash
cat src/SEBT.Portal.Web/src/features/cards/components/ConfirmRequest/ConfirmRequest.test.tsx
```

- [ ] **Step 3: Update test mocks/assertions about the request body**

Look for MSW handlers in the test file that intercept `POST /api/household/cards/replace`. They likely assert on the request body via `await request.json()`. Update assertions from `body.caseIds` to `body.caseRefs`.

Find:

```typescript
http.post('/api/household/cards/replace', async ({ request }) => {
  const body = await request.json()
  expect(body).toEqual({ caseIds: ['SEBT-001'] })
  // ...
})
```

Replace with:

```typescript
http.post('/api/household/cards/replace', async ({ request }) => {
  const body = await request.json()
  expect(body).toEqual({
    caseRefs: [
      {
        summerEbtCaseId: 'SEBT-001',
        applicationId: null,
        applicationStudentId: null
      }
    ]
  })
  // ...
})
```

For tests where the case fixture has `applicationId` / `applicationStudentId` populated, update the assertion accordingly.

If test fixtures pass into `ConfirmRequest` cases that don't include `applicationId`/`applicationStudentId`, update those fixtures to include the fields (with `null` or a value as needed).

- [ ] **Step 4: Add a new test asserting CaseRef enrichment for application-based cases**

Append a new test:

```typescript
it('sends applicationId and applicationStudentId when the case is application-based', async () => {
  let capturedBody: unknown = null
  server.use(
    http.post('/api/household/cards/replace', async ({ request }) => {
      capturedBody = await request.json()
      return HttpResponse.json({}, { status: 200 })
    })
  )

  const cases = [
    {
      ...baseCase,                                    // adapt to existing test fixtures
      summerEBTCaseID: 'CWIN-1',
      applicationId: 'APP-1',
      applicationStudentId: 'STU-1'
    }
  ]

  render(<ConfirmRequest cases={cases} address={baseAddress} onBack={vi.fn()} />)
  await user.click(screen.getByRole('button', { name: /order card/i }))

  await waitFor(() => expect(capturedBody).not.toBeNull())
  expect(capturedBody).toEqual({
    caseRefs: [
      {
        summerEbtCaseId: 'CWIN-1',
        applicationId: 'APP-1',
        applicationStudentId: 'STU-1'
      }
    ]
  })
})
```

`baseCase`, `baseAddress`, `server`, and `user` are placeholders for whatever the existing test setup provides. Match the existing patterns in the file.

- [ ] **Step 5: Run the tests**

```bash
cd src/SEBT.Portal.Web
pnpm test -- src/features/cards/components/ConfirmRequest/ConfirmRequest.test.tsx
```

Expected: all pass.

---

### Task 23: Update MSW handlers

**Files:**
- Modify: `src/SEBT.Portal.Web/src/mocks/handlers.ts`

- [ ] **Step 1: Find the handler for `POST /api/household/cards/replace`**

```bash
cd src/SEBT.Portal.Web
grep -n "cards/replace" src/mocks/handlers.ts
```

- [ ] **Step 2: Inspect the handlers and update to expect the new shape**

If a handler validates the request body, update it to validate `caseRefs` instead of `caseIds`. If it just returns a fixed response, no change is required. Read the current handler:

```bash
sed -n '350,380p' src/mocks/handlers.ts
```

If it contains `body.caseIds` or `request.caseIds` parsing, replace with `body.caseRefs` and adjust any conditional logic.

- [ ] **Step 3: Run all frontend tests**

```bash
pnpm test
```

Expected: all pass.

- [ ] **Step 4: Run lint**

```bash
pnpm lint
```

Expected: clean.

---

### Task 24: Full local verification

**Files:** none

- [ ] **Step 1: Build everything from clean**

```bash
cd ~/Desktop/Code/sebt-self-service-portal
pnpm api:build-co
pnpm api:build
cd src/SEBT.Portal.Web && pnpm build && cd ../..
```

Expected: all clean.

- [ ] **Step 2: Run the full backend test suite**

```bash
pnpm api:test:unit
```

Expected: all pass.

- [ ] **Step 3: Run the full frontend test suite**

```bash
cd src/SEBT.Portal.Web
pnpm test
cd ../..
```

Expected: all pass.

- [ ] **Step 4: Manual verification against local CBMS UAT**

```bash
# In one terminal:
docker compose up -d
pnpm dev:co

# In a browser, open http://localhost:3000 and log in as sebt.co+review2@codeforamerica.org
# Navigate to card replacement, select the affected child's card, click through to "Order card"
```

Expected: the request succeeds. The previously-observed `CASES_NOT_FOUND` rejection no longer fires.

In API stdout, verify:
- `Card replacement dispatching to state connector for household identifier kind Phone, 1 case(s)`
- `CBMS CardReplacement: requesting new card for 1 student(s) (PATCH /sebt/update-std-dtls)`
- `CBMS CardReplacement: update-std-dtls completed in {ElapsedMs}ms, respCd=00`
- No `CASES_NOT_FOUND` warning.

- [ ] **Step 5: Commit frontend changes**

```bash
git add src/SEBT.Portal.Web
git status
git commit -m "$(cat <<'EOF'
DC-358: Send caseRefs in card replacement request, update Zod schema and tests

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 6: Push the portal branch**

```bash
git push -u origin DC-358-co-cwin-collision-card-replacement
```

Open a PR against the portal repo. In the PR description, link to:
- The state-connector PR
- The co-connector PR
- The dc-connector PR
- The Jira ticket DC-358
- The design spec at `docs/superpowers/specs/2026-05-04-co-cwin-collision-and-dd-filtering-design.md`

Note in the PR description that this PR depends on the three connector PRs being merged first.

---

## Open follow-ups (out of scope, capture in tickets)

- The original ticket mentioned address updates as part of the same bug. Reproduction did not surface an address-update failure on `review2`; PM confirmed this part of the ticket was likely a misattribution. If a real address-update failure surfaces, file a separate ticket.
- Tech-lead clarification on the typical DD origin (different guardian submitting, vs same guardian re-applying, vs school auto-submission for pre-K) is useful domain context but does not affect this ticket's fix.
- Broader denied-status filtering (`DE`/`OT`) on write paths is a potential follow-up; not in scope for DC-358.

---

## Self-review checklist (run before invoking execution)

- [ ] Every spec section maps to one or more tasks.
- [ ] No "TBD"/"TODO"/placeholder text in any task.
- [ ] Type names consistent across tasks: `CaseRef` (state-connector), `CaseRefDto` (portal use case), `CaseRefRequestDto` (portal API DTO), `CaseRef` (frontend Zod-derived).
- [ ] Field names consistent: `SummerEbtCaseId`, `ApplicationId`, `ApplicationStudentId` (C#) and `summerEbtCaseId`, `applicationId`, `applicationStudentId` (JSON/TS).
- [ ] Each code-touching step shows the actual code.
- [ ] Each task ends with verification (build, test, or lint) before commit.
- [ ] Commits are scoped per repo and per logical unit of work.
- [ ] Cross-repo merge order documented; portal task explicitly waits on connector PRs.
