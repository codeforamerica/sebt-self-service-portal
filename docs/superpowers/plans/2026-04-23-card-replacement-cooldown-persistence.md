# Card Replacement Cooldown Persistence — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist card replacement requests in the portal database so the 14-day cooldown is enforced across sessions, independent of state connector data.

**Architecture:** A new `CardReplacementRequestEntity` table stores HMAC-SHA256 hashed household/case identifiers with timestamps. The `RequestCardReplacementCommandHandler` writes to this table after validation passes. The handler's `CheckCooldown` method queries this table instead of relying on the (always-null) state connector `CardRequestedAt`. The `SsnNormalizer` is renamed to `IdentifierNormalizer` to reflect its general-purpose usage.

**Tech Stack:** C# / .NET 10, EF Core, xUnit, NSubstitute, Bogus

**Jira:** DC-153

---

## File Structure

| File | Action | Responsibility |
|------|--------|----------------|
| `src/SEBT.Portal.Core/Utilities/SsnNormalizer.cs` | Rename | Becomes `IdentifierNormalizer.cs` — general-purpose whitespace/dash stripping |
| `test/.../Unit/Utilities/SsnNormalizerTests.cs` | Rename | Becomes `IdentifierNormalizerTests.cs` |
| `src/SEBT.Portal.Infrastructure/Services/IdentifierHasher.cs` | Modify | Update `SsnNormalizer` reference to `IdentifierNormalizer` |
| `src/SEBT.Portal.Infrastructure/Data/Entities/CardReplacementRequestEntity.cs` | Create | EF entity with Guid PK, hashed identifiers, timestamps |
| `src/SEBT.Portal.Infrastructure/Data/PortalDbContext.cs` | Modify | Register new `DbSet`, configure table/indexes |
| `src/SEBT.Portal.Core/Repositories/ICardReplacementRequestRepository.cs` | Create | Interface: `HasRecentRequestAsync`, `CreateAsync` |
| `src/SEBT.Portal.Infrastructure/Repositories/CardReplacementRequestRepository.cs` | Create | EF implementation of the repository |
| `src/SEBT.Portal.UseCases/Household/RequestCardReplacement/RequestCardReplacementCommandHandler.cs` | Modify | Inject repository + hasher, persist after validation, query for cooldown |
| `src/SEBT.Portal.Infrastructure/Dependencies.cs` | Modify | Register `ICardReplacementRequestRepository` |
| EF Migration (auto-generated) | Create | `CardReplacementRequests` table + composite index |
| `test/.../Unit/UseCases/Household/RequestCardReplacementCommandHandlerTests.cs` | Modify | Add repository mock, test persistence and cooldown-from-DB |
| `test/.../Unit/Repositories/CardReplacementRequestRepositoryTests.cs` | Create | Integration tests for the repository |

---

### Task 1: Rename `SsnNormalizer` to `IdentifierNormalizer`

The normalizer strips dashes and whitespace — nothing SSN-specific. Renaming it clarifies its general-purpose role before we start using it for case IDs and household identifiers.

**Files:**
- Rename: `src/SEBT.Portal.Core/Utilities/SsnNormalizer.cs` → `IdentifierNormalizer.cs`
- Rename: `test/SEBT.Portal.Tests/Unit/Utilities/SsnNormalizerTests.cs` → `IdentifierNormalizerTests.cs`
- Modify: `src/SEBT.Portal.Infrastructure/Services/IdentifierHasher.cs`

- [ ] **Step 1: Rename the source file and class**

Rename `src/SEBT.Portal.Core/Utilities/SsnNormalizer.cs` to `IdentifierNormalizer.cs`. Update the class name and all XML doc references from "SSN" to "identifier":

