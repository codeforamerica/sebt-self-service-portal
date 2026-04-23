using Microsoft.EntityFrameworkCore;
using SEBT.Portal.Core.Exceptions;
using SEBT.Portal.Core.Models.DocVerification;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Data.Entities;
using SEBT.Portal.Infrastructure.Helpers;
using SEBT.Portal.Infrastructure.Repositories;

namespace SEBT.Portal.Tests.Unit.Repositories;

[Collection("SqlServer")]
[Trait("Category", "SqlServer")]
public class DatabaseDocVerificationChallengeRepositoryTests : IClassFixture<SqlServerTestFixture>
{
    private readonly SqlServerTestFixture _fixture;

    public DatabaseDocVerificationChallengeRepositoryTests(SqlServerTestFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Creates a user and a challenge entity in the database, returning the user's Id.
    /// </summary>
    private async Task<int> SeedChallengeAsync(
        PortalDbContext context,
        int status,
        DateTime? expiresAt)
    {
        var userEntity = UserFactory.CreateUserEntity();
        context.Users.Add(userEntity);
        await context.SaveChangesAsync();

        var challenge = new DocVerificationChallengeEntity
        {
            PublicId = Guid.NewGuid(),
            UserId = userEntity.Id,
            Status = status,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.DocVerificationChallenges.Add(challenge);
        await context.SaveChangesAsync();

        return userEntity.Id;
    }

    // --- GetActiveByUserIdAsync expiration filtering (N2) ---

    [Fact]
    public async Task GetActiveByUserIdAsync_ShouldExcludeExpiredPendingChallenge()
    {
        using var context = _fixture.CreateContext();
        var userId = await SeedChallengeAsync(context,
            status: (int)DocVerificationStatus.Pending,
            expiresAt: DateTime.UtcNow.AddMinutes(-5));

        var repo = new DatabaseDocVerificationChallengeRepository(context);
        var result = await repo.GetActiveByUserIdAsync(userId);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetActiveByUserIdAsync_ShouldIncludeNonExpiredPendingChallenge()
    {
        using var context = _fixture.CreateContext();
        var userId = await SeedChallengeAsync(context,
            status: (int)DocVerificationStatus.Pending,
            expiresAt: DateTime.UtcNow.AddMinutes(25));

        var repo = new DatabaseDocVerificationChallengeRepository(context);
        var result = await repo.GetActiveByUserIdAsync(userId);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetActiveByUserIdAsync_ShouldIncludeCreatedChallengeWithNullExpiresAt()
    {
        using var context = _fixture.CreateContext();
        var userId = await SeedChallengeAsync(context,
            status: (int)DocVerificationStatus.Created,
            expiresAt: null);

        var repo = new DatabaseDocVerificationChallengeRepository(context);
        var result = await repo.GetActiveByUserIdAsync(userId);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetActiveByUserIdAsync_ShouldExcludeExpiredCreatedChallenge()
    {
        using var context = _fixture.CreateContext();
        var userId = await SeedChallengeAsync(context,
            status: (int)DocVerificationStatus.Created,
            expiresAt: DateTime.UtcNow.AddMinutes(-10));

        var repo = new DatabaseDocVerificationChallengeRepository(context);
        var result = await repo.GetActiveByUserIdAsync(userId);

        Assert.Null(result);
    }

    // --- One-active-challenge constraint (F8) ---

    [Fact]
    public async Task CreateAsync_ShouldRejectSecondActiveChallenge_ForSameUser()
    {
        using var context = _fixture.CreateContext();

        // Seed a user with one active (Created) challenge
        var userId = await SeedChallengeAsync(context,
            status: (int)DocVerificationStatus.Created,
            expiresAt: DateTime.UtcNow.AddMinutes(30));

        // Attempt to insert a second active challenge for the same user
        var duplicate = new DocVerificationChallengeEntity
        {
            PublicId = Guid.NewGuid(),
            UserId = userId,
            Status = (int)DocVerificationStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.DocVerificationChallenges.Add(duplicate);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task CreateAsync_ShouldExpireTimeElapsedRow_ThenInsertNew()
    {
        using var context = _fixture.CreateContext();
        var userId = await SeedChallengeAsync(context,
            status: (int)DocVerificationStatus.Created,
            expiresAt: DateTime.UtcNow.AddMinutes(-10));

        var repo = new DatabaseDocVerificationChallengeRepository(context);
        var challenge = new DocVerificationChallenge
        {
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };

        await repo.CreateAsync(challenge);

        var rows = await context.DocVerificationChallenges
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Id)
            .ToListAsync();

        Assert.Equal(2, rows.Count);
        Assert.Single(rows, r => r.Status == (int)DocVerificationStatus.Expired);
        Assert.Single(rows, r => r.Status == (int)DocVerificationStatus.Created && r.PublicId == challenge.PublicId);
    }

    [Fact]
    public async Task CreateAsync_ShouldTranslateUniqueIndexViolation_ToDuplicateRecordException()
    {
        using var context = _fixture.CreateContext();
        var userId = await SeedChallengeAsync(context,
            status: (int)DocVerificationStatus.Created,
            expiresAt: DateTime.UtcNow.AddMinutes(30));

        var repo = new DatabaseDocVerificationChallengeRepository(context);
        var duplicate = new DocVerificationChallenge
        {
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };

        await Assert.ThrowsAsync<DuplicateRecordException>(() => repo.CreateAsync(duplicate));
    }

    [Fact]
    public async Task CreateAsync_ShouldAllowNewChallenge_WhenExistingIsTerminal()
    {
        using var context = _fixture.CreateContext();

        // Seed a user with a terminal (Verified) challenge
        var userId = await SeedChallengeAsync(context,
            status: (int)DocVerificationStatus.Verified,
            expiresAt: DateTime.UtcNow.AddMinutes(-5));

        // A new active challenge for the same user should succeed
        var newChallenge = new DocVerificationChallengeEntity
        {
            PublicId = Guid.NewGuid(),
            UserId = userId,
            Status = (int)DocVerificationStatus.Created,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.DocVerificationChallenges.Add(newChallenge);

        // Should not throw — terminal challenges don't count
        await context.SaveChangesAsync();
    }
}
