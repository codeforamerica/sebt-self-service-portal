# Co-Loaded Case Filtering & Per-Case Feature Flags

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enforce that co-loaded cases (SNAP/TANF benefits loaded onto existing EBT cards) are excluded from the portal's self-service features, and surface per-case `allowAddressChange` / `allowCardReplacement` flags to the frontend.

**Architecture:** Business rules enforced in the UseCases layer (GetHouseholdData filters co-loaded cases; UpdateAddress and RequestCardReplacement reject co-loaded cases). API layer maps per-case flags. Frontend reads flags to gate UI.

**Tech Stack:** C# / .NET 10 / xUnit / NSubstitute / Next.js / TypeScript / Vitest / React Testing Library

---

## File Map

### Backend — UseCases (business rules)
- Modify: `src/SEBT.Portal.UseCases/Household/GetHouseholdData/GetHouseholdDataQueryHandler.cs`
- Modify: `src/SEBT.Portal.UseCases/Household/UpdateAddress/UpdateAddressCommandHandler.cs`
- Modify: `src/SEBT.Portal.UseCases/Household/RequestCardReplacement/RequestCardReplacementCommandHandler.cs`

### Backend — API (response models, mapper)
- Modify: `src/SEBT.Portal.Api/Models/Household/SummerEbtCaseResponse.cs`
- Modify: `src/SEBT.Portal.Api/Models/Household/HouseholdDataResponseMapper.cs`

### Backend — Mock data
- Modify: `src/SEBT.Portal.Infrastructure/Repositories/MockHouseholdRepository.cs`

### Backend — Tests
- Modify: `test/SEBT.Portal.Tests/Unit/UseCases/Household/GetHouseholdDataQueryHandlerTests.cs`
- Modify: `test/SEBT.Portal.Tests/Unit/UseCases/Household/UpdateAddressCommandHandlerTests.cs`
- Modify: `test/SEBT.Portal.Tests/Unit/UseCases/Household/RequestCardReplacementCommandHandlerTests.cs`

### Frontend
- Modify: `src/SEBT.Portal.Web/src/features/household/api/schema.ts`
- Modify: `src/SEBT.Portal.Web/src/features/household/components/HouseholdSummary/HouseholdSummary.tsx`
- Modify: `src/SEBT.Portal.Web/src/features/household/components/HouseholdSummary/HouseholdSummary.test.tsx`
- Modify: `src/SEBT.Portal.Web/src/app/(authenticated)/profile/address/(flow)/page.tsx`

---

## Task 1: Filter co-loaded cases in GetHouseholdDataQueryHandler

### Context

`GetHouseholdDataQueryHandler` currently returns all `SummerEbtCases` from the repository. Mixed-eligibility households (both co-loaded and non-co-loaded cases) should only see their non-co-loaded cases in the portal. Co-loaded cases must be filtered out before returning.

**Files:**
- Modify: `src/SEBT.Portal.UseCases/Household/GetHouseholdData/GetHouseholdDataQueryHandler.cs:56-72`
- Test: `test/SEBT.Portal.Tests/Unit/UseCases/Household/GetHouseholdDataQueryHandlerTests.cs`

- [ ] **Step 1: Write failing test — co-loaded cases are filtered out**

Add this test to `GetHouseholdDataQueryHandlerTests`:

```csharp
[Fact]
public async Task Handle_FiltersOutCoLoadedCases_FromReturnedHouseholdData()
{
    // Arrange: household has one co-loaded case and one non-co-loaded case
    var email = "user@example.com";
    var user = CreateUser(email, UserIalLevel.IAL1plus);
    var identifier = HouseholdIdentifier.Email(EmailNormalizer.Normalize(email));
    var coLoadedCase = new SummerEbtCase
    {
        SummerEBTCaseID = "SEBT-COLOADED",
        ChildFirstName = "CoLoaded",
        ChildLastName = "Child",
        IsCoLoaded = true
    };
    var nonCoLoadedCase = new SummerEbtCase
    {
        SummerEBTCaseID = "SEBT-REGULAR",
        ChildFirstName = "Regular",
        ChildLastName = "Child",
        IsCoLoaded = false
    };
    var householdData = new HouseholdData
    {
        Email = email,
        SummerEbtCases = new List<SummerEbtCase> { coLoadedCase, nonCoLoadedCase }
    };

    _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
        .Returns(identifier);
    _idProofingRequirementsService.GetPiiVisibility(UserIalLevel.IAL1plus)
        .Returns(new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true));
    _repository.GetHouseholdByIdentifierAsync(
            Arg.Any<HouseholdIdentifier>(), Arg.Any<PiiVisibility>(),
            Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
        .Returns(householdData);

    var handler = new GetHouseholdDataQueryHandler(
        _resolver, _repository, _idProofingRequirementsService, _minimumIalService, _logger);
    var query = new GetHouseholdDataQuery { User = user };

    // Act
    var result = await handler.Handle(query, CancellationToken.None);

    // Assert
    Assert.True(result.IsSuccess);
    var success = Assert.IsType<SuccessResult<HouseholdData>>(result);
    Assert.Single(success.Value.SummerEbtCases);
    Assert.Equal("SEBT-REGULAR", success.Value.SummerEbtCases[0].SummerEBTCaseID);
}
```

- [ ] **Step 2: Write failing test — all co-loaded returns empty list**

```csharp
[Fact]
public async Task Handle_ReturnsEmptyCasesList_WhenAllCasesAreCoLoaded()
{
    var email = "user@example.com";
    var user = CreateUser(email, UserIalLevel.IAL1plus);
    var identifier = HouseholdIdentifier.Email(EmailNormalizer.Normalize(email));
    var householdData = new HouseholdData
    {
        Email = email,
        SummerEbtCases = new List<SummerEbtCase>
        {
            new() { SummerEBTCaseID = "SEBT-001", ChildFirstName = "A", ChildLastName = "B", IsCoLoaded = true },
            new() { SummerEBTCaseID = "SEBT-002", ChildFirstName = "C", ChildLastName = "D", IsCoLoaded = true }
        }
    };

    _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
        .Returns(identifier);
    _idProofingRequirementsService.GetPiiVisibility(UserIalLevel.IAL1plus)
        .Returns(new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true));
    _repository.GetHouseholdByIdentifierAsync(
            Arg.Any<HouseholdIdentifier>(), Arg.Any<PiiVisibility>(),
            Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
        .Returns(householdData);

    var handler = new GetHouseholdDataQueryHandler(
        _resolver, _repository, _idProofingRequirementsService, _minimumIalService, _logger);
    var query = new GetHouseholdDataQuery { User = user };

    var result = await handler.Handle(query, CancellationToken.None);

    Assert.True(result.IsSuccess);
    var success = Assert.IsType<SuccessResult<HouseholdData>>(result);
    Assert.Empty(success.Value.SummerEbtCases);
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test test/SEBT.Portal.Tests --filter "FullyQualifiedName~GetHouseholdDataQueryHandlerTests.Handle_FiltersOutCoLoadedCases" --no-build 2>&1 | tail -5`
Run: `dotnet test test/SEBT.Portal.Tests --filter "FullyQualifiedName~GetHouseholdDataQueryHandlerTests.Handle_ReturnsEmptyCasesList_WhenAllCasesAreCoLoaded" --no-build 2>&1 | tail -5`
Expected: FAIL (both tests)

- [ ] **Step 4: Implement co-loaded filtering in GetHouseholdDataQueryHandler**

In `GetHouseholdDataQueryHandler.cs`, after the MinimumIal check succeeds (around line 69), add filtering before returning:

```csharp
        // Filter out co-loaded cases — portal users should only see and manage
        // non-co-loaded cases. Co-loaded benefits are managed by caseworkers.
        householdData.SummerEbtCases = householdData.SummerEbtCases
            .Where(c => !c.IsCoLoaded)
            .ToList();

        logger.LogDebug("Household data retrieved successfully for identifier type {Type}", identifier.Type);
        return Result<HouseholdData>.Success(householdData);
```

Replace the existing final two lines (the LogDebug + return) with the above block.

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test test/SEBT.Portal.Tests --filter "FullyQualifiedName~GetHouseholdDataQueryHandlerTests" --no-build 2>&1 | tail -5`
Expected: ALL PASS (new tests + existing tests unaffected)

- [ ] **Step 6: Commit**

```
feat: filter co-loaded cases from GetHouseholdData response

