using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Exceptions;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Data.Entities;
using SEBT.Portal.Infrastructure.Repositories;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.Tests.Unit.TestSupport;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Repositories;

/// <summary>
/// InMemory unit tests for <see cref="DatabaseUserRepository"/>.
/// </summary>
public class DatabaseUserRepositoryUnitTests
{
    private static readonly IIdentifierHasher Hasher = new IdentifierHasher(
        Options.Create(new IdentifierHasherSettings
        {
            SecretKey = TestPortalCryptography.TestIdentifierHasherSecretKey
        }));

    private static PortalDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new PortalDbContext(options);
    }

    private static DatabaseUserRepository CreateRepository(
        PortalDbContext context,
        IEmailLookupHasher? lookup = null,
        IPiiSymmetricEncryption? crypto = null) =>
        new(
            context,
            Hasher,
            crypto ?? TestPortalCryptography.PiiSymmetricEncryption,
            lookup ?? TestPortalCryptography.EmailLookupHasher);

    [Fact]
    public async Task GetUserByIdAsync_whenUserExists_returnsUser()
    {
        using var context = CreateContext();
        var entity = new UserEntity { ExternalProviderId = "sub-1" };
        context.Users.Add(entity);
        await context.SaveChangesAsync();
        var repo = CreateRepository(context);

        var result = await repo.GetUserByIdAsync(entity.Id);

        Assert.NotNull(result);
        Assert.Equal(entity.Id, result!.Id);
        Assert.Equal("sub-1", result.ExternalProviderId);
    }

    [Fact]
    public async Task GetUserByIdAsync_whenMissingOrEmpty_returnsNull()
    {
        using var context = CreateContext();
        var repo = CreateRepository(context);

        Assert.Null(await repo.GetUserByIdAsync(Guid.NewGuid()));
        Assert.Null(await repo.GetUserByIdAsync(Guid.Empty));
    }

    [Fact]
    public async Task GetUserByExternalIdAsync_whenUserExists_returnsUser()
    {
        using var context = CreateContext();
        var entity = new UserEntity { ExternalProviderId = "oidc-sub" };
        context.Users.Add(entity);
        await context.SaveChangesAsync();
        var repo = CreateRepository(context);

        var result = await repo.GetUserByExternalIdAsync("oidc-sub");

        Assert.NotNull(result);
        Assert.Equal(entity.Id, result!.Id);
    }

    [Fact]
    public async Task GetUserByExternalIdAsync_whenMissingOrBlank_returnsNull()
    {
        using var context = CreateContext();
        var repo = CreateRepository(context);

        Assert.Null(await repo.GetUserByExternalIdAsync("missing"));
        Assert.Null(await repo.GetUserByExternalIdAsync("  "));
        Assert.Null(await repo.GetUserByExternalIdAsync(null!));
    }

    [Fact]
    public async Task GetOrCreateUserByExternalIdAsync_whenMissing_createsUser()
    {
        using var context = CreateContext();
        var repo = CreateRepository(context);

        var (user, isNew) = await repo.GetOrCreateUserByExternalIdAsync("new-sub");

        Assert.True(isNew);
        Assert.Equal("new-sub", user.ExternalProviderId);
        Assert.NotNull(await context.Users.SingleAsync(u => u.ExternalProviderId == "new-sub"));
    }

    [Fact]
    public async Task GetOrCreateUserByExternalIdAsync_whenExists_returnsExisting()
    {
        using var context = CreateContext();
        var entity = new UserEntity { ExternalProviderId = "existing-sub" };
        context.Users.Add(entity);
        await context.SaveChangesAsync();
        var repo = CreateRepository(context);

        var (user, isNew) = await repo.GetOrCreateUserByExternalIdAsync("existing-sub");

        Assert.False(isNew);
        Assert.Equal(entity.Id, user.Id);
        Assert.Equal(1, await context.Users.CountAsync());
    }

    [Fact]
    public async Task GetOrCreateUserByExternalIdAsync_whenLegacyEmailUser_adoptsRecord()
    {
        using var context = CreateContext();
        var email = "legacy@example.com";
        var entity = new UserEntity
        {
            Email = email,
            EmailHash = TestPortalCryptography.EmailLookupHasher.HashNormalized(email),
            ExternalProviderId = null
        };
        context.Users.Add(entity);
        await context.SaveChangesAsync();
        var repo = CreateRepository(context);

        var (user, isNew) = await repo.GetOrCreateUserByExternalIdAsync("oidc-sub", email);

        Assert.False(isNew);
        Assert.Equal(entity.Id, user.Id);
        var stored = await context.Users.SingleAsync(u => u.Id == entity.Id);
        Assert.Equal("oidc-sub", stored.ExternalProviderId);
        Assert.Null(stored.Email);
        Assert.Null(stored.EmailHash);
    }

    [Fact]
    public async Task GetOrCreateUserByExternalIdAsync_whenLegacyAlreadyHasExternalId_createsNew()
    {
        using var context = CreateContext();
        var email = "taken@example.com";
        context.Users.Add(new UserEntity
        {
            Email = email,
            EmailHash = TestPortalCryptography.EmailLookupHasher.HashNormalized(email),
            ExternalProviderId = "already-bound"
        });
        await context.SaveChangesAsync();
        var repo = CreateRepository(context);

        var (user, isNew) = await repo.GetOrCreateUserByExternalIdAsync("new-sub", email);

        Assert.True(isNew);
        Assert.Equal("new-sub", user.ExternalProviderId);
        Assert.Equal(2, await context.Users.CountAsync());
    }

    [Fact]
    public async Task GetOrCreateUserByExternalIdAsync_whenExternalIdBlank_throws()
    {
        using var context = CreateContext();
        var repo = CreateRepository(context);

        await Assert.ThrowsAsync<ArgumentException>(
            () => repo.GetOrCreateUserByExternalIdAsync("  "));
    }

    [Fact]
    public async Task CreateUserAsync_whenOnlyExternalProviderId_persists()
    {
        using var context = CreateContext();
        var repo = CreateRepository(context);
        var user = new User { ExternalProviderId = "oidc-only" };

        await repo.CreateUserAsync(user);

        var stored = await context.Users.SingleAsync(u => u.Id == user.Id);
        Assert.Equal("oidc-only", stored.ExternalProviderId);
        Assert.Null(stored.Email);
    }

    [Fact]
    public async Task CreateUserAsync_whenEmailAndExternalIdMissing_throws()
    {
        using var context = CreateContext();
        var repo = CreateRepository(context);

        await Assert.ThrowsAsync<ArgumentException>(
            () => repo.CreateUserAsync(new User()));
    }

    [Fact]
    public async Task UpdateUserAsync_whenIdEmpty_throws()
    {
        using var context = CreateContext();
        var repo = CreateRepository(context);

        await Assert.ThrowsAsync<ArgumentException>(
            () => repo.UpdateUserAsync(new User { Id = Guid.Empty, Email = "a@example.com" }));
    }

    [Fact]
    public async Task UpdateUserAsync_whenEmailIsNull_leavesEmailColumns()
    {
        using var context = CreateContext();
        var email = "keep@example.com";
        var hash = TestPortalCryptography.EmailLookupHasher.HashNormalized(email);
        var entity = new UserEntity
        {
            Email = email,
            EmailHash = hash,
            Phone = "old"
        };
        context.Users.Add(entity);
        await context.SaveChangesAsync();
        var repo = CreateRepository(context);

        await repo.UpdateUserAsync(new User
        {
            Id = entity.Id,
            Email = null,
            Phone = "phone-ext",
            CreatedAt = entity.CreatedAt
        });

        var stored = await context.Users.SingleAsync(u => u.Id == entity.Id);
        Assert.Equal(email, stored.Email);
        Assert.Equal(hash, stored.EmailHash);
        Assert.Equal(
            "phone-ext",
            TestPortalCryptography.PiiSymmetricEncryption.DecryptOrPassThroughLegacy(stored.Phone));
    }

    [Fact]
    public async Task UpdateUserAsync_whenEmailCollidesWithEnvelopeOrphan_throws()
    {
        using var context = CreateContext();
        var takenEmail = "taken@example.com";
        context.Users.Add(new UserEntity
        {
            Email = TestPortalCryptography.PiiSymmetricEncryption.Encrypt(takenEmail),
            EmailHash = null
        });
        var updater = new UserEntity
        {
            Email = "other@example.com",
            EmailHash = TestPortalCryptography.EmailLookupHasher.HashNormalized("other@example.com")
        };
        context.Users.Add(updater);
        await context.SaveChangesAsync();
        var repo = CreateRepository(context);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repo.UpdateUserAsync(new User { Id = updater.Id, Email = takenEmail }));

        Assert.Equal("A user with this email address already exists.", ex.Message);
        var stored = await context.Users.SingleAsync(u => u.Id == updater.Id);
        Assert.Equal("other@example.com", stored.Email);
    }

    [Fact]
    public async Task GetUserByEmailAsync_whenLookupHashIsNull_returnsNull()
    {
        using var context = CreateContext();
        var lookup = Substitute.For<IEmailLookupHasher>();
        lookup.HashNormalized(Arg.Any<string?>()).Returns((string?)null);
        var repo = CreateRepository(context, lookup);

        var result = await repo.GetUserByEmailAsync("person@example.com");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetUserByEmailAsync_whenEnvelopeOrphanDecryptFails_returnsNull()
    {
        using var context = CreateContext();
        var crypto = Substitute.For<IPiiSymmetricEncryption>();
        crypto.DecryptOrPassThroughLegacy(Arg.Any<string?>())
            .Returns(_ => throw new PiiDecryptException("tampered"));
        context.Users.Add(new UserEntity
        {
            Email = PiiAesGcmSymmetricEncryption.EnvelopePrefix + "orphan",
            EmailHash = null
        });
        await context.SaveChangesAsync();
        var repo = CreateRepository(context, crypto: crypto);

        var result = await repo.GetUserByEmailAsync("person@example.com");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetUserByEmailAsync_whenUserExistsByHash_returnsUser()
    {
        using var context = CreateContext();
        var email = "hashed@example.com";
        context.Users.Add(new UserEntity
        {
            Email = TestPortalCryptography.PiiSymmetricEncryption.Encrypt(email),
            EmailHash = TestPortalCryptography.EmailLookupHasher.HashNormalized(email)
        });
        await context.SaveChangesAsync();
        var repo = CreateRepository(context);

        var result = await repo.GetUserByEmailAsync(email);

        Assert.NotNull(result);
        Assert.Equal(email, result!.Email);
    }

    [Fact]
    public async Task GetUserByEmailAsync_whenEmailBlank_returnsNull()
    {
        using var context = CreateContext();
        var repo = CreateRepository(context);

        Assert.Null(await repo.GetUserByEmailAsync(null!));
        Assert.Null(await repo.GetUserByEmailAsync("  "));
    }

    [Fact]
    public async Task GetOrCreateUserAsync_whenMissing_createsUser()
    {
        using var context = CreateContext();
        var repo = CreateRepository(context);

        var (user, isNew) = await repo.GetOrCreateUserAsync("new@example.com");

        Assert.True(isNew);
        Assert.Equal("new@example.com", user.Email);
    }

    [Fact]
    public async Task GetOrCreateUserAsync_whenExists_returnsExisting()
    {
        using var context = CreateContext();
        var email = "existing@example.com";
        var entity = new UserEntity
        {
            Email = email,
            EmailHash = TestPortalCryptography.EmailLookupHasher.HashNormalized(email)
        };
        context.Users.Add(entity);
        await context.SaveChangesAsync();
        var repo = CreateRepository(context);

        var (user, isNew) = await repo.GetOrCreateUserAsync(email);

        Assert.False(isNew);
        Assert.Equal(entity.Id, user.Id);
        Assert.Equal(1, await context.Users.CountAsync());
    }

    [Fact]
    public async Task GetOrCreateUserAsync_whenEmailBlank_throws()
    {
        using var context = CreateContext();
        var repo = CreateRepository(context);

        await Assert.ThrowsAsync<ArgumentException>(() => repo.GetOrCreateUserAsync("  "));
    }

    [Fact]
    public async Task GetUserBySessionIdAsync_whenExists_returnsUser()
    {
        using var context = CreateContext();
        var entity = new UserEntity { IdProofingSessionId = "session-abc" };
        context.Users.Add(entity);
        await context.SaveChangesAsync();
        var repo = CreateRepository(context);

        var result = await repo.GetUserBySessionIdAsync("session-abc");

        Assert.NotNull(result);
        Assert.Equal(entity.Id, result!.Id);
    }

    [Fact]
    public async Task GetUserBySessionIdAsync_whenMissingOrBlank_returnsNull()
    {
        using var context = CreateContext();
        var repo = CreateRepository(context);

        Assert.Null(await repo.GetUserBySessionIdAsync("missing"));
        Assert.Null(await repo.GetUserBySessionIdAsync("  "));
    }
}
