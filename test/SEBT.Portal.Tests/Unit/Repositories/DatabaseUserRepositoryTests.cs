using Microsoft.EntityFrameworkCore;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Data.Entities;
using SEBT.Portal.Infrastructure.Repositories;
using SEBT.Portal.Infrastructure.Seeding.Helpers;
using SEBT.Portal.Tests.Helpers;
using UserFactory = SEBT.Portal.Infrastructure.Seeding.Helpers.UserFactory;

namespace SEBT.Portal.Tests.Unit.Repositories;

[Collection("SqlServer")]
public class DatabaseUserRepositoryTests : IClassFixture<SqlServerTestFixture>
{
    private readonly SqlServerTestFixture _fixture;

    public DatabaseUserRepositoryTests(SqlServerTestFixture fixture)
    {
        _fixture = fixture;
    }

    private PortalDbContext CreateContext()
    {
        return _fixture.CreateContext();
    }

    [Fact]
    public async Task GetUserByEmailAsync_WhenUserExists_ShouldReturnUser()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseUserRepository(context);

        var entity = UserEntityFactory.CreateUserEntity(e =>
        {
            e.Email = $"test-{Guid.NewGuid()}@example.com";
            e.IdProofingStatus = (int)IdProofingStatus.Completed;
        });
        context.Users.Add(entity);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetUserByEmailAsync(entity.Email);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(entity.Email, result!.Email);
        Assert.Equal(IdProofingStatus.Completed, result.IdProofingStatus);
    }

    [Fact]
    public async Task GetUserByEmailAsync_WhenUserDoesNotExist_ShouldReturnNull()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseUserRepository(context);

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
        var repository = new DatabaseUserRepository(context);

        var testEmail = $"test-{Guid.NewGuid()}@example.com";
        var entity = UserEntityFactory.CreateUserEntity(e =>
        {
            e.Email = testEmail.ToLowerInvariant(); // lowercase
            e.IdProofingStatus = (int)IdProofingStatus.NotStarted;
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
        var repository = new DatabaseUserRepository(context);

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
        var repository = new DatabaseUserRepository(context);

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
        var repository = new DatabaseUserRepository(context);

        var uniqueEmail = $"newuser-{Guid.NewGuid()}@example.com";
        var user = new User
        {
            Email = uniqueEmail,
            IdProofingStatus = IdProofingStatus.InProgress,
            IdProofingSessionId = "session-123",
            IdProofingCompletedAt = null,
            IdProofingExpiresAt = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        await repository.CreateUserAsync(user, CancellationToken.None);

        // Assert
        var normalizedEmail = uniqueEmail.ToLowerInvariant();
        var stored = await context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
        Assert.NotNull(stored);
        Assert.Equal(normalizedEmail, stored!.Email);
        Assert.Equal((int)IdProofingStatus.InProgress, stored.IdProofingStatus);
        Assert.Equal("session-123", stored.IdProofingSessionId);
    }

    [Fact]
    public async Task CreateUserAsync_ShouldNormalizeEmailToLowercase()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseUserRepository(context);

        var uniqueId = Guid.NewGuid();
        var user = UserFactory.CreateUserWithEmail($"USER-{uniqueId}@EXAMPLE.COM", u =>
        {
            u.IdProofingStatus = IdProofingStatus.NotStarted;
        });

        // Act
        await repository.CreateUserAsync(user, CancellationToken.None);

        // Assert
        var normalizedEmail = $"user-{uniqueId}@example.com";
        var stored = await context.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
        Assert.NotNull(stored);
        Assert.Equal(normalizedEmail, stored!.Email);
    }

    [Fact]
    public async Task CreateUserAsync_WhenUserIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseUserRepository(context);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.CreateUserAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task CreateUserAsync_WhenEmailIsEmpty_ShouldThrowArgumentException()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseUserRepository(context);

        var user = UserFactory.CreateUserWithEmail("", u =>
        {
            u.IdProofingStatus = IdProofingStatus.NotStarted;
        });

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => repository.CreateUserAsync(user, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateUserAsync_ShouldUpdateExistingUser()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseUserRepository(context);

        var uniqueEmail = $"update-{Guid.NewGuid()}@example.com";
        var entity = UserEntityFactory.CreateUserEntity(e =>
        {
            e.Email = uniqueEmail;
            e.IdProofingStatus = (int)IdProofingStatus.NotStarted;
        });
        context.Users.Add(entity);
        await context.SaveChangesAsync();

        var user = UserFactory.CreateUserWithEmail(uniqueEmail, u =>
        {
            u.IdProofingStatus = IdProofingStatus.Completed;
            u.IdProofingSessionId = "new-session-456";
            u.IdProofingCompletedAt = DateTime.UtcNow;
            u.IdProofingExpiresAt = DateTime.UtcNow.AddYears(1);
        });
        // Set init-only properties using reflection
        var idProperty = typeof(User).GetProperty(nameof(User.Id));
        var createdAtProperty = typeof(User).GetProperty(nameof(User.CreatedAt));
        idProperty?.SetValue(user, entity.Id);
        createdAtProperty?.SetValue(user, entity.CreatedAt);

        // Act
        await repository.UpdateUserAsync(user, CancellationToken.None);

        // Assert
        var updated = await context.Users.FirstOrDefaultAsync(u => u.Email == uniqueEmail);
        Assert.NotNull(updated);
        Assert.Equal((int)IdProofingStatus.Completed, updated!.IdProofingStatus);
        Assert.Equal("new-session-456", updated.IdProofingSessionId);
        Assert.NotNull(updated.IdProofingCompletedAt);
        Assert.NotNull(updated.IdProofingExpiresAt);
    }

    [Fact]
    public async Task UpdateUserAsync_ShouldUpdateUpdatedAtTimestamp()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseUserRepository(context);

        var uniqueEmail = $"timestamp-{Guid.NewGuid()}@example.com";
        var originalTime = DateTime.UtcNow.AddMinutes(-5);
        var entity = UserEntityFactory.CreateUserEntity(e =>
        {
            e.Email = uniqueEmail;
            e.IdProofingStatus = (int)IdProofingStatus.NotStarted;
            e.CreatedAt = originalTime;
            e.UpdatedAt = originalTime;
        });
        context.Users.Add(entity);
        await context.SaveChangesAsync();

        await Task.Delay(10); // Small delay to ensure timestamp difference

        var user = UserFactory.CreateUserWithEmail(uniqueEmail, u =>
        {
            u.IdProofingStatus = IdProofingStatus.InProgress;
        });
        // Set init-only properties using reflection
        var idProperty = typeof(User).GetProperty(nameof(User.Id));
        var createdAtProperty = typeof(User).GetProperty(nameof(User.CreatedAt));
        idProperty?.SetValue(user, entity.Id);
        createdAtProperty?.SetValue(user, originalTime);

        // Act
        await repository.UpdateUserAsync(user, CancellationToken.None);

        // Assert
        var updated = await context.Users.FirstOrDefaultAsync(u => u.Email == uniqueEmail);
        Assert.NotNull(updated);
        Assert.True(updated!.UpdatedAt > originalTime);
    }

    [Fact]
    public async Task UpdateUserAsync_WhenUserDoesNotExist_ShouldThrowInvalidOperationException()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseUserRepository(context);

        var user = UserFactory.CreateUserWithEmail("nonexistent@example.com", u =>
        {
            u.IdProofingStatus = IdProofingStatus.Completed;
        });
        // Set Id to a non-existent value
        var idProperty = typeof(User).GetProperty(nameof(User.Id));
        idProperty?.SetValue(user, 99999);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.UpdateUserAsync(user, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateUserAsync_WhenUserIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseUserRepository(context);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.UpdateUserAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateUserAsync_ShouldBeCaseInsensitive()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseUserRepository(context);

        var uniqueId = Guid.NewGuid();
        var baseEmail = $"case-{uniqueId}@example.com";
        var entity = UserEntityFactory.CreateUserEntity(e =>
        {
            e.Email = baseEmail;
            e.IdProofingStatus = (int)IdProofingStatus.NotStarted;
        });
        context.Users.Add(entity);
        await context.SaveChangesAsync();

        var user = UserFactory.CreateUserWithEmail(baseEmail.ToUpperInvariant(), u =>
        {
            u.IdProofingStatus = IdProofingStatus.Completed;
        });
        // Set init-only properties using reflection
        var idProperty = typeof(User).GetProperty(nameof(User.Id));
        var createdAtProperty = typeof(User).GetProperty(nameof(User.CreatedAt));
        idProperty?.SetValue(user, entity.Id);
        createdAtProperty?.SetValue(user, entity.CreatedAt);

        // Act
        await repository.UpdateUserAsync(user, CancellationToken.None);

        // Assert
        var updated = await context.Users.FirstOrDefaultAsync(u => u.Email == baseEmail);
        Assert.NotNull(updated);
        Assert.Equal((int)IdProofingStatus.Completed, updated!.IdProofingStatus);
    }

    [Fact]
    public async Task UpdateUserAsync_ShouldUpdateEmail()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseUserRepository(context);

        var originalEmail = $"original-{Guid.NewGuid()}@example.com";
        var newEmail = $"new-{Guid.NewGuid()}@example.com";
        var entity = UserEntityFactory.CreateUserEntity(e =>
        {
            e.Email = originalEmail;
            e.IdProofingStatus = (int)IdProofingStatus.NotStarted;
        });
        context.Users.Add(entity);
        await context.SaveChangesAsync();

        var user = UserFactory.CreateUserWithEmail(newEmail, u =>
        {
            u.IdProofingStatus = IdProofingStatus.Completed;
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
        Assert.Equal(newEmail.ToLowerInvariant(), updated!.Email);
        Assert.Equal((int)IdProofingStatus.Completed, updated.IdProofingStatus);
    }

    [Fact]
    public async Task UpdateUserAsync_WhenEmailAlreadyExists_ShouldThrowInvalidOperationException()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseUserRepository(context);

        var existingEmail = $"existing-{Guid.NewGuid()}@example.com";
        var entity1 = UserEntityFactory.CreateUserEntity(e =>
        {
            e.Email = existingEmail;
            e.IdProofingStatus = (int)IdProofingStatus.NotStarted;
        });
        context.Users.Add(entity1);

        var originalEmail = $"original-{Guid.NewGuid()}@example.com";
        var entity2 = UserEntityFactory.CreateUserEntity(e =>
        {
            e.Email = originalEmail;
            e.IdProofingStatus = (int)IdProofingStatus.NotStarted;
        });
        context.Users.Add(entity2);
        await context.SaveChangesAsync();

        var user = UserFactory.CreateUserWithEmail(existingEmail, u =>
        {
            u.IdProofingStatus = IdProofingStatus.Completed;
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
    public async Task GetOrCreateUserAsync_WhenUserExists_ShouldReturnExistingUser()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseUserRepository(context);

        var uniqueEmail = $"existing-{Guid.NewGuid()}@example.com";
        var entity = UserEntityFactory.CreateUserEntity(e =>
        {
            e.Email = uniqueEmail;
            e.IdProofingStatus = (int)IdProofingStatus.Completed;
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
        Assert.Equal(IdProofingStatus.Completed, result.IdProofingStatus);
        Assert.Equal(entity.CreatedAt, result.CreatedAt);

        // Verify only one user exists with this email
        var count = await context.Users.CountAsync(u => u.Email == uniqueEmail);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task GetOrCreateUserAsync_WhenUserDoesNotExist_ShouldCreateNewUser()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseUserRepository(context);

        var uniqueEmail = $"newuser-{Guid.NewGuid()}@example.com";

        // Act
        var (result, isNewUser) = await repository.GetOrCreateUserAsync(uniqueEmail, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(isNewUser);
        Assert.Equal(uniqueEmail, result.Email);
        Assert.Equal(IdProofingStatus.NotStarted, result.IdProofingStatus);
        Assert.NotEqual(default(DateTime), result.CreatedAt);
        Assert.NotEqual(default(DateTime), result.UpdatedAt);

        // Verify user was saved
        var stored = await context.Users.FirstOrDefaultAsync(u => u.Email == uniqueEmail);
        Assert.NotNull(stored);
    }

    [Fact]
    public async Task GetOrCreateUserAsync_ShouldNormalizeEmailToLowercase()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseUserRepository(context);

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
        var stored = await context.Users.FirstOrDefaultAsync(u => u.Email == expectedEmail);
        Assert.NotNull(stored);
    }

    [Fact]
    public async Task GetOrCreateUserAsync_WhenEmailIsNull_ShouldThrowArgumentException()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseUserRepository(context);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetOrCreateUserAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task GetOrCreateUserAsync_WhenEmailIsWhitespace_ShouldThrowArgumentException()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseUserRepository(context);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetOrCreateUserAsync("   ", CancellationToken.None));
    }

    [Fact]
    public async Task GetUserBySessionIdAsync_WhenSessionExists_ShouldReturnUser()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseUserRepository(context);

        var uniqueEmail = $"session-{Guid.NewGuid()}@example.com";
        var sessionId = $"session-{Guid.NewGuid()}";
        var entity = UserEntityFactory.CreateUserEntity(e =>
        {
            e.Email = uniqueEmail;
            e.IdProofingStatus = (int)IdProofingStatus.InProgress;
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
        Assert.Equal(IdProofingStatus.InProgress, result.IdProofingStatus);
    }

    [Fact]
    public async Task GetUserBySessionIdAsync_WhenSessionDoesNotExist_ShouldReturnNull()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseUserRepository(context);

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
        var repository = new DatabaseUserRepository(context);

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
        var repository = new DatabaseUserRepository(context);

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
        var repository = new DatabaseUserRepository(context);

        var uniqueId1 = Guid.NewGuid();
        var uniqueId2 = Guid.NewGuid();
        var email1 = $"user1-{uniqueId1}@example.com";
        var email2 = $"user2-{uniqueId2}@example.com";
        var session1 = $"session-{uniqueId1}";
        var session2 = $"session-{uniqueId2}";

        var entity1 = UserEntityFactory.CreateUserEntity(e =>
        {
            e.Email = email1;
            e.IdProofingStatus = (int)IdProofingStatus.InProgress;
            e.IdProofingSessionId = session1;
        });
        var entity2 = UserEntityFactory.CreateUserEntity(e =>
        {
            e.Email = email2;
            e.IdProofingStatus = (int)IdProofingStatus.InProgress;
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
        var repository = new DatabaseUserRepository(context);

        var uniqueEmail = $"nosession-{Guid.NewGuid()}@example.com";
        var entity = UserEntityFactory.CreateUserEntity(e =>
        {
            e.Email = uniqueEmail;
            e.IdProofingStatus = (int)IdProofingStatus.NotStarted;
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
        var repository = new DatabaseUserRepository(context);

        var uniqueEmail = $"mapping-{Guid.NewGuid()}@example.com";
        var completedAt = DateTime.UtcNow.AddDays(-1);
        var expiresAt = DateTime.UtcNow.AddYears(1);
        var createdAt = DateTime.UtcNow.AddDays(-5);
        var updatedAt = DateTime.UtcNow.AddDays(-2);

        var coLoadedUpdated = DateTime.UtcNow.AddDays(-3);
        var entity = UserEntityFactory.CreateCoLoadedUserEntity(e =>
        {
            e.Email = uniqueEmail;
            e.IdProofingStatus = (int)IdProofingStatus.Completed;
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
        Assert.Equal(IdProofingStatus.Completed, result.IdProofingStatus);
        Assert.Equal("test-session", result.IdProofingSessionId);
        Assert.Equal(completedAt, result.IdProofingCompletedAt);
        Assert.Equal(expiresAt, result.IdProofingExpiresAt);
        Assert.True(result.IsCoLoaded);
        Assert.Equal(coLoadedUpdated, result.CoLoadedLastUpdated);
        Assert.Equal(createdAt, result.CreatedAt);
        Assert.Equal(updatedAt, result.UpdatedAt);
    }

    [Fact]
    public async Task CreateUserAsync_ShouldPreserveAllUserProperties()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseUserRepository(context);

        var uniqueEmail = $"fulluser-{Guid.NewGuid()}@example.com";
        var completedAt = DateTime.UtcNow.AddDays(-1);
        var expiresAt = DateTime.UtcNow.AddYears(1);
        var coLoadedUpdated = DateTime.UtcNow.AddDays(-2);

        var user = UserFactory.CreateUserWithEmail(uniqueEmail, u =>
        {
            u.IdProofingStatus = IdProofingStatus.Failed;
            u.IdProofingSessionId = "full-session";
            u.IdProofingCompletedAt = completedAt;
            u.IdProofingExpiresAt = expiresAt;
            u.IsCoLoaded = true;
            u.CoLoadedLastUpdated = coLoadedUpdated;
            u.UpdatedAt = DateTime.UtcNow.AddDays(-5);
        });
        // Set init-only CreatedAt using reflection
        var createdAtProperty = typeof(User).GetProperty(nameof(User.CreatedAt));
        createdAtProperty?.SetValue(user, DateTime.UtcNow.AddDays(-10));

        // Act
        await repository.CreateUserAsync(user, CancellationToken.None);

        // Assert
        var stored = await context.Users.FirstOrDefaultAsync(u => u.Email == uniqueEmail);
        Assert.NotNull(stored);
        Assert.Equal((int)IdProofingStatus.Failed, stored!.IdProofingStatus);
        Assert.Equal("full-session", stored.IdProofingSessionId);
        Assert.Equal(completedAt, stored.IdProofingCompletedAt);
        Assert.Equal(expiresAt, stored.IdProofingExpiresAt);
        Assert.True(stored.IsCoLoaded);
        Assert.Equal(coLoadedUpdated, stored.CoLoadedLastUpdated);
    }
}

