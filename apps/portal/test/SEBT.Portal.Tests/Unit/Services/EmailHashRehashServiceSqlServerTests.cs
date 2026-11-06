using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Helpers;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.Tests.Unit.Repositories;
using SEBT.Portal.Tests.Unit.TestSupport;

namespace SEBT.Portal.Tests.Unit.Services;

/// <summary>
/// SqlServer coverage for <see cref="EmailHashRehashService.ApplyAsync"/> persist and collision behavior.
/// Asserts against seeded row Ids (shared fixture DB may contain other tests' rows).
/// </summary>
[Collection("SqlServer")]
[Trait("Category", "SqlServer")]
public class EmailHashRehashServiceSqlServerTests : IClassFixture<SqlServerTestFixture>
{
    private readonly SqlServerTestFixture _fixture;

    public EmailHashRehashServiceSqlServerTests(SqlServerTestFixture fixture)
    {
        _fixture = fixture;
    }

    private static EmailHashRehashService CreateService(PortalDbContext context) =>
        new(
            context,
            TestPortalCryptography.PiiSymmetricEncryption,
            TestPortalCryptography.EmailLookupHasher,
            NullLogger<EmailHashRehashService>.Instance);

    private static EmailLookupHasher CreateStaleHasher() =>
        new(Options.Create(new IdentifierHasherSettings
        {
            SecretKey = "BackfillSecretKeyMustBeAtLeast32Chars!!"
        }));

    [Fact]
    public async Task ApplyAsync_DryRun_DoesNotPersistStaleHashUpdate()
    {
        using var context = _fixture.CreateContext();
        var email = $"rehash-dry-{Guid.NewGuid()}@example.com";
        var normalized = email.ToLowerInvariant();
        var targetHash = TestPortalCryptography.EmailLookupHasher.HashNormalized(normalized);
        var staleHash = CreateStaleHasher().HashNormalized(normalized);
        Assert.NotEqual(staleHash, targetHash);

        var seeded = UserFactory.CreateUserEntity(e =>
        {
            e.Email = TestPortalCryptography.PiiSymmetricEncryption.Encrypt(normalized);
            e.EmailHash = staleHash;
        });
        context.Users.Add(seeded);
        await context.SaveChangesAsync();

        var result = await CreateService(context).ApplyAsync(dryRun: true);

        Assert.True(result.DryRun);
        Assert.Equal(0, result.Updated);
        Assert.True(result.WouldUpdate >= 1);

        context.ChangeTracker.Clear();
        var stored = await context.Users.AsNoTracking().SingleAsync(u => u.Id == seeded.Id);
        Assert.Equal(staleHash, stored.EmailHash);
    }

    [Fact]
    public async Task ApplyAsync_WhenNotDryRun_PersistsNewHash()
    {
        using var context = _fixture.CreateContext();
        var email = $"rehash-apply-{Guid.NewGuid()}@example.com";
        var normalized = email.ToLowerInvariant();
        var targetHash = TestPortalCryptography.EmailLookupHasher.HashNormalized(normalized);
        var staleHash = CreateStaleHasher().HashNormalized(normalized);

        var seeded = UserFactory.CreateUserEntity(e =>
        {
            e.Email = TestPortalCryptography.PiiSymmetricEncryption.Encrypt(normalized);
            e.EmailHash = staleHash;
        });
        context.Users.Add(seeded);
        await context.SaveChangesAsync();

        var result = await CreateService(context).ApplyAsync(dryRun: false);

        Assert.False(result.DryRun);
        Assert.True(result.Updated >= 1);
        Assert.DoesNotContain(seeded.Id, result.CollisionUserIds);

        context.ChangeTracker.Clear();
        var stored = await context.Users.AsNoTracking().SingleAsync(u => u.Id == seeded.Id);
        Assert.Equal(targetHash, stored.EmailHash);
    }

    [Fact]
    public async Task ApplyAsync_WhenDuplicateEmailCollision_SkipsUpdateAndLeavesHashesUnchanged()
    {
        using var context = _fixture.CreateContext();
        var email = $"rehash-collision-{Guid.NewGuid()}@example.com";
        var normalized = email.ToLowerInvariant();
        var targetHash = TestPortalCryptography.EmailLookupHasher.HashNormalized(normalized)!;
        var staleHash = CreateStaleHasher().HashNormalized(normalized);

        var alreadyCurrent = UserFactory.CreateUserEntity(e =>
        {
            e.Email = TestPortalCryptography.PiiSymmetricEncryption.Encrypt(normalized);
            e.EmailHash = targetHash;
        });
        var duplicate = UserFactory.CreateUserEntity(e =>
        {
            e.Email = TestPortalCryptography.PiiSymmetricEncryption.Encrypt(normalized);
            e.EmailHash = staleHash;
        });
        context.Users.AddRange(alreadyCurrent, duplicate);
        await context.SaveChangesAsync();

        var result = await CreateService(context).ApplyAsync(dryRun: false);

        Assert.True(result.SkippedCollision >= 1);
        Assert.Contains(alreadyCurrent.Id, result.CollisionUserIds);
        Assert.Contains(duplicate.Id, result.CollisionUserIds);

        context.ChangeTracker.Clear();
        var storedCurrent = await context.Users.AsNoTracking().SingleAsync(u => u.Id == alreadyCurrent.Id);
        var storedDuplicate = await context.Users.AsNoTracking().SingleAsync(u => u.Id == duplicate.Id);
        Assert.Equal(targetHash, storedCurrent.EmailHash);
        Assert.Equal(staleHash, storedDuplicate.EmailHash);
    }
}
