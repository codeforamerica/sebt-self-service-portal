using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Data.Entities;
using SEBT.Portal.Infrastructure.Helpers;
using SEBT.Portal.Infrastructure.Repositories;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.Tests.Unit.TestSupport;

namespace SEBT.Portal.Tests.Unit.Repositories;

[Collection("SqlServer")]
[Trait("Category", "SqlServer")]
public class DatabaseUserRepositoryTests : IClassFixture<SqlServerTestFixture>
{
    private readonly SqlServerTestFixture _fixture;
    private static readonly IIdentifierHasher TestHasher = new IdentifierHasher(
        Options.Create(new IdentifierHasherSettings { SecretKey = "TestKeyMustBeAtLeast32CharactersLong!!" }));

    private static readonly IOptions<IdProofingValiditySettings> TestValiditySettings =
        Options.Create(new IdProofingValiditySettings { ValidityDays = 1826 });

    public DatabaseUserRepositoryTests(SqlServerTestFixture fixture)
    {
        _fixture = fixture;
    }

    private PortalDbContext CreateContext()
    {
        return _fixture.CreateContext();
    }

    private static DatabaseUserRepository CreateRepository(PortalDbContext context) =>
        new(
            context,
            TestHasher,
            TestPortalCryptography.PiiSymmetricEncryption,
            TestPortalCryptography.EmailLookupHasher,
            TestValiditySettings);

