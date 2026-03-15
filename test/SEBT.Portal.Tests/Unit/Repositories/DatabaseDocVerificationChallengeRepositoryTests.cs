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
}