```csharp
namespace SEBT.Portal.Core.Utilities;

/// <summary>
/// Utility class for normalizing identifier values to ensure consistent storage and comparison.
/// Strips dashes and spaces to produce a canonical form.
/// </summary>
public static class IdentifierNormalizer
{
    /// <summary>
    /// Normalizes an identifier by trimming whitespace and removing dashes and spaces.
    /// </summary>
    /// <param name="value">The identifier to normalize.</param>
    /// <returns>The normalized identifier.</returns>
    /// <exception cref="ArgumentException">Thrown when the value is null or whitespace.</exception>
    public static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));

        return NormalizeCore(value);
    }

    /// <summary>
    /// Normalizes an identifier by trimming whitespace and removing dashes and spaces,
    /// or returns null if the input is null or whitespace.
    /// </summary>
    /// <param name="value">The identifier to normalize, or null/whitespace.</param>
    /// <returns>The normalized identifier, or null if the input was null or whitespace.</returns>
    public static string? NormalizeOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return NormalizeCore(value);
    }

    private static string NormalizeCore(string value) =>
        value.Trim().Replace("-", "").Replace(" ", "");
}
```

- [ ] **Step 2: Update `IdentifierHasher` to reference `IdentifierNormalizer`**

In `src/SEBT.Portal.Infrastructure/Services/IdentifierHasher.cs`, change line 33:

```csharp
// Before:
var normalized = SsnNormalizer.NormalizeOrNull(plaintext);
// After:
var normalized = IdentifierNormalizer.NormalizeOrNull(plaintext);
```

Update the `using` statement if needed (namespace is the same — `SEBT.Portal.Core.Utilities`).

- [ ] **Step 3: Rename the test file and class**

Rename `test/SEBT.Portal.Tests/Unit/Utilities/SsnNormalizerTests.cs` to `IdentifierNormalizerTests.cs`. Update the class name to `IdentifierNormalizerTests` and all `SsnNormalizer.` references to `IdentifierNormalizer.`:

```csharp
namespace SEBT.Portal.Tests.Unit.Utilities;

public class IdentifierNormalizerTests
{
    [Fact]
    public void NormalizeOrNull_RemovesDashes()
    {
        var result = IdentifierNormalizer.NormalizeOrNull("123-45-6789");
        Assert.Equal("123456789", result);
    }

    [Fact]
    public void NormalizeOrNull_RemovesSpaces()
    {
        var result = IdentifierNormalizer.NormalizeOrNull("123 45 6789");
        Assert.Equal("123456789", result);
    }

    [Fact]
    public void NormalizeOrNull_TrimsWhitespace()
    {
        var result = IdentifierNormalizer.NormalizeOrNull("  123456789  ");
        Assert.Equal("123456789", result);
    }

    [Fact]
    public void NormalizeOrNull_ReturnsNull_WhenNull()
    {
        var result = IdentifierNormalizer.NormalizeOrNull(null);
        Assert.Null(result);
    }

    [Fact]
    public void NormalizeOrNull_ReturnsNull_WhenWhitespace()
    {
        var result = IdentifierNormalizer.NormalizeOrNull("   ");
        Assert.Null(result);
    }

    [Fact]
    public void Normalize_RemovesDashes()
    {
        var result = IdentifierNormalizer.Normalize("123-45-6789");
        Assert.Equal("123456789", result);
    }

    [Fact]
    public void Normalize_ThrowsOnNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => IdentifierNormalizer.Normalize(null!));
        Assert.Equal("value", ex.ParamName);
    }

    [Fact]
    public void Normalize_ThrowsOnWhitespace()
    {
        Assert.Throws<ArgumentException>(() => IdentifierNormalizer.Normalize("   "));
    }
}
```

- [ ] **Step 4: Run tests to verify the rename is clean**

Run: `dotnet test test/SEBT.Portal.Tests/SEBT.Portal.Tests.csproj --filter "FullyQualifiedName~IdentifierNormalizerTests" -v minimal`

Expected: All 8 tests pass.

Then run the broader unit test suite to confirm nothing else broke:

Run: `pnpm api:test:unit`

Expected: All 860+ tests pass.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor: rename SsnNormalizer to IdentifierNormalizer

The normalizer strips dashes and whitespace — nothing SSN-specific.
Renaming clarifies its general-purpose role for hashing any identifier
(case IDs, household identifiers, SSNs)."
```

---

### Task 2: Create `CardReplacementRequestEntity` and DB configuration

**Files:**
- Create: `src/SEBT.Portal.Infrastructure/Data/Entities/CardReplacementRequestEntity.cs`
- Modify: `src/SEBT.Portal.Infrastructure/Data/PortalDbContext.cs`

- [ ] **Step 1: Create the entity**

Create `src/SEBT.Portal.Infrastructure/Data/Entities/CardReplacementRequestEntity.cs`:

```csharp
namespace SEBT.Portal.Infrastructure.Data.Entities;