    [Fact]
    public async Task GetUserByEmailAsync_WhenUserExists_ShouldReturnUser()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);

        var entity = UserFactory.CreateUserEntity(e =>
        {
            e.Email = $"test-{Guid.NewGuid()}@example.com";
            e.IalLevel = (int)UserIalLevel.IAL1plus;
        });
        context.Users.Add(entity);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetUserByEmailAsync(entity.Email!);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(entity.Email, result!.Email);
        Assert.Equal(UserIalLevel.IAL1plus, result.IalLevel);
    }

    [Fact]
    public async Task GetUserByEmailAsync_WhenUserDoesNotExist_ShouldReturnNull()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);

        // Act
        var result = await repository.GetUserByEmailAsync("nonexistent@example.com");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetUserByEmailAsync_ShouldBeCaseInsensitive()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);

        var testEmail = $"test-{Guid.NewGuid()}@example.com";
        var entity = UserFactory.CreateUserEntity(e =>
        {
            e.Email = testEmail.ToLowerInvariant(); // lowercase
            e.IalLevel = (int)UserIalLevel.None;
        });
        context.Users.Add(entity);
        await context.SaveChangesAsync();

        // Act - query with different casing
        var result1 = await repository.GetUserByEmailAsync(testEmail.ToUpperInvariant());
        var result2 = await repository.GetUserByEmailAsync(testEmail);
        var result3 = await repository.GetUserByEmailAsync(testEmail.ToLowerInvariant());

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotNull(result3);
        var normalizedEmail = testEmail.ToLowerInvariant();
        Assert.Equal(normalizedEmail, result1!.Email);
        Assert.Equal(normalizedEmail, result2!.Email);
        Assert.Equal(normalizedEmail, result3!.Email);
    }

    [Fact]
    public async Task GetUserByEmailAsync_WhenEmailIsNull_ShouldReturnNull()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);

        // Act
        var result = await repository.GetUserByEmailAsync(null!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetUserByEmailAsync_WhenEmailIsWhitespace_ShouldReturnNull()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);

        // Act
        var result = await repository.GetUserByEmailAsync("   ");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateUserAsync_ShouldStoreUserInDatabase()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);

        var uniqueEmail = $"newuser-{Guid.NewGuid()}@example.com";
        var user = new User
        {
            Email = uniqueEmail,
            IalLevel = UserIalLevel.IAL1,
            IdProofingSessionId = "session-123",
            IdProofingCompletedAt = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        await repository.CreateUserAsync(user, CancellationToken.None);

        // Assert
        var normalizedEmail = uniqueEmail.ToLowerInvariant();
        var stored = await FindStoredUserWithLegacyPlaintextFallbackAsync(context, normalizedEmail);
        Assert.NotNull(stored);
        AssertPersistedNormalizedEmailCipher(normalizedEmail, stored!.Email);
        Assert.Equal((int)UserIalLevel.IAL1, stored.IalLevel);
        Assert.Equal("session-123", stored.IdProofingSessionId);
    }

    [Fact]
    public async Task CreateUserAsync_ShouldNormalizeEmailToLowercase()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);

        var uniqueId = Guid.NewGuid();
        var user = UserFactory.CreateUserWithEmail($"USER-{uniqueId}@EXAMPLE.COM", u =>
        {
            u.IalLevel = UserIalLevel.None;
        });

        // Act
        await repository.CreateUserAsync(user, CancellationToken.None);

        // Assert
        var normalizedEmail = $"user-{uniqueId}@example.com";
        var stored = await FindStoredUserWithLegacyPlaintextFallbackAsync(context, normalizedEmail);
        Assert.NotNull(stored);
        AssertPersistedNormalizedEmailCipher(normalizedEmail, stored!.Email);
    }

    [Fact]
    public async Task CreateUserAsync_WhenUserIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.CreateUserAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task CreateUserAsync_WhenEmailIsEmpty_ShouldThrowArgumentException()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);

        var user = UserFactory.CreateUserWithEmail("", u =>
        {
            u.IalLevel = UserIalLevel.None;
        });

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => repository.CreateUserAsync(user, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateUserAsync_ShouldUpdateExistingUser()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);

        var uniqueEmail = $"update-{Guid.NewGuid()}@example.com";
        var entity = UserFactory.CreateUserEntity(e =>
        {
            e.Email = uniqueEmail;
            e.IalLevel = (int)UserIalLevel.None;
        });
        context.Users.Add(entity);
        await context.SaveChangesAsync();

        var completedAt = DateTime.UtcNow;
        var user = UserFactory.CreateUserWithEmail(uniqueEmail, u =>
        {
            u.IalLevel = UserIalLevel.IAL1plus;
            u.IdProofingSessionId = "new-session-456";
            u.IdProofingCompletedAt = completedAt;
        });
        // Set init-only properties using reflection
        var idProperty = typeof(User).GetProperty(nameof(User.Id));
        var createdAtProperty = typeof(User).GetProperty(nameof(User.CreatedAt));
        idProperty?.SetValue(user, entity.Id);
        createdAtProperty?.SetValue(user, entity.CreatedAt);

        // Act
        await repository.UpdateUserAsync(user, CancellationToken.None);

        // Assert
        var updated = await FindStoredUserWithLegacyPlaintextFallbackAsync(context, uniqueEmail);
        Assert.NotNull(updated);
        Assert.Equal((int)UserIalLevel.IAL1plus, updated!.IalLevel);
        Assert.Equal("new-session-456", updated.IdProofingSessionId);
        Assert.NotNull(updated.IdProofingCompletedAt);
        Assert.Equal(
            completedAt.AddDays(TestValiditySettings.Value.ValidityDays),
            updated.IdProofingExpiresAt);
    }

    [Fact]
    public async Task UpdateUserAsync_ShouldUpdateUpdatedAtTimestamp()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);

        var uniqueEmail = $"timestamp-{Guid.NewGuid()}@example.com";
        var originalTime = DateTime.UtcNow.AddMinutes(-5);
        var entity = UserFactory.CreateUserEntity(e =>
        {
            e.Email = uniqueEmail;
            e.IalLevel = (int)UserIalLevel.None;
            e.CreatedAt = originalTime;
            e.UpdatedAt = originalTime;
        });
        context.Users.Add(entity);
        await context.SaveChangesAsync();

        await Task.Delay(10); // Small delay to ensure timestamp difference

        var user = UserFactory.CreateUserWithEmail(uniqueEmail, u =>
        {
            u.IalLevel = UserIalLevel.IAL1;
        });
        // Set init-only properties using reflection
        var idProperty = typeof(User).GetProperty(nameof(User.Id));
        var createdAtProperty = typeof(User).GetProperty(nameof(User.CreatedAt));
        idProperty?.SetValue(user, entity.Id);
        createdAtProperty?.SetValue(user, originalTime);

        // Act
        await repository.UpdateUserAsync(user, CancellationToken.None);

        // Assert
        var updated = await FindStoredUserWithLegacyPlaintextFallbackAsync(context, uniqueEmail);
        Assert.NotNull(updated);
        Assert.True(updated!.UpdatedAt > originalTime);
    }

    [Fact]
    public async Task UpdateUserAsync_WhenUserDoesNotExist_ShouldThrowInvalidOperationException()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);

        var user = UserFactory.CreateUserWithEmail("nonexistent@example.com", u =>
        {
            u.IalLevel = UserIalLevel.IAL1plus;
        });
        // Set Id to a non-existent value (reflection because User.Id is init-only)
        var idProperty = typeof(User).GetProperty(nameof(User.Id));
        idProperty?.SetValue(user, Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.UpdateUserAsync(user, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateUserAsync_WhenUserIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.UpdateUserAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateUserAsync_ShouldBeCaseInsensitive()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);

        var uniqueId = Guid.NewGuid();
        var baseEmail = $"case-{uniqueId}@example.com";
        var entity = UserFactory.CreateUserEntity(e =>
        {
            e.Email = baseEmail;
            e.IalLevel = (int)UserIalLevel.None;
        });
        context.Users.Add(entity);
        await context.SaveChangesAsync();

        var user = UserFactory.CreateUserWithEmail(baseEmail.ToUpperInvariant(), u =>
        {
            u.IalLevel = UserIalLevel.IAL1plus;
        });
        // Set init-only properties using reflection
        var idProperty = typeof(User).GetProperty(nameof(User.Id));
        var createdAtProperty = typeof(User).GetProperty(nameof(User.CreatedAt));
        idProperty?.SetValue(user, entity.Id);
        createdAtProperty?.SetValue(user, entity.CreatedAt);

        // Act
        await repository.UpdateUserAsync(user, CancellationToken.None);

        // Assert
        var updated = await FindStoredUserWithLegacyPlaintextFallbackAsync(context, baseEmail);
        Assert.NotNull(updated);
        Assert.Equal((int)UserIalLevel.IAL1plus, updated!.IalLevel);
    }

    [Fact]
    public async Task UpdateUserAsync_ShouldUpdateEmail()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);

        var originalEmail = $"original-{Guid.NewGuid()}@example.com";
        var newEmail = $"new-{Guid.NewGuid()}@example.com";
        var entity = UserFactory.CreateUserEntity(e =>
        {
            e.Email = originalEmail;
            e.IalLevel = (int)UserIalLevel.None;
        });
        context.Users.Add(entity);
        await context.SaveChangesAsync();

        var user = UserFactory.CreateUserWithEmail(newEmail, u =>
        {
            u.IalLevel = UserIalLevel.IAL1plus;
        });
        // Set init-only properties using reflection
        var idProperty = typeof(User).GetProperty(nameof(User.Id));
        var createdAtProperty = typeof(User).GetProperty(nameof(User.CreatedAt));
        idProperty?.SetValue(user, entity.Id);
        createdAtProperty?.SetValue(user, entity.CreatedAt);

        // Act
        await repository.UpdateUserAsync(user, CancellationToken.None);

        // Assert
        var updated = await context.Users.FirstOrDefaultAsync(u => u.Id == entity.Id);
        Assert.NotNull(updated);
        AssertPersistedNormalizedEmailCipher(newEmail.ToLowerInvariant(), updated!.Email);
        Assert.Equal((int)UserIalLevel.IAL1plus, updated.IalLevel);
    }

    [Fact]
    public async Task UpdateUserAsync_WhenEmailAlreadyExists_ShouldThrowInvalidOperationException()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);

        var existingEmail = $"existing-{Guid.NewGuid()}@example.com";
        var entity1 = UserFactory.CreateUserEntity(e =>
        {
            e.Email = existingEmail;
            e.IalLevel = (int)UserIalLevel.None;
        });
        context.Users.Add(entity1);

        var originalEmail = $"original-{Guid.NewGuid()}@example.com";
        var entity2 = UserFactory.CreateUserEntity(e =>
        {
            e.Email = originalEmail;
            e.IalLevel = (int)UserIalLevel.None;
        });
        context.Users.Add(entity2);
        await context.SaveChangesAsync();

        var user = UserFactory.CreateUserWithEmail(existingEmail, u =>
        {
            u.IalLevel = UserIalLevel.IAL1plus;
        });
        // Set init-only properties using reflection
        var idProperty = typeof(User).GetProperty(nameof(User.Id));
        var createdAtProperty = typeof(User).GetProperty(nameof(User.CreatedAt));
        idProperty?.SetValue(user, entity2.Id); // Try to change entity2's email to entity1's email
        createdAtProperty?.SetValue(user, entity2.CreatedAt);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.UpdateUserAsync(user, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateUserAsync_WhenUserHasSsn_ShouldStoreSsnAsHash()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);

        var uniqueEmail = $"ssn-update-{Guid.NewGuid()}@example.com";
        var entity = UserFactory.CreateUserEntity(e =>
        {
            e.Email = uniqueEmail;
            e.IdProofingStatus = (int)IdProofingStatus.NotStarted;
        });
        context.Users.Add(entity);
        await context.SaveChangesAsync();

        var user = UserFactory.CreateUserWithEmail(uniqueEmail, u =>
        {
            u.IdProofingStatus = IdProofingStatus.Completed;
            u.Ssn = "123-45-6789";
        });
        var idProperty = typeof(User).GetProperty(nameof(User.Id));
        var createdAtProperty = typeof(User).GetProperty(nameof(User.CreatedAt));
        idProperty?.SetValue(user, entity.Id);
        createdAtProperty?.SetValue(user, entity.CreatedAt);

        // Act
        await repository.UpdateUserAsync(user, CancellationToken.None);

        // Assert - SSN stored as HMAC-SHA256 hash, not plaintext
        var updated = await FindStoredUserWithLegacyPlaintextFallbackAsync(context, uniqueEmail);
        Assert.NotNull(updated);
        Assert.NotNull(updated!.Ssn);
        Assert.Equal(64, updated.Ssn.Length);
        Assert.NotEqual("123-45-6789", updated.Ssn);
        Assert.NotEqual("123456789", updated.Ssn);
    }

    [Fact]
    public async Task GetOrCreateUserAsync_WhenUserExists_ShouldReturnExistingUser()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);

        var uniqueEmail = $"existing-{Guid.NewGuid()}@example.com";
        var entity = UserFactory.CreateUserEntity(e =>
        {
            e.Email = uniqueEmail;
            e.IalLevel = (int)UserIalLevel.IAL1plus;
            e.CreatedAt = DateTime.UtcNow.AddDays(-1);
            e.UpdatedAt = DateTime.UtcNow.AddDays(-1);
        });
        context.Users.Add(entity);
        await context.SaveChangesAsync();

        // Act
        var (result, isNewUser) = await repository.GetOrCreateUserAsync(uniqueEmail, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.False(isNewUser);
        Assert.Equal(uniqueEmail, result.Email);
        Assert.Equal(UserIalLevel.IAL1plus, result.IalLevel);
        Assert.Equal(entity.CreatedAt, result.CreatedAt);

        // Verify only one user exists with this email
        var count = await CountNormalizedEmailMatchesAsync(context, uniqueEmail);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetOrCreateUserAsync_WhenUserDoesNotExist_ShouldCreateNewUser()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);

        var uniqueEmail = $"newuser-{Guid.NewGuid()}@example.com";

        // Act
        var (result, isNewUser) = await repository.GetOrCreateUserAsync(uniqueEmail, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(isNewUser);
        Assert.Equal(uniqueEmail, result.Email);
        Assert.Equal(UserIalLevel.None, result.IalLevel);
        Assert.NotEqual(default(DateTime), result.CreatedAt);
        Assert.NotEqual(default(DateTime), result.UpdatedAt);

        // Verify user was saved
        var stored = await FindStoredUserWithLegacyPlaintextFallbackAsync(context, uniqueEmail);
        Assert.NotNull(stored);
    }

    [Fact]
    public async Task GetOrCreateUserAsync_ShouldNormalizeEmailToLowercase()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);

        var uniqueId = Guid.NewGuid();
        var mixedCaseEmail = $"MIXED-{uniqueId}@CASE.COM";

        // Act
        var (result, isNewUser) = await repository.GetOrCreateUserAsync(mixedCaseEmail);

        // Assert
        Assert.NotNull(result);
        Assert.True(isNewUser);
        var expectedEmail = $"mixed-{uniqueId}@case.com";
        Assert.Equal(expectedEmail, result.Email);

        // Verify stored with lowercase
        var stored = await FindStoredUserWithLegacyPlaintextFallbackAsync(context, expectedEmail);
        Assert.NotNull(stored);
    }

    [Fact]
    public async Task GetOrCreateUserAsync_WhenEmailIsNull_ShouldThrowArgumentException()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetOrCreateUserAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task GetOrCreateUserAsync_WhenEmailIsWhitespace_ShouldThrowArgumentException()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetOrCreateUserAsync("   ", CancellationToken.None));
    }

    [Fact]
    public async Task GetUserBySessionIdAsync_WhenSessionExists_ShouldReturnUser()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);

        var uniqueEmail = $"session-{Guid.NewGuid()}@example.com";
        var sessionId = $"session-{Guid.NewGuid()}";
        var entity = UserFactory.CreateUserEntity(e =>
        {
            e.Email = uniqueEmail;
            e.IalLevel = (int)UserIalLevel.IAL1;
            e.IdProofingSessionId = sessionId;
        });
        context.Users.Add(entity);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetUserBySessionIdAsync(sessionId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(uniqueEmail, result!.Email);
        Assert.Equal(sessionId, result.IdProofingSessionId);
        Assert.Equal(UserIalLevel.IAL1, result.IalLevel);
    }

    [Fact]
    public async Task GetUserBySessionIdAsync_WhenSessionDoesNotExist_ShouldReturnNull()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);

        // Act
        var result = await repository.GetUserBySessionIdAsync("nonexistent-session");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetUserBySessionIdAsync_WhenSessionIdIsNull_ShouldReturnNull()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);

        // Act
        var result = await repository.GetUserBySessionIdAsync(null!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetUserBySessionIdAsync_WhenSessionIdIsWhitespace_ShouldReturnNull()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);

        // Act
        var result = await repository.GetUserBySessionIdAsync("   ");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetUserBySessionIdAsync_WhenMultipleUsersHaveDifferentSessions_ShouldReturnCorrectUser()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);

        var uniqueId1 = Guid.NewGuid();
        var uniqueId2 = Guid.NewGuid();
        var email1 = $"user1-{uniqueId1}@example.com";
        var email2 = $"user2-{uniqueId2}@example.com";
        var session1 = $"session-{uniqueId1}";
        var session2 = $"session-{uniqueId2}";

        var entity1 = UserFactory.CreateUserEntity(e =>
        {
            e.Email = email1;
            e.IalLevel = (int)UserIalLevel.IAL1;
            e.IdProofingSessionId = session1;
        });
        var entity2 = UserFactory.CreateUserEntity(e =>
        {
            e.Email = email2;
            e.IalLevel = (int)UserIalLevel.IAL1;
            e.IdProofingSessionId = session2;
        });
        context.Users.AddRange(entity1, entity2);
        await context.SaveChangesAsync();

        // Act
        var result1 = await repository.GetUserBySessionIdAsync(session1);
        var result2 = await repository.GetUserBySessionIdAsync(session2);

        // Assert
        Assert.NotNull(result1);
        Assert.Equal(email1, result1!.Email);
        Assert.NotNull(result2);
        Assert.Equal(email2, result2!.Email);
    }

    [Fact]
    public async Task GetUserBySessionIdAsync_WhenUserHasNullSessionId_ShouldNotReturnUser()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);

        var uniqueEmail = $"nosession-{Guid.NewGuid()}@example.com";
        var entity = UserFactory.CreateUserEntity(e =>
        {
            e.Email = uniqueEmail;
            e.IalLevel = (int)UserIalLevel.None;
            e.IdProofingSessionId = null;
        });
        context.Users.Add(entity);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetUserBySessionIdAsync("any-session");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task MapToDomainModel_ShouldCorrectlyMapAllProperties()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);

        var uniqueEmail = $"mapping-{Guid.NewGuid()}@example.com";
        var completedAt = DateTime.UtcNow.AddDays(-1);
        var expiresAt = DateTime.UtcNow.AddYears(1);
        var createdAt = DateTime.UtcNow.AddDays(-5);
        var updatedAt = DateTime.UtcNow.AddDays(-2);

        var coLoadedUpdated = DateTime.UtcNow.AddDays(-3);
        var entity = UserFactory.CreateCoLoadedUserEntity(e =>
        {
            e.Email = uniqueEmail;
            e.IalLevel = (int)UserIalLevel.IAL1plus;
            e.IdProofingSessionId = "test-session";
            e.IdProofingCompletedAt = completedAt;
            e.IdProofingExpiresAt = expiresAt;
            e.CoLoadedLastUpdated = coLoadedUpdated;
            e.CreatedAt = createdAt;
            e.UpdatedAt = updatedAt;
        });
        context.Users.Add(entity);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetUserByEmailAsync(uniqueEmail);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(uniqueEmail, result!.Email);
        Assert.Equal(UserIalLevel.IAL1plus, result.IalLevel);
        Assert.Equal("test-session", result.IdProofingSessionId);
        Assert.Equal(completedAt, result.IdProofingCompletedAt);
