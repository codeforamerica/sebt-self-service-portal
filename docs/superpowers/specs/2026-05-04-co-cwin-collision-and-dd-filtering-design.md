# CO cwin collision and DD filtering — design

## 1. Context & problem

Colorado households can have a child whose CBMS record carries both an **active** Summer EBT case (`stdntEligSts = AP`) and a **denied-duplicate** case (`stdntEligSts = DD`). Both rows describe the same child, share the same `sebtChldCwin`, and differ in `sebtAppId` / `sebtChldId`.

Today the portal uses `sebtChldCwin` as the case identifier exposed to the frontend (via `SummerEbtCase.SummerEBTCaseID`). The bug:

- **Card replacement** — `ColoradoCardReplacementService` filters CBMS rows by matching `sebtChldCwin` against the requested case ids. When the same child has both AP and DD rows, both match. The strict count check (`matched.Count != requested.Count`) rejects the request as `CASES_NOT_FOUND`. Surface symptom: user cannot request a card replacement for a child who has a DD case in the household. Reproduced in AWS dev (CO) on 2026-05-04 with the `sebt.co+review2@codeforamerica.org` test account; logs show `"Card replacement policy rejection ... CASES_NOT_FOUND"` for a 1-case request against a household whose UI showed 1 application.

DD rows are already excluded from the *read* path: `BuildCases` in `CbmsResponseMapper` keeps only auto-eligible rows or application-based rows with `Approved` status. DD falls through `MapCaseStatus` to `Unknown` and is therefore excluded from `household.SummerEbtCases`. The bug is asymmetry: the read path filters DD, but the card-replacement write path resolves identifiers from the unfiltered raw CBMS response.

Note on scope: the original ticket also mentioned address updates. Reproduction in AWS dev on 2026-05-04 found no address-update failure for the same `review2` account, and the PM subsequently confirmed that the address-update mention was likely a misattribution. Address updates are **out of scope** for this ticket; if a real failure surfaces later it should be a separate ticket with its own reproduction.

Underneath the bug is a single data-model fact:

> **`sebtChldCwin` identifies a child, not a case.** A child can have multiple cases (AP + DD). The truly-unique case key is `(sebtAppId, sebtChldId)` — exposed on the portal model as `SummerEbtCase.ApplicationId` and `SummerEbtCase.ApplicationStudentId`.