/// <summary>
/// Records a card replacement request for cooldown enforcement.
/// Household and case identifiers are stored as HMAC-SHA256 hashes (via IIdentifierHasher)
/// because only lookup-by-hash is needed — original values are never retrieved.
/// </summary>
public class CardReplacementRequestEntity
{
    /// <summary>
    /// Primary key (UUIDv7, application-generated).
    /// </summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// HMAC-SHA256 hash of the household identifier value.
    /// 64-character hex string produced by <see cref="Core.Services.IIdentifierHasher"/>.
    /// </summary>
    public string HouseholdIdentifierHash { get; set; } = string.Empty;

    /// <summary>
    /// HMAC-SHA256 hash of the Summer EBT case ID.
    /// 64-character hex string produced by <see cref="Core.Services.IIdentifierHasher"/>.
    /// </summary>
    public string CaseIdHash { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the card replacement was requested.
    /// </summary>
    public DateTime RequestedAt { get; set; }

    /// <summary>
    /// Foreign key to the user who made the request. Audit trail only.
    /// </summary>
    public Guid RequestedByUserId { get; set; }

    /// <summary>
    /// Navigation property to the requesting user.
    /// </summary>
    public UserEntity? RequestedByUser { get; set; }
}
```

- [ ] **Step 2: Register DbSet and configure the table in PortalDbContext**

In `src/SEBT.Portal.Infrastructure/Data/PortalDbContext.cs`:

Add the DbSet property after the existing ones:

```csharp
/// <summary>
/// Card replacement request records for cooldown enforcement.
/// </summary>
public DbSet<CardReplacementRequestEntity> CardReplacementRequests { get; set; }
```

Add the entity configuration inside `OnModelCreating`, after the `DeidentifiedChildResultEntity` block:

```csharp
modelBuilder.Entity<CardReplacementRequestEntity>(entity =>
{
    entity.ToTable("CardReplacementRequests");
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Id).ValueGeneratedNever();

    entity.Property(e => e.HouseholdIdentifierHash)
        .IsRequired()
        .HasMaxLength(64);

    entity.Property(e => e.CaseIdHash)
        .IsRequired()
        .HasMaxLength(64);

    entity.Property(e => e.RequestedAt)
        .IsRequired();

    entity.Property(e => e.RequestedByUserId)
        .IsRequired();

    entity.HasOne(e => e.RequestedByUser)
        .WithMany()
        .HasForeignKey(e => e.RequestedByUserId)
        .OnDelete(DeleteBehavior.Cascade);

    // Composite index covering the cooldown lookup query:
    // WHERE HouseholdIdentifierHash = @hash AND CaseIdHash = @hash AND RequestedAt > @cutoff
    entity.HasIndex(e => new { e.HouseholdIdentifierHash, e.CaseIdHash, e.RequestedAt })
        .HasDatabaseName("IX_CardReplacementRequests_Household_Case_RequestedAt");
});
```

- [ ] **Step 3: Build to verify compilation**

Run: `pnpm api:build`

Expected: Build succeeds with 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/SEBT.Portal.Infrastructure/Data/Entities/CardReplacementRequestEntity.cs
git add src/SEBT.Portal.Infrastructure/Data/PortalDbContext.cs
git commit -m "feat: add CardReplacementRequestEntity with hashed identifiers

New entity for tracking card replacement requests in the portal DB.
Stores HMAC-SHA256 hashes of household identifier and case ID (never
cleartext PII). Composite index covers the cooldown lookup query."
```

---

### Task 3: Generate EF Core migration

**Files:**
- Create: `src/SEBT.Portal.Infrastructure/Data/Migrations/<timestamp>_AddCardReplacementRequests.cs` (auto-generated)

- [ ] **Step 1: Generate the migration**

Run:

