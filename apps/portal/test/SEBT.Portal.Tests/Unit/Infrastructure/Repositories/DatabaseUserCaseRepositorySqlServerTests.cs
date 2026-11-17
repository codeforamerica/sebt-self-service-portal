using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Data.Entities;
using SEBT.Portal.Infrastructure.Repositories;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.Tests.Unit.Repositories;
using SEBT.Portal.Tests.Unit.TestSupport;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Repositories;

/// <seealso cref="SqlServerTestFixture"/>
[Collection("SqlServer")]
[Trait("Category", "SqlServer")]
public class DatabaseUserCaseRepositorySqlServerTests : IClassFixture<SqlServerTestFixture>
{
    private static readonly IdentifierHasher Hasher = new(
        Options.Create(new IdentifierHasherSettings
        {
            SecretKey = TestPortalCryptography.TestIdentifierHasherSecretKey
        }));

    private readonly SqlServerTestFixture _fixture;
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));

    public DatabaseUserCaseRepositorySqlServerTests(SqlServerTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UpsertDisplayedCasesAsync_WhenConcurrentRequestInsertsSameCase_DoesNotThrow()
    {
        var userId = await CreateUserAsync();
        UserCaseIdentifiers[] cases = [new("SEBT-001", "APP-9", null)];

        await using var db = CreateContextThatRacesOnFirstSave(userId, cases);
        var repository = CreateRepository(db);

        await repository.UpsertDisplayedCasesAsync(userId, cases);

        await using var verify = _fixture.CreateContext();
        Assert.Single(verify.UserCases.Where(c => c.UserId == userId));
    }

    [Fact]
    public async Task UpsertDisplayedCasesAsync_WhenConcurrentRequestInsertsSomeCases_StillInsertsTheRest()
    {
        var userId = await CreateUserAsync();
        UserCaseIdentifiers[] racedCases = [new("SEBT-001", null, null)];
        UserCaseIdentifiers[] cases =
        [
            new("SEBT-001", null, null),
            new("SEBT-002", null, null)
        ];

        await using var db = CreateContextThatRacesOnFirstSave(userId, racedCases);
        var repository = CreateRepository(db);

        await repository.UpsertDisplayedCasesAsync(userId, cases);

        await using var verify = _fixture.CreateContext();
        var storedHashes = verify.UserCases
            .Where(c => c.UserId == userId)
            .Select(c => c.CaseIdHash)
            .ToList();
        Assert.Equal(2, storedHashes.Count);
        Assert.Contains(Hasher.Hash("SEBT-002"), storedHashes);
    }

    [Fact]
    public async Task UpsertDisplayedCasesAsync_WhenRetryAlsoConflicts_ThrowsAndLeavesNoPendingChanges()
    {
        var userId = await CreateUserAsync();
        UserCaseIdentifiers[] cases =
        [
            new("SEBT-001", null, null),
            new("SEBT-002", null, null)
        ];

        await using var db = CreateContextThatRacesOnEachSave(
            userId,
            [[new("SEBT-001", null, null)], [new("SEBT-002", null, null)]]);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            CreateRepository(db).UpsertDisplayedCasesAsync(userId, cases));

        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task UpsertDisplayedCasesAsync_WhenInsertFailsForOtherReason_ThrowsAndLeavesNoPendingChanges()
    {
        var unknownUserId = Guid.CreateVersion7();

        await using var db = _fixture.CreateContext();

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            CreateRepository(db).UpsertDisplayedCasesAsync(unknownUserId, [new("SEBT-001", null, null)]));

        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task UpsertDisplayedCasesAsync_WhenLastSeenIsJustOverAnHourOld_RefreshesLastSeenAt()
    {
        var userId = await CreateUserAsync();
        UserCaseIdentifiers[] cases = [new("SEBT-001", "APP-9", null)];
        await UpsertOnFreshContextAsync(userId, cases);

        _time.Advance(TimeSpan.FromHours(1) + TimeSpan.FromSeconds(1));
        await UpsertOnFreshContextAsync(userId, cases);

        var stored = await GetSingleUserCaseAsync(userId);
        Assert.Equal(_time.GetUtcNow().UtcDateTime, stored.LastSeenAt);
    }

    [Fact]
    public async Task UpsertDisplayedCasesAsync_WhenLastSeenIsExactlyAnHourOld_DoesNotRewriteLastSeenAt()
    {
        var userId = await CreateUserAsync();
        UserCaseIdentifiers[] cases = [new("SEBT-001", "APP-9", null)];
        await UpsertOnFreshContextAsync(userId, cases);
        var firstSeen = _time.GetUtcNow().UtcDateTime;

        _time.Advance(TimeSpan.FromHours(1));
        await UpsertOnFreshContextAsync(userId, cases);

        var stored = await GetSingleUserCaseAsync(userId);
        Assert.Equal(firstSeen, stored.LastSeenAt);
    }

    [Fact]
    public async Task UpsertDisplayedCasesAsync_WhenRefreshingLastSeen_DoesNotSaveUnrelatedPendingChanges()
    {
        var userId = await CreateUserAsync();
        UserCaseIdentifiers[] cases = [new("SEBT-001", null, null)];
        await UpsertOnFreshContextAsync(userId, cases);
        _time.Advance(TimeSpan.FromHours(2));

        await using var db = _fixture.CreateContext();
        var trackedUser = await db.Users.SingleAsync(u => u.Id == userId);
        trackedUser.IsCoLoaded = true;

        await CreateRepository(db).UpsertDisplayedCasesAsync(userId, cases);

        await using var verify = _fixture.CreateContext();
        Assert.False((await verify.Users.SingleAsync(u => u.Id == userId)).IsCoLoaded);
    }

    [Fact]
    public async Task UpsertDisplayedCasesAsync_WhenInsertingWithUnrelatedPendingChanges_ThrowsWithoutSavingThem()
    {
        var userId = await CreateUserAsync();

        await using var db = _fixture.CreateContext();
        var trackedUser = await db.Users.SingleAsync(u => u.Id == userId);
        trackedUser.IsCoLoaded = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateRepository(db).UpsertDisplayedCasesAsync(userId, [new("SEBT-001", null, null)]));

        await using var verify = _fixture.CreateContext();
        Assert.False((await verify.Users.SingleAsync(u => u.Id == userId)).IsCoLoaded);
        Assert.Empty(verify.UserCases.Where(c => c.UserId == userId));
    }

    private async Task UpsertOnFreshContextAsync(Guid userId, IReadOnlyList<UserCaseIdentifiers> cases)
    {
        await using var db = _fixture.CreateContext();
        await CreateRepository(db).UpsertDisplayedCasesAsync(userId, cases);
    }

    private async Task<UserCaseEntity> GetSingleUserCaseAsync(Guid userId)
    {
        await using var db = _fixture.CreateContext();
        return await db.UserCases.SingleAsync(c => c.UserId == userId);
    }

    private async Task<Guid> CreateUserAsync()
    {
        await using var db = _fixture.CreateContext();
        var user = new UserEntity { Id = Guid.CreateVersion7() };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private DatabaseUserCaseRepository CreateRepository(PortalDbContext db) =>
        new(db, Hasher, TestPortalCryptography.PiiSymmetricEncryption, _time);

    private PortalDbContext CreateContextThatRacesOnFirstSave(
        Guid userId,
        IReadOnlyList<UserCaseIdentifiers> racedCases) =>
        CreateContextThatRacesOnEachSave(userId, [racedCases]);

    private PortalDbContext CreateContextThatRacesOnEachSave(
        Guid userId,
        IReadOnlyList<IReadOnlyList<UserCaseIdentifiers>> racedCasesPerSave)
    {
        var interceptor = new CompetingUpsertBeforeSaveInterceptor(async racedCases =>
        {
            await using var competing = _fixture.CreateContext();
            await CreateRepository(competing).UpsertDisplayedCasesAsync(userId, racedCases);
        }, racedCasesPerSave);
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseSqlServer(_fixture.ConnectionString)
            .AddInterceptors(interceptor)
            .Options;
        return new PortalDbContext(options);
    }

    /// <summary>
    /// Simulates another request committing between this context's read and each of its saves.
    /// </summary>
    private sealed class CompetingUpsertBeforeSaveInterceptor(
        Func<IReadOnlyList<UserCaseIdentifiers>, Task> competingUpsert,
        IReadOnlyList<IReadOnlyList<UserCaseIdentifiers>> racedCasesPerSave) : SaveChangesInterceptor
    {
        private int _saveCount;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (_saveCount < racedCasesPerSave.Count)
            {
                await competingUpsert(racedCasesPerSave[_saveCount++]);
            }

            return result;
        }
    }
}