A captured `review2` CBMS response (2026-05-04) shows the bug shape concretely: one child has an AP row with `eligSrc=DIRC` (auto-eligible direct certification) and a DD row with `eligSrc=PK` (an application denied as duplicate), both sharing `sebtChldCwin=3575922` with different `sebtAppId`/`sebtChldId`. The active case being DIRC means its portal-model `ApplicationId`/`ApplicationStudentId` are populated as null (per `CbmsResponseMapper`'s "application-based-only" gate), so the frontend's `CaseRef` for this case carries only `SummerEbtCaseId` (the cwin). The fix has two halves that work together: the `(ApplicationId, ApplicationStudentId)` matching path handles the application-based-active + DD case; the DD pre-filter handles the auto-eligible-active + DD case (the reproduced scenario). Both belong; either alone would leave a real-world scenario broken.

## 2. Goals & non-goals

### Goals

- Eliminate the cwin collision in card replacement by routing case identity through `(ApplicationId, ApplicationStudentId)` for application-based cases, with cwin as the fallback for auto-eligible cases (which do not have DD twins).
- Defensively filter DD rows out of the card-replacement candidate pool inside the CO connector, so even a stale or malformed `CaseRef` cannot land on a DD row.
- Keep DD invisible to the frontend per the architectural principle "filter excluded cases entirely within the API."
- Minimize cross-repo blast radius: change only what the bug requires.

### Non-goals

- **No address-update changes.** Reproduction in AWS dev did not produce an address-update failure on `review2`; PM confirmed the original ticket's address-update mention was likely a misattribution. Out of scope.
- **No `ApplicationStatus.DeniedDuplicate` enum value.** Per tech-lead guidance, DD must never reach the frontend; an enum value would expose it.
- **No change to any `AddressUpdateRequest` or address-update service** (`ColoradoAddressUpdateService`, `DcAddressUpdateService`, the state-connector contract).
- **No DC DD awareness.** DC's domain does not have a DD concept; `SummerEBTCaseID` in DC is already a true per-case identifier.
- **No broader denied-status filtering** (e.g. `DE`, `OT`). The ticket calls out DD specifically; broader filtering is a follow-up to discuss with the team.
- **No transitional dual-field API parser.** A hard replacement keeps the contract clean; the deploy-window risk is small (see §10).

## 3. Architecture

### What changes, where, and why

| Change | Repo | Layer | Reason |
|---|---|---|---|
| Replace `CardReplacementRequest.CaseIds: IList<string>` with `CaseRefs: IList<CaseRef>` | `state-connector` | Interfaces | Carry the unique-key triple |
| New `CaseRef` type | `state-connector` | Interfaces | Generic, state-agnostic case key |
| New `CbmsCaseFilters.IsDeniedDuplicate(row)` predicate | `co-connector` | `Cbms/` helpers | Single owner of the literal `"DD"` check |
| Re-key card-replacement matching by `(ApplicationId, ApplicationStudentId)` with cwin fallback; pre-filter DD rows defensively | `co-connector` | `ColoradoCardReplacementService` | Eliminates collision; DD defense-in-depth |
| Adapt to new `CaseRefs` shape (read primary `SummerEbtCaseId` only) | `dc-connector` | `DcCardReplacementService` | Mechanical contract follow |
| Build `CaseRef`s in command handler from already-loaded `SummerEbtCase` objects | `portal` | `RequestCardReplacementCommandHandler` | Use data already in scope |
| Replace `RequestCardReplacementRequest.CaseIds: List<string>` with `CaseRefs: List<CaseRefDto>` | `portal` | API DTO | Wire shape change |
| Mutation sends per-case `(summerEbtCaseId, applicationId, applicationStudentId)` | `portal` | Frontend (`Web/src/features/household/`) | Wire shape change |
| Zod schema accepts the new request shape | `portal` | Frontend | Validation |

### Cross-repo merge order

Per existing convention (preserved in `MEMORY.md` `project_plugin_merge_order.md`):

1. `sebt-self-service-portal-state-connector` (contract change)
2. `sebt-self-service-portal-co-connector` and `sebt-self-service-portal-dc-connector` (parallel)
3. `sebt-self-service-portal` (consumes the new contract)

## 4. Contract changes (state-connector)

### New `CaseRef` type

```csharp
// SEBT.Portal.StatesPlugins.Interfaces/Models/Household/CaseRef.cs (new)
namespace SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

public class CaseRef
{
    /// <summary>Primary case identifier (from SummerEbtCase.SummerEBTCaseID).</summary>
    public required string SummerEbtCaseId { get; init; }

    /// <summary>Application identifier when the case is application-based; null for auto-eligible cases.</summary>
    public string? ApplicationId { get; init; }

    /// <summary>Per-(case, child) identifier when the case is application-based; null for auto-eligible cases.</summary>
    public string? ApplicationStudentId { get; init; }
}
```

The type is state-agnostic: each connector populates whichever subset of identifiers its upstream provides and reads whichever subset it needs. State-specific concepts (cwin, sebtAppId) stay inside the relevant connector.

### `CardReplacementRequest` change

```csharp
// CardReplacementRequest.cs — was:
public required IReadOnlyList<string> CaseIds { get; init; }
// becomes:
public required IReadOnlyList<CaseRef> CaseRefs { get; init; }
```

Hard replacement; not additive. `HouseholdIdentifierValue` and `Reason` are unchanged.

### `ApplicationStatus` enum — unchanged

DD recognition is a CO-internal concern. Adding `DeniedDuplicate` would surface it in the contract and to the frontend; instead the CO connector recognizes the literal `"DD"` code in a small predicate and uses it for filtering only. The existing `[InlineData("DD", ApplicationStatus.Unknown)]` test in `CbmsResponseMapperTests` continues to pass.

## 5. CO connector changes

### `CbmsCaseFilters` (new helper)

```csharp
// SEBT.Portal.StatePlugins.CO/Cbms/CbmsCaseFilters.cs
namespace SEBT.Portal.StatePlugins.CO.Cbms;

internal static class CbmsCaseFilters
{
    /// <summary>
    /// True when the row is a Denied Duplicate. CBMS encodes this as stdntEligSts="DD".
    /// DD rows must not be exposed to the frontend or be acted on by the
    /// card-replacement write path.
    /// </summary>
    public static bool IsDeniedDuplicate(GetAccountStudentDetail row) =>
        string.Equals(row?.StdntEligSts, "DD", StringComparison.OrdinalIgnoreCase);
}
```

Single owner of the DD predicate; called from both write-path services.

### `ColoradoCardReplacementService` — re-keyed matching + DD pre-filter

The existing match-by-cwin block (lines 150–164) becomes:

```csharp
// Filter DD rows out of the candidate pool first — they are not actionable.
var actionableStudents = students
    .Where(s => !CbmsCaseFilters.IsDeniedDuplicate(s))
    .ToList();

// Resolve each requested CaseRef to at most one row.
var matched = new List<(GetAccountStudentDetail Row, ResolvedIds Ids)>();
foreach (var caseRef in request.CaseRefs)
{
    var row = actionableStudents.FirstOrDefault(s => MatchesCaseRef(s, caseRef));
    if (row is null) continue;

    var ids = CbmsGetAccountStudentDetailIds.Resolve(row);
    if (CbmsGetAccountStudentDetailIds.CanBuildUpdatePayload(ids))
        matched.Add((row, ids));
}

if (matched.Count < request.CaseRefs.Count)
{
    return CardReplacementResult.PolicyRejected(
        "CASES_NOT_FOUND",
        $"Requested {request.CaseRefs.Count} case(s), but only {matched.Count} matched usable CBMS enrollment row(s). " +
        "Portal case list may be stale; ask the user to refresh and retry.");
}
```

The rejection branch is preserved as a defensive guard against PATCHing an empty array to CBMS, but the user-facing wording is **not refined** beyond the existing message. Per tech-lead guidance (2026-05-05), the scenarios that would surface this rejection — stale frontend state (DD is set at submission time and is immutable, so a case can't flip to DD between page-load and click), frontend bug (frontend and API are co-deployed so divergence is unlikely), and tampered request (acceptable for tampering to surface ugly errors) — do not justify investing in user-friendly wording.

`MatchesCaseRef` prefers the unique pair when present:

```csharp
private static bool MatchesCaseRef(GetAccountStudentDetail row, CaseRef caseRef)
{
    if (!string.IsNullOrEmpty(caseRef.ApplicationId)
        && !string.IsNullOrEmpty(caseRef.ApplicationStudentId))
    {
        return string.Equals(row.SebtAppId?.ToString(CultureInfo.InvariantCulture),
                             caseRef.ApplicationId, StringComparison.Ordinal)
            && string.Equals(row.SebtChldId?.ToString(CultureInfo.InvariantCulture),
                             caseRef.ApplicationStudentId, StringComparison.Ordinal);
    }
    // Fallback for auto-eligible cases (DIRC/CDE) — cwin is the only identifier.
    // DD does not apply to auto-eligible rows, so cwin is unambiguous here.
    return string.Equals(row.SebtChldCwin?.ToString(CultureInfo.InvariantCulture),
                         caseRef.SummerEbtCaseId, StringComparison.Ordinal);
}
```

The check is `<` rather than `!=` because `FirstOrDefault` per `CaseRef` makes `matched.Count > requested` structurally impossible.

## 6. DC connector changes

`DcCardReplacementService` adapts to the new request shape; DC reads only `SummerEbtCaseId` and ignores the optional fields. Mechanical change in the foreach loop and the empty-list guard. SP signature, parameter binding, and result handling are unchanged. Tests in `DcCardReplacementServiceTests.cs` are updated to construct `CaseRef`s instead of bare strings — coverage identical, fixture shape changes.

## 7. Portal changes

### `RequestCardReplacementCommand`

```csharp
public record RequestCardReplacementCommand(
    ClaimsPrincipal User,
    IReadOnlyList<CaseRefDto> CaseRefs);
```

`CaseRefDto` is a portal-side type with the same fields as the state-connector `CaseRef`. The handler maps between them.

### `RequestCardReplacementCommandHandler`

Three localized changes:

1. Match `household.SummerEbtCases` by `caseRef.SummerEbtCaseId`:
   ```csharp
   var requestedSummerEbtCaseIds = command.CaseRefs.Select(r => r.SummerEbtCaseId).ToHashSet();
   var requestedCases = household.SummerEbtCases
       .Where(c => c.SummerEBTCaseID != null && requestedSummerEbtCaseIds.Contains(c.SummerEBTCaseID))
       .ToList();
   ```
2. Cooldown loop hashes `caseRef.SummerEbtCaseId` (logically identical to today's `caseId`).
3. Build `PluginCardReplacementRequest.CaseRefs` from the matched `SummerEbtCase` objects. The handler file already uses a `using PluginCardReplacementRequest = ...` alias to disambiguate the portal type from the state-connector type; we'd add a parallel `using PluginCaseRef = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CaseRef` alias and use it the same way:
   ```csharp
   var pluginCaseRefs = requestedCases.Select(c => new PluginCaseRef
   {
       SummerEbtCaseId = c.SummerEBTCaseID!,
       ApplicationId = c.ApplicationId,
       ApplicationStudentId = c.ApplicationStudentId,
   }).ToList();
   ```

The handler does not need any new database lookups; the matched `SummerEbtCase` objects are already loaded and already carry `ApplicationId` / `ApplicationStudentId` (populated in `CbmsResponseMapper` for application-based cases; in `DcSummerEbtCaseService` from SP columns).

### `RequestCardReplacementRequest` (API DTO)

```csharp
public record RequestCardReplacementRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "At least one case reference is required.")]
    public required List<CaseRefDto> CaseRefs { get; init; }
}

public record CaseRefDto
{
    [Required]
    public required string SummerEbtCaseId { get; init; }
    public string? ApplicationId { get; init; }
    public string? ApplicationStudentId { get; init; }
}
```

## 8. Frontend changes

### Zod schema (`Web/src/features/household/api/schema.ts` — exact path verified during plan-writing)

```typescript
export const CaseRefSchema = z.object({
  summerEbtCaseId: z.string(),
  applicationId: z.string().nullable().optional(),
  applicationStudentId: z.string().nullable().optional(),
})

export const CardReplacementRequestSchema = z.object({
  caseRefs: z.array(CaseRefSchema).min(1),
})
```

### Mutation call site

The card-replacement mutation builds the request from the case objects already in scope. Every `SummerEbtCase` returned by the household query carries the three fields. The mutation call changes from sending `caseIds: string[]` to sending an array of `{ summerEbtCaseId, applicationId, applicationStudentId }` objects. The exact frontend field names depend on the project's JSON-serializer naming convention (the C# property `SummerEBTCaseID` does not naively round-trip to a clean `summerEbtCaseId` under default camelCase) — to confirm when we open the existing response schema file during plan-writing.

### Verification deferred to plan-writing

- The portal API response model exposes `ApplicationId` and `ApplicationStudentId` on each `SummerEbtCase` today; the response Zod schema needs to confirm these fields are parsed.
- The exact serialized JSON field names for `SummerEBTCaseID`, `ApplicationId`, `ApplicationStudentId` (which determine the request and response Zod field names) — depends on the existing serializer configuration.

## 9. Testing strategy

### TDD ordering

1. **Synthesize the missing fixture first.** Place a JSON fixture next to `get-account-details.actual.json` in the CO connector — for example `get-account-details.dd-collision.json` — that captures one child with both `AP` and `DD` rows (same `sebtChldCwin`, different `sebtAppId` / `sebtChldId`). This is the data shape the existing fixture lacks and the data shape that triggers the bug. It is the single most valuable test artifact added by this work.
2. **Write failing tests** that consume the new fixture and assert the desired post-fix behavior (one matched row, AP-row's IDs in the PATCH, DD-row excluded from the PATCH).
3. **Implement** in repo order: state-connector contract → CO + DC connectors → portal handler / DTO → frontend.

### Coverage by layer

| Layer | New / changed tests | What it proves |
|---|---|---|
| `co-connector` `CbmsCaseFiltersTests` (new) | `IsDeniedDuplicate(row)` returns true for `stdntEligSts ∈ {"DD","Dd","dd"}`; false for `AP`, `DE`, `OT`, null | predicate correctness, case-insensitive |
| `co-connector` `ColoradoCardReplacementServiceTests` | DD-collision fixture: request 1 cwin → success, PATCH targets only the AP row's IDs | collision is gone; right row acted on |
| `co-connector` `ColoradoCardReplacementServiceTests` | existing fixture: request → still succeeds unchanged | regression guard |
| `co-connector` `ColoradoCardReplacementServiceTests` | `CaseRef` with null `ApplicationId`/`ApplicationStudentId` (auto-eligible): cwin fallback matches, succeeds | fallback path works |
| `co-connector` `ColoradoCardReplacementServiceTests` | `CaseRef` matches no non-DD row → `CASES_NOT_FOUND` with `<` semantics and refreshed message | rejection guard fires correctly |
| `co-connector` `CbmsResponseMapperTests` | `[InlineData("DD", ApplicationStatus.Unknown)]` continues to pass | confirms enum was not extended; DD not exposed |
| `dc-connector` `DcCardReplacementServiceTests` | existing tests adapted to construct `CaseRef`s | mechanical adaptation works |
| `state-connector` `EnumContractTests` | unchanged | enum was not touched |
| `state-connector` `ModelContractTests` | new: `CaseRef` is constructible with required + optional fields; serialization roundtrip | new type is contract-stable |
| `portal` `RequestCardReplacementCommandHandlerTests` | request with all three fields → handler builds plugin `CaseRef` correctly | handler maps correctly |
| `portal` `RequestCardReplacementCommandHandlerTests` | request with null `ApplicationId`/`ApplicationStudentId` → handler maps null through | null-handling correct |
| `portal` `HouseholdController` request-binding test | new shape parses; old `caseIds` shape returns 400 | wire contract enforced |
| `portal` `Web/src/features/household/api/schema.test.ts` | new schema parses valid `caseRefs`; rejects malformed input | frontend parses the new shape |
| `portal` `Web` mutation tests | mutation builds `caseRefs` from in-scope case objects with correct wire shape | frontend builds the request correctly |
| `portal` Playwright E2E (optional) | card-replacement happy path with an AP+DD child → only the AP card is replaced | end-to-end smoke (if mocking permits) |

### Out-of-scope test approaches

- **No new CBMS sandbox test.** `CbmsSandboxTests.cs` hits real UAT and depends on UAT data we do not control. Synthesized fixtures are sufficient and cheaper.
- **No new mock-data persona** in `MockHouseholdRepository.SeedMockData`. Mock-mode (`UseMockHouseholdData=true`) bypasses the connector entirely, so it cannot reproduce a connector-side bug. For a manually-clickable repro use CBMS UAT mode with the test account a teammate provides.

## 10. Rollout

- **No DB migration**, no config change, no new feature flag.
- **No persisted data uses the request shape** — `CardReplacementRequestEntity` stores hashed identifiers and timestamps only. Pre-existing data is unaffected.
- **Cross-repo merge order**: state-connector → CO + DC connectors (parallel) → portal.
- **Deploy-window note**: a user with a stale React bundle who clicks "request card replacement" between the frontend and API rolling out will receive a 400. The error is recoverable in one click (refresh). Card replacement is a low-frequency action with a 14-day cooldown; practical impact is small. We accept this rather than carry a transitional dual-shape API parser, which would leave dead weight in the contract.

## 11. Open questions deferred to plan-writing

- Exact frontend file paths for the mutation hook, Zod request schema, and call sites that build the request body.
- Verification that the household API response model exposes `ApplicationId` and `ApplicationStudentId` to the frontend today, and that the response Zod schema parses them.
- Exact JSON serialized field names for `SummerEBTCaseID`, `ApplicationId`, `ApplicationStudentId` under the existing serializer configuration — these determine the wire field names used by the Zod schemas.
- Whether any other portal-side caller of `CardReplacementRequest` exists outside `RequestCardReplacementCommandHandler`.