#pragma warning disable CS0618 // User.IdProofingExpiresAt — verifying EF ↔ domain mapping until column retired
        Assert.Equal(expiresAt, result.IdProofingExpiresAt);
#pragma warning restore CS0618
        Assert.True(result.IsCoLoaded);
        Assert.Equal(coLoadedUpdated, result.CoLoadedLastUpdated);
        Assert.Equal(createdAt, result.CreatedAt);
        Assert.Equal(updatedAt, result.UpdatedAt);
    }

    [Fact]
    public async Task CreateUserAsync_WhenUserHasIdentifierFields_ShouldStoreSsnAsHashAndOthersEncrypted()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);

        var uniqueEmail = $"hash-{Guid.NewGuid()}@example.com";
        var user = UserFactory.CreateUserWithEmail(uniqueEmail, u =>
        {
            u.Phone = "8185558439";
            u.SnapId = "SNAP123";
            u.TanfId = "TANF456";
            u.Ssn = "123456789";
        });

        // Act
        await repository.CreateUserAsync(user, CancellationToken.None);

        // Assert - reversible identifiers persist as ciphertext; SSN stored as HMAC-SHA256 hash
        var stored = await FindStoredUserWithLegacyPlaintextFallbackAsync(context, uniqueEmail);
        Assert.NotNull(stored);
        Assert.Equal(
            "8185558439",
            TestPortalCryptography.PiiSymmetricEncryption.DecryptOrPassThroughLegacy(stored!.Phone));
        Assert.Equal(
            "SNAP123",
            TestPortalCryptography.PiiSymmetricEncryption.DecryptOrPassThroughLegacy(stored.SnapId));
        Assert.Equal(
            "TANF456",
            TestPortalCryptography.PiiSymmetricEncryption.DecryptOrPassThroughLegacy(stored.TanfId));
        Assert.NotNull(stored.Ssn);
        Assert.Equal(64, stored.Ssn.Length);
        Assert.NotEqual("123456789", stored.Ssn);
    }

    [Fact]
    public async Task CreateUserAsync_ShouldPreserveAllUserProperties()
    {
        // Arrange
        using var context = CreateContext();
        var repository = CreateRepository(context);

        var uniqueEmail = $"fulluser-{Guid.NewGuid()}@example.com";
        var completedAt = DateTime.UtcNow.AddDays(-1);
        var expiresAt = DateTime.UtcNow.AddYears(1);
        var coLoadedUpdated = DateTime.UtcNow.AddDays(-2);

        var user = UserFactory.CreateUserWithEmail(uniqueEmail, u =>
        {
            u.IalLevel = UserIalLevel.None;
            u.IdProofingSessionId = "full-session";
            u.IdProofingCompletedAt = completedAt;
            u.IsCoLoaded = true;
            u.CoLoadedLastUpdated = coLoadedUpdated;
            u.UpdatedAt = DateTime.UtcNow.AddDays(-5);
        });
#pragma warning disable CS0618 // User.IdProofingExpiresAt — persistence round-trip for legacy column
        user.IdProofingExpiresAt = expiresAt;
#pragma warning restore CS0618
        // Set init-only CreatedAt using reflection
        var createdAtProperty = typeof(User).GetProperty(nameof(User.CreatedAt));
        createdAtProperty?.SetValue(user, DateTime.UtcNow.AddDays(-10));

        // Act
        await repository.CreateUserAsync(user, CancellationToken.None);

        // Assert
        var stored = await FindStoredUserWithLegacyPlaintextFallbackAsync(context, uniqueEmail);
        Assert.NotNull(stored);
        Assert.Equal((int)UserIalLevel.None, stored!.IalLevel);
        Assert.Equal("full-session", stored.IdProofingSessionId);
        Assert.Equal(completedAt, stored.IdProofingCompletedAt);
        Assert.Equal(
            completedAt.AddDays(TestValiditySettings.Value.ValidityDays),
            stored.IdProofingExpiresAt);
        Assert.True(stored.IsCoLoaded);
        Assert.Equal(coLoadedUpdated, stored.CoLoadedLastUpdated);
    }

    [Fact]
    public async Task GetUserByEmailAsync_WhenEnvelopeOrphanWithoutEmailHash_FindsUser()
    {
        using var context = CreateContext();
        var repository = CreateRepository(context);
        var email = $"orphan-{Guid.NewGuid()}@example.com";
        var normalized = email.ToLowerInvariant();
        var encryptedEmail = TestPortalCryptography.PiiSymmetricEncryption.Encrypt(normalized);

        context.Users.Add(UserFactory.CreateUserEntity(e =>
        {
            e.Email = encryptedEmail;
            e.EmailHash = null;
        }));
        await context.SaveChangesAsync();

        var result = await repository.GetUserByEmailAsync(email);

        Assert.NotNull(result);
        Assert.Equal(normalized, result!.Email);
    }

    [Fact]
    public async Task GetOrCreateUserAsync_WhenEnvelopeOrphanWithoutEmailHash_ReturnsExistingUser()
    {
        using var context = CreateContext();
        var repository = CreateRepository(context);
        var email = $"orphan-create-{Guid.NewGuid()}@example.com";
        var normalized = email.ToLowerInvariant();
        var encryptedEmail = TestPortalCryptography.PiiSymmetricEncryption.Encrypt(normalized);

        var seeded = UserFactory.CreateUserEntity(e =>
        {
            e.Email = encryptedEmail;
            e.EmailHash = null;
        });
        context.Users.Add(seeded);
        await context.SaveChangesAsync();

        var (user, isNew) = await repository.GetOrCreateUserAsync(email);

        Assert.False(isNew);
        Assert.Equal(seeded.Id, user.Id);
        Assert.Equal(normalized, user.Email);
    }

    private static async Task<UserEntity?> FindStoredUserWithLegacyPlaintextFallbackAsync(
        PortalDbContext ctx,
        string normalizedEmailPlaintext)
    {
        var fingerprint =
            TestPortalCryptography.EmailLookupHasher.HashNormalized(normalizedEmailPlaintext)!;

        return await ctx.Users.FirstOrDefaultAsync(
            u =>
                u.EmailHash == fingerprint ||
                u.EmailHash == null && u.Email != null && u.Email == normalizedEmailPlaintext,
            CancellationToken.None);
    }

    private static async Task<int> CountNormalizedEmailMatchesAsync(PortalDbContext ctx, string normalizedEmail)
    {
        var fingerprint =
            TestPortalCryptography.EmailLookupHasher.HashNormalized(normalizedEmail)!;

        return await ctx.Users.CountAsync(
            u =>
                u.EmailHash == fingerprint ||
                u.EmailHash == null && u.Email != null && u.Email == normalizedEmail,
            CancellationToken.None);
    }

    private static void AssertPersistedNormalizedEmailCipher(string expectedNormalizedPlaintext, string? ciphertextEmail)
    {
        Assert.False(string.IsNullOrEmpty(ciphertextEmail));
        var plain =
            TestPortalCryptography.PiiSymmetricEncryption.DecryptOrPassThroughLegacy(ciphertextEmail!);
        Assert.False(string.IsNullOrEmpty(plain));
        Assert.Equal(expectedNormalizedPlaintext, plain);
    }
}