```bash
dotnet ef migrations add AddCardReplacementRequests \
  --project src/SEBT.Portal.Infrastructure/SEBT.Portal.Infrastructure.csproj \
  --startup-project src/SEBT.Portal.Api/SEBT.Portal.Api.csproj
```

Expected: Migration file created in `src/SEBT.Portal.Infrastructure/Data/Migrations/`.

- [ ] **Step 2: Review the generated migration**

Open the generated migration file and verify it contains:
- `CreateTable("CardReplacementRequests")` with columns: `Id` (uniqueidentifier), `HouseholdIdentifierHash` (nvarchar(64)), `CaseIdHash` (nvarchar(64)), `RequestedAt` (datetime2), `RequestedByUserId` (uniqueidentifier)
- Primary key on `Id`
- Foreign key to `Users` on `RequestedByUserId`
- Index named `IX_CardReplacementRequests_Household_Case_RequestedAt` on `(HouseholdIdentifierHash, CaseIdHash, RequestedAt)`

If anything looks wrong, delete the migration and regenerate after fixing the DbContext config.

- [ ] **Step 3: Build to verify the migration compiles**

Run: `pnpm api:build`

Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add src/SEBT.Portal.Infrastructure/Data/Migrations/
git commit -m "migration: add CardReplacementRequests table"
```

---

### Task 4: Create `ICardReplacementRequestRepository` and implementation

**Files:**
- Create: `src/SEBT.Portal.Core/Repositories/ICardReplacementRequestRepository.cs`
- Create: `src/SEBT.Portal.Infrastructure/Repositories/CardReplacementRequestRepository.cs`
- Modify: `src/SEBT.Portal.Infrastructure/Dependencies.cs`

- [ ] **Step 1: Write the failing test for `HasRecentRequestAsync`**

Create `test/SEBT.Portal.Tests/Unit/Repositories/CardReplacementRequestRepositoryTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Data.Entities;
using SEBT.Portal.Infrastructure.Repositories;

namespace SEBT.Portal.Tests.Unit.Repositories;

public class CardReplacementRequestRepositoryTests : IDisposable
{
    private readonly PortalDbContext _dbContext;
    private readonly CardReplacementRequestRepository _repository;

    // Deterministic hash values for test isolation
    private const string HouseholdHash = "A1B2C3D4E5F6A1B2C3D4E5F6A1B2C3D4E5F6A1B2C3D4E5F6A1B2C3D4E5F6A1B2";
    private const string CaseHash = "1234567890ABCDEF1234567890ABCDEF1234567890ABCDEF1234567890ABCDEF";
    private const string OtherCaseHash = "FEDCBA0987654321FEDCBA0987654321FEDCBA0987654321FEDCBA0987654321";

    public CardReplacementRequestRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new PortalDbContext(options);

        // Seed a user for FK constraint
        _dbContext.Users.Add(new UserEntity { Id = TestUserId });
        _dbContext.SaveChanges();

