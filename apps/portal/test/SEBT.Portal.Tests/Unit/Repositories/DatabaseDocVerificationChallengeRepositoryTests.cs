using Microsoft.EntityFrameworkCore;
using SEBT.Portal.Core.Exceptions;
using SEBT.Portal.Core.Models.DocVerification;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Data.Entities;
using SEBT.Portal.Infrastructure.Helpers;
using SEBT.Portal.Infrastructure.Repositories;
using SEBT.Portal.Tests.Unit.TestSupport;

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
    private async Task<Guid> SeedChallengeAsync(
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

    // --- GetLatestSocureReferenceIdByUserIdAsync ---

    [Fact]
    public async Task GetLatestSocureReferenceIdByUserIdAsync_ReturnsMostRecentNonNullReferenceId()
    {
        using var context = _fixture.CreateContext();
        var userEntity = UserFactory.CreateUserEntity();
        context.Users.Add(userEntity);
        await context.SaveChangesAsync();

        var baseTime = DateTime.UtcNow;
        // Oldest has a reference id, middle has none (abandoned before a Socure
        // session started), newest has the one that should win.
        context.DocVerificationChallenges.AddRange(
            new DocVerificationChallengeEntity
            {
                PublicId = Guid.NewGuid(),
                UserId = userEntity.Id,
                Status = (int)DocVerificationStatus.Rejected,
                SocureReferenceId = "ref-oldest",
                CreatedAt = baseTime.AddHours(-2),
                UpdatedAt = baseTime.AddHours(-2)
            },
            new DocVerificationChallengeEntity
            {
                PublicId = Guid.NewGuid(),
                UserId = userEntity.Id,
                Status = (int)DocVerificationStatus.Expired,
                SocureReferenceId = null,
                CreatedAt = baseTime.AddHours(-1),
                UpdatedAt = baseTime.AddHours(-1)
            },
            new DocVerificationChallengeEntity
            {
                PublicId = Guid.NewGuid(),
                UserId = userEntity.Id,
                Status = (int)DocVerificationStatus.Verified,
                SocureReferenceId = "ref-newest",
                CreatedAt = baseTime.AddMinutes(-30),
                UpdatedAt = baseTime.AddMinutes(-30)
            });
        await context.SaveChangesAsync();

        var repo = new DatabaseDocVerificationChallengeRepository(context, TestPortalCryptography.PiiSymmetricEncryption);

        var referenceId = await repo.GetLatestSocureReferenceIdByUserIdAsync(userEntity.Id);

        Assert.Equal("ref-newest", referenceId);
    }

    [Fact]
    public async Task GetLatestSocureReferenceIdByUserIdAsync_ReturnsNullWhenUserHasNoChallengesWithReferenceId()
    {
        using var context = _fixture.CreateContext();
        var userId = await SeedChallengeAsync(context,
            status: (int)DocVerificationStatus.Created,
            expiresAt: null);

        var repo = new DatabaseDocVerificationChallengeRepository(context, TestPortalCryptography.PiiSymmetricEncryption);

        var referenceId = await repo.GetLatestSocureReferenceIdByUserIdAsync(userId);

        Assert.Null(referenceId);
    }

    [Fact]
    public async Task GetLatestSocureReferenceIdByUserIdAsync_DoesNotReturnAnotherUsersReferenceId()
    {
        using var context = _fixture.CreateContext();
        var userWithChallenge = UserFactory.CreateUserEntity();
        var userWithout = UserFactory.CreateUserEntity();
        context.Users.AddRange(userWithChallenge, userWithout);
        await context.SaveChangesAsync();

        context.DocVerificationChallenges.Add(new DocVerificationChallengeEntity
        {
            PublicId = Guid.NewGuid(),
            UserId = userWithChallenge.Id,
            Status = (int)DocVerificationStatus.Verified,
            SocureReferenceId = "ref-other-user",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var repo = new DatabaseDocVerificationChallengeRepository(context, TestPortalCryptography.PiiSymmetricEncryption);

        var referenceId = await repo.GetLatestSocureReferenceIdByUserIdAsync(userWithout.Id);

        Assert.Null(referenceId);
    }

    // --- GetActiveByUserIdAsync expiration filtering (N2) ---

    [Fact]
    public async Task GetActiveByUserIdAsync_ShouldExcludeExpiredPendingChallenge()
    {
        using var context = _fixture.CreateContext();
        var userId = await SeedChallengeAsync(context,
            status: (int)DocVerificationStatus.Pending,
            expiresAt: DateTime.UtcNow.AddMinutes(-5));

        var repo = new DatabaseDocVerificationChallengeRepository(context, TestPortalCryptography.PiiSymmetricEncryption);
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

        var repo = new DatabaseDocVerificationChallengeRepository(context, TestPortalCryptography.PiiSymmetricEncryption);
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

        var repo = new DatabaseDocVerificationChallengeRepository(context, TestPortalCryptography.PiiSymmetricEncryption);
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

        var repo = new DatabaseDocVerificationChallengeRepository(context, TestPortalCryptography.PiiSymmetricEncryption);
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

        var repo = new DatabaseDocVerificationChallengeRepository(context, TestPortalCryptography.PiiSymmetricEncryption);
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

        var repo = new DatabaseDocVerificationChallengeRepository(context, TestPortalCryptography.PiiSymmetricEncryption);
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