Co-loaded cases (SNAP/TANF benefits on existing EBT cards) are managed
by caseworkers, not the portal. Filter them out in the use case layer
so the frontend never sees or acts on them.
```

---

## Task 2: Replace BenefitIssuanceType check with IsCoLoaded in UpdateAddressCommandHandler

### Context

`UpdateAddressCommandHandler` currently checks `household.BenefitIssuanceType` (SnapEbtCard/TanfEbtCard) to block address changes. This should check `IsCoLoaded` on cases instead — it's the canonical signal and aligns with the filtering in Task 1.

**Files:**
- Modify: `src/SEBT.Portal.UseCases/Household/UpdateAddress/UpdateAddressCommandHandler.cs:131-140`
- Test: `test/SEBT.Portal.Tests/Unit/UseCases/Household/UpdateAddressCommandHandlerTests.cs`

- [ ] **Step 1: Update existing tests to use IsCoLoaded instead of BenefitIssuanceType**

Replace the four existing benefit-type tests with IsCoLoaded equivalents. First, replace the `SetupHouseholdWithBenefitType` helper:

```csharp
private void SetupHouseholdWithCases(params SummerEbtCase[] cases)
{
    _householdRepository.GetHouseholdByIdentifierAsync(
            Arg.Any<HouseholdIdentifier>(), Arg.Any<PiiVisibility>(),
            Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
        .Returns(new HouseholdData { SummerEbtCases = cases.ToList() });
}
```

Then replace the four test methods `Handle_ReturnsPreconditionFailed_WhenHouseholdIsSnapBenefitType`, `Handle_ReturnsPreconditionFailed_WhenHouseholdIsTanfBenefitType`, `Handle_AllowsUpdate_WhenHouseholdIsSummerEbtBenefitType`, and `Handle_AllowsUpdate_WhenHouseholdIsUnknownBenefitType` with:

```csharp
[Fact]
public async Task Handle_ReturnsPreconditionFailed_WhenAnyCaseIsCoLoaded()
{
    var handler = CreateHandler();
    var command = CreateValidCommand();

    SetupResolverReturnsEmail();
    SetupHouseholdWithCases(
        new SummerEbtCase { SummerEBTCaseID = "S1", ChildFirstName = "A", ChildLastName = "B", IsCoLoaded = true });

    var result = await handler.Handle(command, CancellationToken.None);

    Assert.False(result.IsSuccess);
    var preconditionFailed = Assert.IsType<PreconditionFailedResult<AddressValidationResult>>(result);
    Assert.Equal(PreconditionFailedReason.Conflict, preconditionFailed.Reason);
}

[Fact]
public async Task Handle_ReturnsPreconditionFailed_WhenMixedCoLoadedAndNonCoLoaded()
{
    var handler = CreateHandler();
    var command = CreateValidCommand();

    SetupResolverReturnsEmail();
    SetupHouseholdWithCases(
        new SummerEbtCase { SummerEBTCaseID = "S1", ChildFirstName = "A", ChildLastName = "B", IsCoLoaded = true },
        new SummerEbtCase { SummerEBTCaseID = "S2", ChildFirstName = "C", ChildLastName = "D", IsCoLoaded = false });

    var result = await handler.Handle(command, CancellationToken.None);

    Assert.False(result.IsSuccess);
    var preconditionFailed = Assert.IsType<PreconditionFailedResult<AddressValidationResult>>(result);
    Assert.Equal(PreconditionFailedReason.Conflict, preconditionFailed.Reason);
}

[Fact]
public async Task Handle_AllowsUpdate_WhenNoCasesAreCoLoaded()
{
    var handler = CreateHandler();
    var command = CreateValidCommand();

    SetupResolverReturnsEmail();
    SetupHouseholdWithCases(
        new SummerEbtCase { SummerEBTCaseID = "S1", ChildFirstName = "A", ChildLastName = "B", IsCoLoaded = false });

    var result = await handler.Handle(command, CancellationToken.None);

    Assert.True(result.IsSuccess);
}

[Fact]
public async Task Handle_AllowsUpdate_WhenNoCasesExist()
{
    var handler = CreateHandler();
    var command = CreateValidCommand();

    SetupResolverReturnsEmail();
    SetupHouseholdWithCases(); // empty

    var result = await handler.Handle(command, CancellationToken.None);

    Assert.True(result.IsSuccess);
}
```

Also remove the `SetupHouseholdWithBenefitType` helper method and the `BenefitIssuanceType` using alias if no longer needed.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test test/SEBT.Portal.Tests --filter "FullyQualifiedName~UpdateAddressCommandHandlerTests.Handle_ReturnsPreconditionFailed_WhenAnyCaseIsCoLoaded" --no-build 2>&1 | tail -5`
Expected: FAIL

- [ ] **Step 3: Update the handler to check IsCoLoaded**

In `UpdateAddressCommandHandler.cs`, replace lines 131-140 (the `BenefitIssuanceType` check):

```csharp
        if (household is { BenefitIssuanceType: BenefitIssuanceType.SnapEbtCard or BenefitIssuanceType.TanfEbtCard })
        {
            logger.LogWarning(
                "Address update rejected for household identifier kind {Kind}: benefit type {BenefitType} is not eligible for portal self-service",
                identifierKind,
                household.BenefitIssuanceType);
            return Result<AddressValidationResult>.PreconditionFailed(
                PreconditionFailedReason.Conflict,
                "Address updates are not available for this benefit type. Please contact your case worker.");
        }
```

With:

```csharp
        if (household.SummerEbtCases.Any(c => c.IsCoLoaded))
        {
            logger.LogWarning(
                "Address update rejected for household identifier kind {Kind}: household contains co-loaded cases",
                identifierKind);
            return Result<AddressValidationResult>.PreconditionFailed(
                PreconditionFailedReason.Conflict,
                "Address updates are not available for co-loaded benefits. Please contact your case worker.");
        }
```

Also remove the now-unused `using BenefitIssuanceType` import if nothing else references it.

- [ ] **Step 4: Run all handler tests to verify they pass**

Run: `dotnet test test/SEBT.Portal.Tests --filter "FullyQualifiedName~UpdateAddressCommandHandlerTests" --no-build 2>&1 | tail -5`
Expected: ALL PASS

- [ ] **Step 5: Commit**

```
feat: check IsCoLoaded instead of BenefitIssuanceType for address updates

The canonical signal for "managed by caseworker" is IsCoLoaded on the
case, not the household-level BenefitIssuanceType enum. This aligns
the address update guard with the co-loaded filtering in GetHouseholdData.
```

---

## Task 3: Reject co-loaded case IDs in RequestCardReplacementCommandHandler

### Context

`RequestCardReplacementCommandHandler` accepts a list of case IDs. It currently validates cooldown and case ownership but does not check `IsCoLoaded`. A co-loaded case ID should be rejected.

**Files:**
- Modify: `src/SEBT.Portal.UseCases/Household/RequestCardReplacement/RequestCardReplacementCommandHandler.cs`
- Test: `test/SEBT.Portal.Tests/Unit/UseCases/Household/RequestCardReplacementCommandHandlerTests.cs`

- [ ] **Step 1: Write failing test — co-loaded case ID rejected**

Add to `RequestCardReplacementCommandHandlerTests`:

```csharp
[Fact]
public async Task Handle_ReturnsConflict_WhenRequestedCaseIsCoLoaded()
{
    var handler = CreateHandler();
    var command = CreateValidCommand(caseIds: new List<string> { "SEBT-COLOADED" });
    SetupResolverSuccess();
    SetupRepositoryReturns(CreateHouseholdWithCases(
        new SummerEbtCase
        {
            SummerEBTCaseID = "SEBT-COLOADED",
            ChildFirstName = "John",
            ChildLastName = "Doe",
            IsCoLoaded = true,
            CardRequestedAt = DateTime.UtcNow.AddDays(-30)
        }
    ));

    var result = await handler.Handle(command, CancellationToken.None);

    Assert.False(result.IsSuccess);
    var preconditionFailed = Assert.IsType<PreconditionFailedResult>(result);
    Assert.Equal(PreconditionFailedReason.Conflict, preconditionFailed.Reason);
}
```

- [ ] **Step 2: Write failing test — mixed batch rejects when any case is co-loaded**

```csharp
[Fact]
public async Task Handle_ReturnsConflict_WhenAnyRequestedCaseIsCoLoaded()
{
    var handler = CreateHandler();
    var command = CreateValidCommand(caseIds: new List<string> { "SEBT-001", "SEBT-COLOADED" });
    SetupResolverSuccess();
    SetupRepositoryReturns(CreateHouseholdWithCases(
        new SummerEbtCase
        {
            SummerEBTCaseID = "SEBT-001",
            ChildFirstName = "Regular",
            ChildLastName = "Child",
            IsCoLoaded = false,
            CardRequestedAt = DateTime.UtcNow.AddDays(-30)
        },
        new SummerEbtCase
        {
            SummerEBTCaseID = "SEBT-COLOADED",
            ChildFirstName = "CoLoaded",
            ChildLastName = "Child",
            IsCoLoaded = true,
            CardRequestedAt = DateTime.UtcNow.AddDays(-30)
        }
    ));

    var result = await handler.Handle(command, CancellationToken.None);

    Assert.False(result.IsSuccess);
    var preconditionFailed = Assert.IsType<PreconditionFailedResult>(result);
    Assert.Equal(PreconditionFailedReason.Conflict, preconditionFailed.Reason);
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test test/SEBT.Portal.Tests --filter "FullyQualifiedName~RequestCardReplacementCommandHandlerTests.Handle_ReturnsConflict" --no-build 2>&1 | tail -5`
Expected: FAIL

- [ ] **Step 4: Implement co-loaded guard in the handler**

In `RequestCardReplacementCommandHandler.cs`, after the IAL check (line 71) and before the cooldown check (line 73), add:

```csharp
        // Co-loaded cases are managed by caseworkers, not the portal.
        var requestedCases = household.SummerEbtCases
            .Where(c => command.CaseIds.Contains(c.SummerEBTCaseID));
        if (requestedCases.Any(c => c.IsCoLoaded))
        {
            logger.LogWarning(
                "Card replacement rejected: request includes co-loaded case(s)");
            return Result.PreconditionFailed(
                PreconditionFailedReason.Conflict,
                "Card replacements are not available for co-loaded benefits. Please contact your case worker.");
        }
```

- [ ] **Step 5: Run all card replacement tests to verify they pass**

Run: `dotnet test test/SEBT.Portal.Tests --filter "FullyQualifiedName~RequestCardReplacementCommandHandlerTests" --no-build 2>&1 | tail -5`
Expected: ALL PASS

- [ ] **Step 6: Commit**

```
feat: reject card replacement requests for co-loaded cases

Co-loaded case IDs in a card replacement request now return 409 Conflict.
Aligns with the address update guard and co-loaded filtering.
```

---

## Task 4: Add per-case feature flags to API response

### Context

Add `AllowAddressChange` and `AllowCardReplacement` to `SummerEbtCaseResponse`, derived from `!IsCoLoaded`. After Task 1 filtering, all cases reaching the mapper will be non-co-loaded (so these will be `true`), but the flags are still valuable: they decouple the frontend from knowing *why* an action is allowed, and they'll support future per-case gating (e.g., cooldown-based card replacement restrictions).

**Files:**
- Modify: `src/SEBT.Portal.Api/Models/Household/SummerEbtCaseResponse.cs`
- Modify: `src/SEBT.Portal.Api/Models/Household/HouseholdDataResponseMapper.cs:34-59`

- [ ] **Step 1: Add properties to SummerEbtCaseResponse**

Add these two properties at the end of the record (before the closing brace), after `BenefitExpirationDate`:

```csharp
    /// <summary>
    /// Whether the portal user can change the mailing address for this case.
    /// False for co-loaded cases (benefits managed by caseworker).
    /// </summary>
    public bool AllowAddressChange { get; init; }

    /// <summary>
    /// Whether the portal user can request a replacement card for this case.
    /// False for co-loaded cases (benefits managed by caseworker).
    /// </summary>
    public bool AllowCardReplacement { get; init; }
```

- [ ] **Step 2: Map the flags in HouseholdDataResponseMapper**

In the `ToResponse(SummerEbtCase domain)` method, add the two new mappings at the end of the initializer (after `BenefitExpirationDate = domain.BenefitExpirationDate`):

```csharp
            AllowAddressChange = !domain.IsCoLoaded,
            AllowCardReplacement = !domain.IsCoLoaded
```

- [ ] **Step 3: Build to verify compilation**

Run: `dotnet build src/SEBT.Portal.Api/SEBT.Portal.Api.csproj --no-restore 2>&1 | tail -5`
Expected: Build succeeded

- [ ] **Step 4: Run existing tests to verify no regressions**

Run: `dotnet test test/SEBT.Portal.Tests --no-build 2>&1 | tail -5`
Expected: ALL PASS

- [ ] **Step 5: Commit**

```
feat: add AllowAddressChange and AllowCardReplacement to case response

Per-case feature flags derived from IsCoLoaded. Decouples the frontend
from needing to know the business logic behind feature availability.
```

---

## Task 5: Fix mock data — Review scenario address and Verified scenario IsCoLoaded

### Context

Two mock data fixes:
1. The Review scenario (`sebt.dc+review`) has no mailing address — it should have one (it's a SummerEbt household that should be able to change address).
2. The Verified scenario (`sebt.dc+verified`) has SnapEbtCard cases but `IsCoLoaded` defaults to `false` — set it to `true` so the co-loaded filtering works correctly.

**Files:**
- Modify: `src/SEBT.Portal.Infrastructure/Repositories/MockHouseholdRepository.cs`

- [ ] **Step 1: Add mailing address to the Review scenario**

In `MockHouseholdRepository.cs`, inside the Review scenario builder (around line 295, after `h.BenefitIssuanceType = BenefitIssuanceType.SummerEbt;`), add:

```csharp
            h.AddressOnFile = new Address
            {
                StreetAddress1 = "700 14th Street NW",
                StreetAddress2 = "Unit 2",
                City = "Washington",
                State = "DC",
                PostalCode = "20005"
            };
```

- [ ] **Step 2: Set IsCoLoaded = true on Verified scenario cases**

In the Verified scenario (around line 224), the two `CreateSummerEbtCase` calls use a `customize` lambda. Add `c.IsCoLoaded = true;` to each. The first case becomes:

```csharp
                HouseholdFactory.CreateSummerEbtCase("John", "Doe", "Application", c =>
                {
                    c.IssuanceType = IssuanceType.SnapEbtCard;
                    c.IsCoLoaded = true;
                    c.BenefitAvailableDate = appBenefitStart;
                    c.BenefitExpirationDate = appBenefitStart.AddDays(122);
                }),
```

And the second:

```csharp
                HouseholdFactory.CreateSummerEbtCase("Jane", "Doe", "Application", c =>
                {
                    c.IssuanceType = IssuanceType.SnapEbtCard;
                    c.IsCoLoaded = true;
                    c.BenefitAvailableDate = appBenefitStart;
                    c.BenefitExpirationDate = appBenefitStart.AddDays(122);
                })
```

- [ ] **Step 3: Build to verify**

Run: `dotnet build src/SEBT.Portal.Api/SEBT.Portal.Api.csproj --no-restore 2>&1 | tail -5`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```
fix: add address to Review mock, set IsCoLoaded on Verified mock cases

Review scenario now has a DC mailing address for testing address change.
Verified scenario's SnapEbtCard cases are marked co-loaded so the
filtering and feature flags work correctly in local dev.
```

---

## Task 6: Add per-case flags to frontend schema

### Context

The frontend Zod schema needs the two new boolean fields so TypeScript picks them up.

**Files:**
- Modify: `src/SEBT.Portal.Web/src/features/household/api/schema.ts`

- [ ] **Step 1: Add fields to SummerEbtCaseSchema**

In `schema.ts`, add these two fields to the `SummerEbtCaseSchema` object, after `cardDeactivatedAt`:

```typescript
  allowAddressChange: z.boolean().optional().default(true),
  allowCardReplacement: z.boolean().optional().default(true),
```

The `.optional().default(true)` ensures backward compatibility — if the API hasn't been updated yet or the field is missing, the UI defaults to allowing the action.

- [ ] **Step 2: Verify TypeScript compiles**

Run: `cd src/SEBT.Portal.Web && npx tsc --noEmit 2>&1 | tail -5`
Expected: No errors

- [ ] **Step 3: Commit**

```
feat: add allowAddressChange and allowCardReplacement to case schema
```

---

## Task 7: Gate "Change Address" link in HouseholdSummary

### Context

The "Change my mailing address" link currently shows whenever `addressOnFile` exists. It should only show when at least one case has `allowAddressChange === true`. The address itself should always be displayed.

**Files:**
- Modify: `src/SEBT.Portal.Web/src/features/household/components/HouseholdSummary/HouseholdSummary.tsx:124-139`
- Test: `src/SEBT.Portal.Web/src/features/household/components/HouseholdSummary/HouseholdSummary.test.tsx`

- [ ] **Step 1: Write failing test — link hidden when no case allows address change**

Add to `HouseholdSummary.test.tsx`:

```typescript
it('hides change address link when no case allows address change', () => {
  const coLoadedCase: SummerEbtCase = {
    ...mockCase,
    allowAddressChange: false,
    allowCardReplacement: false
  }
  mockReturnData = {
    ...defaultMockData,
    summerEbtCases: [coLoadedCase]
  }
  render(<HouseholdSummary />)
  expect(screen.getByText('Your mailing address')).toBeInTheDocument()
  expect(screen.getByText(/1350 Pennsylvania Ave NW/)).toBeInTheDocument()
  expect(screen.queryByRole('link', { name: 'Change my mailing address' })).not.toBeInTheDocument()
})
```

- [ ] **Step 2: Write failing test — link shown when any case allows address change**

```typescript
it('shows change address link when any case allows address change', () => {
  const allowedCase: SummerEbtCase = {
    ...mockCase,
    allowAddressChange: true,
    allowCardReplacement: true
  }
  mockReturnData = {
    ...defaultMockData,
    summerEbtCases: [allowedCase]
  }
  render(<HouseholdSummary />)
  expect(screen.getByRole('link', { name: 'Change my mailing address' })).toBeInTheDocument()
})
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `cd src/SEBT.Portal.Web && pnpm vitest run src/features/household/components/HouseholdSummary/HouseholdSummary.test.tsx 2>&1 | tail -10`
Expected: At least one FAIL (the "hides" test)

- [ ] **Step 4: Update HouseholdSummary to check the flag**

In `HouseholdSummary.tsx`, replace the address section (lines 123-139):

```tsx
          {/* Your mailing address */}
          {data.addressOnFile && (
            <>
              <dt className="text-bold">{t('profileTableHeadingAddress')}</dt>
              <dd className="margin-left-0 margin-bottom-2">
                <span style={{ whiteSpace: 'pre-line' }}>{formatAddress(data.addressOnFile)}</span>
                <br />
                <Link
                  href="/profile/address"
                  data-analytics-cta="update_address_cta"
                  className="usa-link"
                >
                  {t('profileTableActionChangeAddress')}
                </Link>
              </dd>
            </>
          )}
```

With:

```tsx
          {/* Your mailing address */}
          {data.addressOnFile && (
            <>
              <dt className="text-bold">{t('profileTableHeadingAddress')}</dt>
              <dd className="margin-left-0 margin-bottom-2">
                <span style={{ whiteSpace: 'pre-line' }}>{formatAddress(data.addressOnFile)}</span>
                {data.summerEbtCases.some((c) => c.allowAddressChange) && (
                  <>
                    <br />
                    <Link
                      href="/profile/address"
                      data-analytics-cta="update_address_cta"
                      className="usa-link"
                    >
                      {t('profileTableActionChangeAddress')}
                    </Link>
                  </>
                )}
              </dd>
            </>
          )}
```

- [ ] **Step 5: Update existing test mock data for compatibility**

The existing `mockCase` in the test file doesn't include the new fields. Since the schema defaults to `true`, existing tests should still pass. Verify:

Run: `cd src/SEBT.Portal.Web && pnpm vitest run src/features/household/components/HouseholdSummary/HouseholdSummary.test.tsx 2>&1 | tail -10`
Expected: ALL PASS

- [ ] **Step 6: Commit**

```
feat: gate change-address link on per-case allowAddressChange flag

Address is still displayed; only the action link is gated. The frontend
reads the API flag instead of duplicating benefit-type logic.
```

---

## Task 8: Guard the address form page

### Context

Direct navigation to `/profile/address` should redirect to `/profile` when no case allows address change. This replaces the TODO at the top of the file.

**Files:**
- Modify: `src/SEBT.Portal.Web/src/app/(authenticated)/profile/address/(flow)/page.tsx`

- [ ] **Step 1: Update the page component**

Replace the D9 TODO and add the eligibility guard. Keep the DC-153 TODO (it's unrelated — separate ticket).

Replace the existing imports and the D9 TODO comment (lines 1-10) with:

```tsx
'use client'

import { useRouter } from 'next/navigation'
import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'

import { AddressForm } from '@/features/address/components/AddressForm'
import { useHouseholdData } from '@/features/household'
```

Replace the D9 TODO comment (lines 8-10) with nothing — it's resolved by the guard below.

Keep the DC-153 TODO comment as-is:
```tsx
// TODO (DC-153): Card-flow entry point — when accessed via /profile/address?from=cards,
// the form should return the user to the card replacement flow on completion
// instead of the replacement card prompt. See DC-02/CO-01 mockups.
```

Replace the `AddressFormPage` function body with:

```tsx
export default function AddressFormPage() {
  const { t } = useTranslation('confirmInfo')
  const { data, isLoading } = useHouseholdData()
  const router = useRouter()

  const canChangeAddress = data?.summerEbtCases.some((c) => c.allowAddressChange) ?? false

  useEffect(() => {
    if (!isLoading && !canChangeAddress) {
      router.replace('/profile')
    }
  }, [isLoading, canChangeAddress, router])

  if (isLoading || !canChangeAddress) {
    return (
      <div
        aria-busy="true"
        role="status"
      >
        <span className="usa-sr-only">Loading…</span>
      </div>
    )
  }

  return (
    <div className="grid-container maxw-tablet padding-top-4 padding-bottom-4">
      <h1 className="font-sans-xl text-primary">
        {t('pageTitle', 'Tell us where to safely send your mail')}
      </h1>
      <p className="usa-hint">
        {t('requiredFieldsNote', 'Asterisks (*) indicate a required field')}
      </p>
      <AddressForm initialAddress={data?.addressOnFile ?? null} />
    </div>
  )
}
```

- [ ] **Step 2: Verify TypeScript compiles**

Run: `cd src/SEBT.Portal.Web && npx tsc --noEmit 2>&1 | tail -5`
Expected: No errors

- [ ] **Step 3: Commit**

```
feat: redirect to /profile when address change is not allowed

Resolves TODO (D9) — guards the address form page using the per-case
allowAddressChange flag from the API. DC-153 TODO preserved (separate ticket).
```

---

## Task 9: Run full test suites

- [ ] **Step 1: Run all backend tests**

Run: `dotnet test test/SEBT.Portal.Tests 2>&1 | tail -10`
Expected: ALL PASS

- [ ] **Step 2: Run frontend tests**

Run: `cd src/SEBT.Portal.Web && pnpm test 2>&1 | tail -10`
Expected: ALL PASS

- [ ] **Step 3: Run frontend lint**

Run: `cd src/SEBT.Portal.Web && pnpm lint 2>&1 | tail -5`
Expected: No errors

- [ ] **Step 4: Fix any failures, then commit fixes if needed**

---

## Notes for implementer

- **Do not modify Core layer models.** `IsCoLoaded` already exists on `SummerEbtCase` in Core. No domain model changes needed.
- **The `BenefitIssuanceType` field on `HouseholdDataResponse`** is left as-is. Other parts of the UI may still use it. Cleanup is a separate task.
- **The DC-153 TODO** (card-flow entry point) must be preserved in the address form page. It's unrelated to this work.
- **The `MinimumIalService`** uses `IsCoLoaded` for IAL determination. Its logic reads the full (unfiltered) case list from the repository, which is called *before* the filtering in `GetHouseholdDataQueryHandler`. This is correct — IAL checks need the full picture.
