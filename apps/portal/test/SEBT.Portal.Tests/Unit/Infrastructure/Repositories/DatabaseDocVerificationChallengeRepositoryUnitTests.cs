using Microsoft.EntityFrameworkCore;
using SEBT.Portal.Core.Models.DocVerification;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Data.Entities;
using SEBT.Portal.Infrastructure.Repositories;
using SEBT.Portal.Tests.Unit.TestSupport;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Repositories;

/// <summary>
/// InMemory unit tests for <see cref="DatabaseDocVerificationChallengeRepository"/>.
/// </summary>
public class DatabaseDocVerificationChallengeRepositoryUnitTests
{
    private static PortalDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new PortalDbContext(options);
    }

    private static DatabaseDocVerificationChallengeRepository CreateRepository(PortalDbContext context) =>
        new(context, TestPortalCryptography.PiiSymmetricEncryption);

    private static async Task<UserEntity> SeedUserAsync(PortalDbContext context)
    {
        var user = new UserEntity();
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static async Task<DocVerificationChallengeEntity> SeedChallengeAsync(
        PortalDbContext context,
        Guid userId,
        Action<DocVerificationChallengeEntity>? customize = null)
    {
        var entity = new DocVerificationChallengeEntity
        {
            PublicId = Guid.NewGuid(),
            UserId = userId,
            Status = (int)DocVerificationStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };
        customize?.Invoke(entity);
        context.DocVerificationChallenges.Add(entity);
        await context.SaveChangesAsync();
        return entity;
    }

    [Fact]
    public async Task GetByPublicIdAsync_whenOwnedByUser_returnsChallenge()
    {
        using var context = CreateContext();
        var user = await SeedUserAsync(context);
        var entity = await SeedChallengeAsync(context, user.Id, c =>
        {
            c.ProofingIdType = TestPortalCryptography.PiiSymmetricEncryption.Encrypt("ssn");
        });
        var repo = CreateRepository(context);

        var result = await repo.GetByPublicIdAsync(entity.PublicId, user.Id);

        Assert.NotNull(result);
        Assert.Equal(entity.Id, result!.Id);
        Assert.Equal("ssn", result.ProofingIdType);
    }

    [Fact]
    public async Task GetByPublicIdAsync_whenDifferentUser_returnsNull()
    {
        using var context = CreateContext();
        var owner = await SeedUserAsync(context);
        var other = await SeedUserAsync(context);
        var entity = await SeedChallengeAsync(context, owner.Id);
        var repo = CreateRepository(context);

        var result = await repo.GetByPublicIdAsync(entity.PublicId, other.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetBySocureReferenceIdAsync_whenExists_returnsChallenge()
    {
        using var context = CreateContext();
        var user = await SeedUserAsync(context);
        await SeedChallengeAsync(context, user.Id, c => c.SocureReferenceId = "ref-1");
        var repo = CreateRepository(context);

        var result = await repo.GetBySocureReferenceIdAsync("ref-1");

        Assert.NotNull(result);
        Assert.Equal("ref-1", result!.SocureReferenceId);
    }

    [Fact]
    public async Task GetBySocureReferenceIdAsync_whenMissingOrBlank_returnsNull()
    {
        using var context = CreateContext();
        var repo = CreateRepository(context);

        Assert.Null(await repo.GetBySocureReferenceIdAsync("missing"));
        Assert.Null(await repo.GetBySocureReferenceIdAsync("  "));
        Assert.Null(await repo.GetBySocureReferenceIdAsync(null!));
    }

    [Fact]
    public async Task GetByEvalIdAsync_whenExists_returnsChallenge()
    {
        using var context = CreateContext();
        var user = await SeedUserAsync(context);
        await SeedChallengeAsync(context, user.Id, c => c.EvalId = "eval-1");
        var repo = CreateRepository(context);

        var result = await repo.GetByEvalIdAsync("eval-1");

        Assert.NotNull(result);
        Assert.Equal("eval-1", result!.EvalId);
    }

    [Fact]
    public async Task GetByEvalIdAsync_whenMissingOrBlank_returnsNull()
    {
        using var context = CreateContext();
        var repo = CreateRepository(context);

        Assert.Null(await repo.GetByEvalIdAsync("missing"));
        Assert.Null(await repo.GetByEvalIdAsync("  "));
    }

    [Fact]
    public async Task GetActiveByUserIdAsync_whenPendingAndUnexpired_returnsChallenge()
    {
        using var context = CreateContext();
        var user = await SeedUserAsync(context);
        var entity = await SeedChallengeAsync(context, user.Id);
        var repo = CreateRepository(context);

        var result = await repo.GetActiveByUserIdAsync(user.Id);

        Assert.NotNull(result);
        Assert.Equal(entity.Id, result!.Id);
    }

    [Fact]
    public async Task GetLatestSocureReferenceIdByUserIdAsync_returnsMostRecent()
    {
        using var context = CreateContext();
        var user = await SeedUserAsync(context);
        var older = DateTime.UtcNow.AddHours(-2);
        var newer = DateTime.UtcNow.AddHours(-1);
        await SeedChallengeAsync(context, user.Id, c =>
        {
            c.SocureReferenceId = "ref-old";
            c.CreatedAt = older;
            c.Status = (int)DocVerificationStatus.Rejected;
        });
        await SeedChallengeAsync(context, user.Id, c =>
        {
            c.SocureReferenceId = "ref-new";
            c.CreatedAt = newer;
            c.Status = (int)DocVerificationStatus.Verified;
        });
        var repo = CreateRepository(context);

        var result = await repo.GetLatestSocureReferenceIdByUserIdAsync(user.Id);

        Assert.Equal("ref-new", result);
    }

    [Fact]
    public async Task CreateAsync_whenChallengeIsNull_throws()
    {
        using var context = CreateContext();
        var repo = CreateRepository(context);

        await Assert.ThrowsAsync<ArgumentNullException>(() => repo.CreateAsync(null!));
    }

    [Fact]
    public async Task UpdateAsync_whenChallengeIsNull_throws()
    {
        using var context = CreateContext();
        var repo = CreateRepository(context);

        await Assert.ThrowsAsync<ArgumentNullException>(() => repo.UpdateAsync(null!));
    }

    [Fact]
    public async Task UpdateAsync_whenMissing_throws()
    {
        using var context = CreateContext();
        var repo = CreateRepository(context);
        var challenge = new DocVerificationChallenge { UserId = Guid.NewGuid() };

        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.UpdateAsync(challenge));
    }

    [Fact]
    public async Task UpdateAsync_persistsFieldsAndEncryptsProofingPii()
    {
        using var context = CreateContext();
        var user = await SeedUserAsync(context);
        var entity = await SeedChallengeAsync(context, user.Id);
        var repo = CreateRepository(context);
        var issuedAt = DateTime.UtcNow.AddMinutes(-5);
        var challenge = DocVerificationChallenge.Reconstitute(
            id: entity.Id,
            publicId: entity.PublicId,
            userId: user.Id,
            status: DocVerificationStatus.Pending,
            socureReferenceId: "ref-updated",
            evalId: "eval-updated",
            socureEventId: "event-1",
            docvTransactionToken: "token-1",
            docvUrl: "https://example.test/docv",
            offboardingReason: null,
            allowIdRetry: false,
            createdAt: entity.CreatedAt,
            updatedAt: entity.UpdatedAt,
            expiresAt: entity.ExpiresAt,
            proofingDateOfBirth: "1990-04-15",
            proofingIdType: "ssn",
            proofingIdValue: "last4",
            docvTokenIssuedAt: issuedAt);

        await repo.UpdateAsync(challenge);

        var stored = await context.DocVerificationChallenges.AsNoTracking()
            .SingleAsync(c => c.Id == entity.Id);
        Assert.Equal("ref-updated", stored.SocureReferenceId);
        Assert.Equal("eval-updated", stored.EvalId);
        Assert.Equal("event-1", stored.SocureEventId);
        Assert.Equal("token-1", stored.DocvTransactionToken);
        Assert.Equal("https://example.test/docv", stored.DocvUrl);
        Assert.False(stored.AllowIdRetry);
        Assert.Equal(issuedAt, stored.DocvTokenIssuedAt);
        Assert.Equal(
            "1990-04-15",
            TestPortalCryptography.PiiSymmetricEncryption.DecryptOrPassThroughLegacy(
                stored.ProofingDateOfBirth));
        Assert.Equal(
            "ssn",
            TestPortalCryptography.PiiSymmetricEncryption.DecryptOrPassThroughLegacy(
                stored.ProofingIdType));
        Assert.Equal(
            "last4",
            TestPortalCryptography.PiiSymmetricEncryption.DecryptOrPassThroughLegacy(
                stored.ProofingIdValue));
    }
}