        _repository = new CardReplacementRequestRepository(_dbContext);
    }

    private static readonly Guid TestUserId = Guid.CreateVersion7();

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task HasRecentRequestAsync_ReturnsFalse_WhenNoRequests()
    {
        var result = await _repository.HasRecentRequestAsync(
            HouseholdHash, CaseHash, TimeSpan.FromDays(14));

        Assert.False(result);
    }

    [Fact]
    public async Task HasRecentRequestAsync_ReturnsTrue_WhenRequestWithinCooldown()
    {
        _dbContext.CardReplacementRequests.Add(new CardReplacementRequestEntity
        {
            HouseholdIdentifierHash = HouseholdHash,
            CaseIdHash = CaseHash,
            RequestedAt = DateTime.UtcNow.AddDays(-3),
            RequestedByUserId = TestUserId
        });
        await _dbContext.SaveChangesAsync();

        var result = await _repository.HasRecentRequestAsync(
            HouseholdHash, CaseHash, TimeSpan.FromDays(14));

        Assert.True(result);
    }

    [Fact]
    public async Task HasRecentRequestAsync_ReturnsFalse_WhenRequestOutsideCooldown()
    {
        _dbContext.CardReplacementRequests.Add(new CardReplacementRequestEntity
        {
            HouseholdIdentifierHash = HouseholdHash,
            CaseIdHash = CaseHash,
            RequestedAt = DateTime.UtcNow.AddDays(-15),
            RequestedByUserId = TestUserId
        });
        await _dbContext.SaveChangesAsync();

        var result = await _repository.HasRecentRequestAsync(
            HouseholdHash, CaseHash, TimeSpan.FromDays(14));

        Assert.False(result);
    }

    [Fact]
    public async Task HasRecentRequestAsync_ReturnsFalse_WhenDifferentCase()
    {
        _dbContext.CardReplacementRequests.Add(new CardReplacementRequestEntity
        {
            HouseholdIdentifierHash = HouseholdHash,
            CaseIdHash = OtherCaseHash,
            RequestedAt = DateTime.UtcNow.AddDays(-3),
            RequestedByUserId = TestUserId
        });
        await _dbContext.SaveChangesAsync();

        var result = await _repository.HasRecentRequestAsync(
            HouseholdHash, CaseHash, TimeSpan.FromDays(14));

        Assert.False(result);
    }

    [Fact]
    public async Task CreateAsync_PersistsRequest()
    {
        await _repository.CreateAsync(HouseholdHash, CaseHash, TestUserId);

        var stored = await _dbContext.CardReplacementRequests.SingleAsync();
        Assert.Equal(HouseholdHash, stored.HouseholdIdentifierHash);
        Assert.Equal(CaseHash, stored.CaseIdHash);
        Assert.Equal(TestUserId, stored.RequestedByUserId);
        Assert.True((DateTime.UtcNow - stored.RequestedAt).TotalSeconds < 5);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test test/SEBT.Portal.Tests/SEBT.Portal.Tests.csproj --filter "FullyQualifiedName~CardReplacementRequestRepositoryTests" -v minimal`

Expected: FAIL — `CardReplacementRequestRepository` does not exist yet.

- [ ] **Step 3: Create the repository interface**

Create `src/SEBT.Portal.Core/Repositories/ICardReplacementRequestRepository.cs`:

```csharp
namespace SEBT.Portal.Core.Repositories;

/// <summary>
/// Repository for tracking card replacement requests.
/// Used to enforce the 14-day cooldown between replacement requests per case.
/// All identifier values are pre-hashed by callers via <see cref="Services.IIdentifierHasher"/>.
/// </summary>
public interface ICardReplacementRequestRepository
{
    /// <summary>
    /// Checks whether a card replacement request exists for the given household+case
    /// within the specified cooldown period.
    /// </summary>
    /// <param name="householdIdentifierHash">HMAC hash of the household identifier.</param>
    /// <param name="caseIdHash">HMAC hash of the case ID.</param>
    /// <param name="cooldownPeriod">The minimum time that must elapse between requests.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if a request exists within the cooldown window.</returns>
    Task<bool> HasRecentRequestAsync(
        string householdIdentifierHash,
        string caseIdHash,
        TimeSpan cooldownPeriod,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a new card replacement request.
    /// </summary>
    /// <param name="householdIdentifierHash">HMAC hash of the household identifier.</param>
    /// <param name="caseIdHash">HMAC hash of the case ID.</param>
    /// <param name="requestedByUserId">The ID of the user making the request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CreateAsync(
        string householdIdentifierHash,
        string caseIdHash,
        Guid requestedByUserId,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Create the repository implementation**

Create `src/SEBT.Portal.Infrastructure/Repositories/CardReplacementRequestRepository.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Data.Entities;

namespace SEBT.Portal.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ICardReplacementRequestRepository"/>.
/// Queries and writes to the CardReplacementRequests table.
/// </summary>
public class CardReplacementRequestRepository(PortalDbContext dbContext)
    : ICardReplacementRequestRepository
{
    /// <inheritdoc />
    public async Task<bool> HasRecentRequestAsync(
        string householdIdentifierHash,
        string caseIdHash,
        TimeSpan cooldownPeriod,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow - cooldownPeriod;

        return await dbContext.CardReplacementRequests.AnyAsync(
            r => r.HouseholdIdentifierHash == householdIdentifierHash
                 && r.CaseIdHash == caseIdHash
                 && r.RequestedAt > cutoff,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task CreateAsync(
        string householdIdentifierHash,
        string caseIdHash,
        Guid requestedByUserId,
        CancellationToken cancellationToken = default)
    {
        var entity = new CardReplacementRequestEntity
        {
            HouseholdIdentifierHash = householdIdentifierHash,
            CaseIdHash = caseIdHash,
            RequestedAt = DateTime.UtcNow,
            RequestedByUserId = requestedByUserId
        };

        dbContext.CardReplacementRequests.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
```

- [ ] **Step 5: Register in DI**

In `src/SEBT.Portal.Infrastructure/Dependencies.cs`, add the registration alongside the other repository registrations:

```csharp
services.AddScoped<ICardReplacementRequestRepository, CardReplacementRequestRepository>();
```

Add the using: `using SEBT.Portal.Infrastructure.Repositories;` (if not already present).

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test test/SEBT.Portal.Tests/SEBT.Portal.Tests.csproj --filter "FullyQualifiedName~CardReplacementRequestRepositoryTests" -v minimal`

Expected: All 5 tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/SEBT.Portal.Core/Repositories/ICardReplacementRequestRepository.cs
git add src/SEBT.Portal.Infrastructure/Repositories/CardReplacementRequestRepository.cs
git add src/SEBT.Portal.Infrastructure/Dependencies.cs
git add test/SEBT.Portal.Tests/Unit/Repositories/CardReplacementRequestRepositoryTests.cs
git commit -m "feat: add CardReplacementRequestRepository for cooldown tracking

Interface in Core, EF implementation in Infrastructure. Provides
HasRecentRequestAsync for cooldown checks and CreateAsync for recording
new requests. All identifiers are pre-hashed by callers."
```

---

### Task 5: Wire cooldown persistence into `RequestCardReplacementCommandHandler`

This is the core change. The handler needs to:
1. Query the repository for recent requests (replacing the stale state-connector check)
2. Persist new requests after all validations pass

**Files:**
- Modify: `src/SEBT.Portal.UseCases/Household/RequestCardReplacement/RequestCardReplacementCommandHandler.cs`
- Modify: `test/SEBT.Portal.Tests/Unit/UseCases/Household/RequestCardReplacementCommandHandlerTests.cs`

- [ ] **Step 1: Write the failing test — cooldown from DB blocks request**

Add to `RequestCardReplacementCommandHandlerTests.cs`. First, add new fields and update the constructor/factory method:

```csharp
// Add new fields alongside existing ones:
private readonly ICardReplacementRequestRepository _cardReplacementRepo =
    Substitute.For<ICardReplacementRequestRepository>();
private readonly IIdentifierHasher _identifierHasher =
    Substitute.For<IIdentifierHasher>();
```

Update `CreateHandler`:

```csharp
private RequestCardReplacementCommandHandler CreateHandler(TimeProvider? timeProvider = null) =>
    new(_validator, _resolver, _repository, _minimumIalService, _evaluator,
        _cardReplacementRepo, _identifierHasher, timeProvider ?? TimeProvider.System, _logger);
```

Add the using statements:

```csharp
using SEBT.Portal.Core.Services;
```

Add the `IIdentifierHasher` default setup to the constructor:

```csharp
// Default: hasher returns a deterministic hash for any input
_identifierHasher.Hash(Arg.Any<string?>()).Returns(callInfo =>
    callInfo.Arg<string?>() != null ? $"HASH_{callInfo.Arg<string>()}" : null);
```

Now add the new test:

```csharp
[Fact]
public async Task Handle_ReturnsFailed_WhenCooldownActiveInPortalDb()
{
    var user = CreateUser("user@example.com");
    var command = CreateValidCommand(user, new List<string> { "SEBT-001" });
    var household = CreateHouseholdWithCases(
        new SummerEbtCase
        {
            SummerEBTCaseID = "SEBT-001",
            CardRequestedAt = null // State connector has no data
        });

    SetupResolverSuccess();
    SetupRepositoryReturns(household);

    // Portal DB says this case was requested 3 days ago
    _cardReplacementRepo.HasRecentRequestAsync(
        Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
        .Returns(true);

    var result = await CreateHandler().Handle(command);

    Assert.IsType<ValidationFailedResult>(result);
}
```

- [ ] **Step 2: Write the failing test — successful request persists to DB**

```csharp
[Fact]
public async Task Handle_PersistsRequest_WhenSuccessful()
{
    var user = CreateUser("user@example.com");
    var command = CreateValidCommand(user, new List<string> { "SEBT-001" });
    var household = CreateHouseholdWithCases(
        new SummerEbtCase
        {
            SummerEBTCaseID = "SEBT-001",
            CardRequestedAt = null
        });

    SetupResolverSuccess();
    SetupRepositoryReturns(household);

    // No recent requests in portal DB
    _cardReplacementRepo.HasRecentRequestAsync(
        Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
        .Returns(false);

    var result = await CreateHandler().Handle(command);

    Assert.IsType<SuccessResult>(result);
    await _cardReplacementRepo.Received(1).CreateAsync(
        Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test test/SEBT.Portal.Tests/SEBT.Portal.Tests.csproj --filter "FullyQualifiedName~RequestCardReplacementCommandHandlerTests" -v minimal`

Expected: FAIL — constructor signature doesn't match.

- [ ] **Step 4: Update the handler to accept new dependencies and use them**

Modify `src/SEBT.Portal.UseCases/Household/RequestCardReplacement/RequestCardReplacementCommandHandler.cs`:

Update the constructor to add the new dependencies:

```csharp
public class RequestCardReplacementCommandHandler(
    IValidator<RequestCardReplacementCommand> validator,
    IHouseholdIdentifierResolver resolver,
    IHouseholdRepository repository,
    IMinimumIalService minimumIalService,
    ISelfServiceEvaluator selfServiceEvaluator,
    ICardReplacementRequestRepository cardReplacementRepo,
    IIdentifierHasher identifierHasher,
    TimeProvider timeProvider,
    ILogger<RequestCardReplacementCommandHandler> logger)
    : ICommandHandler<RequestCardReplacementCommand>
```

Add using statements:

```csharp
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
```

Replace the `CheckCooldown` call and the TODO block (lines 103–125) with:

```csharp
        // Check cooldown from portal DB — the authoritative source for request timestamps.
        // State connector CardRequestedAt may be null/stale; portal DB tracks our own writes.
        var householdHash = identifierHasher.Hash(identifier.Value);
        var cooldownErrors = new List<ValidationError>();

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

        if (cooldownErrors.Count > 0)
        {
            logger.LogInformation(
                "Card replacement rejected: {Count} case(s) within cooldown period",
                cooldownErrors.Count);
            return Result.ValidationFailed(cooldownErrors);
        }

        // Resolve the user ID for audit trail
        var userId = command.User.FindFirst(JwtClaimTypes.UserId)?.Value;
        if (userId == null || !Guid.TryParse(userId, out var userGuid))
        {
            logger.LogWarning("Card replacement: unable to resolve user ID from claims");
            return Result.Unauthorized("Unable to identify user from token.");
        }

        // Persist replacement requests to portal DB for cooldown enforcement
        foreach (var caseId in command.CaseIds)
        {
            var caseHash = identifierHasher.Hash(caseId);
            if (householdHash != null && caseHash != null)
            {
                await cardReplacementRepo.CreateAsync(
                    householdHash, caseHash, userGuid, cancellationToken);
            }
        }

        var identifierKind = identifier.Type.ToString();
        logger.LogInformation(
            "Card replacement request recorded for household identifier kind {Kind}, {Count} case(s)",
            identifierKind,
            command.CaseIds.Count);

        return Result.Success();
```

Remove the now-unused `CheckCooldown` static method entirely (lines 128–162).

- [ ] **Step 5: Check `JwtClaimTypes` includes a UserId constant**

Verify that `JwtClaimTypes` has a `UserId` constant. Search for it:

```bash
grep -r "UserId" src/SEBT.Portal.Core/Models/Auth/JwtClaimTypes.cs
```

If it doesn't exist, add it to `JwtClaimTypes.cs`:

```csharp
public const string UserId = "userId";
```

Check how the user ID claim is actually set in the auth flow (look at `JwtTokenService` or login handlers) to use the correct claim name.

- [ ] **Step 6: Run the new tests**

Run: `dotnet test test/SEBT.Portal.Tests/SEBT.Portal.Tests.csproj --filter "FullyQualifiedName~RequestCardReplacementCommandHandlerTests" -v minimal`

Expected: The 2 new tests pass. Some existing tests may need their `CreateHandler` calls updated.

- [ ] **Step 7: Fix any existing tests that broke due to constructor change**

The existing tests in `RequestCardReplacementCommandHandlerTests` use `CreateHandler()` which now requires additional arguments. Update the `CreateHandler` method to use the mocked `_cardReplacementRepo` and `_identifierHasher` fields.

Existing cooldown tests that relied on `SummerEbtCase.CardRequestedAt` should be updated:
- Tests checking cooldown with `CardRequestedAt = now.AddDays(-3)` should instead set up `_cardReplacementRepo.HasRecentRequestAsync(...).Returns(true)`
- Tests checking cooldown with `CardRequestedAt = now.AddDays(-30)` should set up `_cardReplacementRepo.HasRecentRequestAsync(...).Returns(false)`

The existing tests for the old `CheckCooldown` static method behavior are replaced by the repository mock tests.

- [ ] **Step 8: Run full unit test suite**

Run: `pnpm api:test:unit`

Expected: All tests pass (860+ including the new ones).

- [ ] **Step 9: Commit**

```bash
git add src/SEBT.Portal.UseCases/Household/RequestCardReplacement/RequestCardReplacementCommandHandler.cs
git add test/SEBT.Portal.Tests/Unit/UseCases/Household/RequestCardReplacementCommandHandlerTests.cs
git commit -m "feat: persist card replacement requests for cooldown enforcement

The handler now writes CardReplacementRequestEntity records to the
portal DB after validation passes, and checks the DB for recent
requests instead of relying on the state connector's CardRequestedAt
(which was never populated). Identifiers are HMAC-hashed before storage.

Closes the DC-153 2-week rule gap."
```

---

### Task 6: Update CLAUDE.md (already done in worktree)

The CLAUDE.md additions were made at the start of this branch. Verify they're correct and commit if not already committed.

**Files:**
- Verify: `CLAUDE.md` (already modified in worktree)

- [ ] **Step 1: Verify the CLAUDE.md additions are present**

Check that the two new subsections exist under `## Security`:
- `### PII at rest`
- `### State connector data boundaries`

- [ ] **Step 2: Commit if not already committed**

```bash
git add CLAUDE.md
git commit -m "docs: add PII-at-rest and state connector boundary guidelines to CLAUDE.md"
```

---

### Task 7: Final verification and cleanup

- [ ] **Step 1: Run full unit test suite**

Run: `pnpm api:test:unit`

Expected: All tests pass.

- [ ] **Step 2: Run the build**

Run: `pnpm api:build`

Expected: Build succeeds with 0 errors.

- [ ] **Step 3: Verify no stale references to `SsnNormalizer` remain**

Run: `grep -r "SsnNormalizer" src/ test/`

Expected: No matches (only in git history).

- [ ] **Step 4: Verify no cleartext PII in new code**

Review all new files to ensure:
- No hardcoded email addresses, case IDs, or identifiers
- All stored identifiers go through `IIdentifierHasher.Hash()`
- Test files use constants like `"HASH_..."` for mock hash values

---

## Notes for implementer

- **Frontend needs zero changes.** The frontend already reads `cardRequestedAt` from the API response and implements cooldown logic in `cooldown.ts`. The API response will continue to pass through whatever `CardRequestedAt` the state connector provides, but the backend now enforces cooldown independently via the portal DB.
- **The `SummerEbtCase.CardRequestedAt` field on the domain model is still populated by the state connector.** We don't remove it — it may eventually contain data if a state system starts tracking this. The portal DB is the authoritative source for cooldown enforcement; the state connector value is informational.
- **`IIdentifierHasher.Hash()` normalizes input** via `IdentifierNormalizer` (trim, strip dashes/spaces) before hashing. This means `"SEBT-001"` and `"SEBT 001"` produce the same hash. This is correct — case IDs may appear with or without formatting.
