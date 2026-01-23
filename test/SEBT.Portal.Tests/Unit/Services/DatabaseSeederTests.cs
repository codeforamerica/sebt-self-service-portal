using Microsoft.EntityFrameworkCore;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Data.Entities;
using SEBT.Portal.Infrastructure.Repositories;
using SEBT.Portal.Infrastructure.Seeding.Helpers;
using SEBT.Portal.Infrastructure.Seeding.Services;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.Tests.Helpers;
using SEBT.Portal.Tests.Unit.Repositories;
using UserFactory = SEBT.Portal.Infrastructure.Seeding.Helpers.UserFactory;

namespace SEBT.Portal.Tests.Unit.Services;

[Collection("SqlServer")]
public class DatabaseSeederTests : IClassFixture<SqlServerTestFixture>
{
    private readonly SqlServerTestFixture _fixture;

    public DatabaseSeederTests(SqlServerTestFixture fixture)
    {
        _fixture = fixture;
    }

    private PortalDbContext CreateContext()
    {
        return _fixture.CreateContext();
    }

    private DatabaseSeeder CreateSeeder(PortalDbContext context)
    {
        var dataSeeder = new DataSeeder(context);
        return new DatabaseSeeder(dataSeeder);
    }

    /// <summary>
    /// Cleans up the database to ensure test isolation.
    /// </summary>
    private async Task CleanupDatabaseAsync(PortalDbContext context)
    {
        // Clear change tracker first
        context.ChangeTracker.Clear();

        // Remove all data
        var allUsers = await context.Users.ToListAsync();
        var allOptIns = await context.UserOptIns.ToListAsync();

        context.UserOptIns.RemoveRange(allOptIns);
        context.Users.RemoveRange(allUsers);
        await context.SaveChangesAsync();

        // Clear change tracker again after save
        context.ChangeTracker.Clear();
    }

    /// <summary>
    /// Cleans up the database synchronously to ensure test isolation.
    /// </summary>
    private void CleanupDatabase(PortalDbContext context)
    {
        // Clear change tracker first
        context.ChangeTracker.Clear();

        // Remove all data
        var allUsers = context.Users.ToList();
        var allOptIns = context.UserOptIns.ToList();

        context.UserOptIns.RemoveRange(allOptIns);
        context.Users.RemoveRange(allUsers);
        context.SaveChanges();

        // Clear change tracker again after save
        context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task SeedUsersAsync_WhenDatabaseIsEmpty_ShouldCreateUsers()
    {
        // Arrange
        using var context = CreateContext();
        await CleanupDatabaseAsync(context);
        var seeder = CreateSeeder(context);
        const int userCount = 5;

        // Act
        await seeder.SeedUsersAsync(userCount);

        // Assert
        var users = await context.Users.ToListAsync();
        Assert.Equal(userCount, users.Count);
        Assert.All(users, user => Assert.NotNull(user.Email));
    }

    [Fact]
    public async Task SeedUsersAsync_WhenUsersAlreadyExist_ShouldNotCreateUsers()
    {
        // Arrange
        using var context = CreateContext();
        await CleanupDatabaseAsync(context);
        var seeder = CreateSeeder(context);

        // Create an existing user
        var existingUser = UserEntityFactory.CreateUserEntity(e =>
        {
            e.Email = $"existing-{Guid.NewGuid()}@example.com";
        });
        context.Users.Add(existingUser);
        await context.SaveChangesAsync();

        // Act
        await seeder.SeedUsersAsync(5);

        // Assert - Should still only have 1 user
        var users = await context.Users.ToListAsync();
        Assert.Single(users);
        Assert.Equal(existingUser.Email, users[0].Email);
    }

    [Fact]
    public async Task SeedUsersAsync_WithCustomUserCount_ShouldCreateCorrectNumberOfUsers()
    {
        // Arrange
        using var context = CreateContext();
        await CleanupDatabaseAsync(context);
        var seeder = CreateSeeder(context);
        const int userCount = 15;

        // Act
        await seeder.SeedUsersAsync(userCount);

        // Assert
        var users = await context.Users.ToListAsync();
        Assert.Equal(userCount, users.Count);
    }

    [Fact]
    public async Task SeedUsersAsync_ShouldNormalizeEmails()
    {
        // Arrange
        using var context = CreateContext();
        await CleanupDatabaseAsync(context);
        var seeder = CreateSeeder(context);

        // Act
        await seeder.SeedUsersAsync(3);

        // Assert
        var users = await context.Users.ToListAsync();
        Assert.All(users, user =>
            Assert.Equal(user.Email, user.Email.ToLowerInvariant()));
    }

    [Fact]
    public async Task SeedUsersAsync_WhenDuplicateKeyExceptionOccurs_ShouldHandleGracefully()
    {
        // Arrange
        using var context = CreateContext();
        await CleanupDatabaseAsync(context);
        var seeder = CreateSeeder(context);

        // Create a user that might conflict (though unlikely with random emails)
        // We'll test the exception handling path by seeding twice
        await seeder.SeedUsersAsync(3);

        // Manually add a user to simulate a race condition
        var conflictingUser = UserEntityFactory.CreateUserEntity(e =>
        {
            e.Email = $"conflict-{Guid.NewGuid()}@example.com";
        });
        context.Users.Add(conflictingUser);
        await context.SaveChangesAsync();

        // Clear the context and try to seed again - should skip due to existing users
        context.Dispose();
        using var newContext = CreateContext();
        var newSeeder = CreateSeeder(newContext);

        // Act - Should not throw even if there are existing users
        await newSeeder.SeedUsersAsync(3);

        // Assert - Should have skipped seeding
        var users = await newContext.Users.ToListAsync();
        Assert.True(users.Count >= 1); // At least the conflicting user
    }

    [Fact]
    public async Task SeedTestUsersAsync_WhenDatabaseIsEmpty_ShouldCreateAllTestUsers()
    {
        // Arrange
        using var context = CreateContext();
        await CleanupDatabaseAsync(context);
        var seeder = CreateSeeder(context);

        // Act
        await seeder.SeedTestUsersAsync();

        // Assert
        var users = await context.Users.ToListAsync();
        Assert.Equal(3, users.Count);

        var emails = users.Select(u => u.Email).ToHashSet();
        Assert.Contains("co-loaded@example.com", emails);
        Assert.Contains("non-co-loaded@example.com", emails);
        Assert.Contains("not-started@example.com", emails);
    }

    [Fact]
    public async Task SeedTestUsersAsync_ShouldCreateUsersWithCorrectProperties()
    {
        // Arrange
        using var context = CreateContext();
        await CleanupDatabaseAsync(context);
        var seeder = CreateSeeder(context);

        // Act
        await seeder.SeedTestUsersAsync();

        // Assert - Check co-loaded user
        var coLoadedUser = await context.Users
            .FirstOrDefaultAsync(u => u.Email == "co-loaded@example.com");
        Assert.NotNull(coLoadedUser);
        Assert.True(coLoadedUser!.IsCoLoaded);
        Assert.Equal((int)IdProofingStatus.Completed, coLoadedUser.IdProofingStatus);
        Assert.NotNull(coLoadedUser.CoLoadedLastUpdated);
        Assert.NotNull(coLoadedUser.IdProofingCompletedAt);
        Assert.NotNull(coLoadedUser.IdProofingExpiresAt);

        // Check non-co-loaded user
        var nonCoLoadedUser = await context.Users
            .FirstOrDefaultAsync(u => u.Email == "non-co-loaded@example.com");
        Assert.NotNull(nonCoLoadedUser);
        Assert.False(nonCoLoadedUser!.IsCoLoaded);
        Assert.Equal((int)IdProofingStatus.InProgress, nonCoLoadedUser.IdProofingStatus);

        // Check not-started user
        var notStartedUser = await context.Users
            .FirstOrDefaultAsync(u => u.Email == "not-started@example.com");
        Assert.NotNull(notStartedUser);
        Assert.False(notStartedUser!.IsCoLoaded);
        Assert.Equal((int)IdProofingStatus.NotStarted, notStartedUser.IdProofingStatus);
    }

    [Fact]
    public async Task SeedTestUsersAsync_WhenUsersAlreadyExist_ShouldSkipExistingUsers()
    {
        // Arrange
        using var context = CreateContext();
        await CleanupDatabaseAsync(context);
        var seeder = CreateSeeder(context);

        // Create one of the test users manually
        var existingUser = UserEntityFactory.CreateUserEntity(e =>
        {
            e.Email = "co-loaded@example.com";
            e.IdProofingStatus = (int)IdProofingStatus.NotStarted;
            e.IsCoLoaded = false;
        });
        context.Users.Add(existingUser);
        await context.SaveChangesAsync();

        // Act
        await seeder.SeedTestUsersAsync();

        // Assert - Should have 3 users total (1 existing + 2 new)
        var users = await context.Users.ToListAsync();
        Assert.Equal(3, users.Count);

        // Verify the existing user wasn't modified
        var coLoadedUser = await context.Users
            .FirstOrDefaultAsync(u => u.Email == "co-loaded@example.com");
        Assert.NotNull(coLoadedUser);
        Assert.False(coLoadedUser!.IsCoLoaded); // Should remain as originally set
    }

    [Fact]
    public async Task SeedTestUsersAsync_WhenAllUsersExist_ShouldNotAddAnyUsers()
    {
        // Arrange
        using var context = CreateContext();
        await CleanupDatabaseAsync(context);
        var seeder = CreateSeeder(context);

        // Create all test users manually
        var testEmails = new[] { "co-loaded@example.com", "non-co-loaded@example.com", "not-started@example.com" };
        foreach (var email in testEmails)
        {
            var user = UserEntityFactory.CreateUserEntity(e =>
            {
                e.Email = email;
            });
            context.Users.Add(user);
        }
        await context.SaveChangesAsync();

        // Act
        await seeder.SeedTestUsersAsync();

        // Assert - Should still only have 3 users
        var users = await context.Users.ToListAsync();
        Assert.Equal(3, users.Count);
    }

    [Fact]
    public async Task SeedTestUsersAsync_WhenDuplicateKeyExceptionOccurs_ShouldHandleGracefully()
    {
        // Arrange
        using var context = CreateContext();
        await CleanupDatabaseAsync(context);
        var seeder = CreateSeeder(context);

        // Seed once
        await seeder.SeedTestUsersAsync();

        // Clear context and seed again - should handle gracefully
        context.Dispose();
        using var newContext = CreateContext();
        var newSeeder = CreateSeeder(newContext);

        // Act - Should not throw
        await newSeeder.SeedTestUsersAsync();

        // Assert - Should still have 3 users (no duplicates)
        var users = await newContext.Users.ToListAsync();
        Assert.Equal(3, users.Count);
    }

    [Fact]
    public void SeedTestUsers_WhenDatabaseIsEmpty_ShouldCreateAllTestUsers()
    {
        // Arrange
        using var context = CreateContext();
        CleanupDatabase(context);
        var seeder = CreateSeeder(context);

        // Act
        seeder.SeedTestUsers();

        // Assert
        var users = context.Users.ToList();
        Assert.Equal(3, users.Count);

        var emails = users.Select(u => u.Email).ToHashSet();
        Assert.Contains("co-loaded@example.com", emails);
        Assert.Contains("non-co-loaded@example.com", emails);
        Assert.Contains("not-started@example.com", emails);
    }

    [Fact]
    public void SeedTestUsers_WhenUsersAlreadyExist_ShouldSkipExistingUsers()
    {
        // Arrange
        using var context = CreateContext();
        CleanupDatabase(context);
        var seeder = CreateSeeder(context);

        // Create one of the test users manually
        var existingUser = UserEntityFactory.CreateUserEntity(e =>
        {
            e.Email = "co-loaded@example.com";
        });
        context.Users.Add(existingUser);
        context.SaveChanges();

        // Act
        seeder.SeedTestUsers();

        // Assert - Should have 3 users total
        var users = context.Users.ToList();
        Assert.Equal(3, users.Count);
    }

    [Fact]
    public void SeedTestUsers_WhenDuplicateKeyExceptionOccurs_ShouldHandleGracefully()
    {
        // Arrange
        using var context = CreateContext();
        CleanupDatabase(context);
        var seeder = CreateSeeder(context);

        // Seed once
        seeder.SeedTestUsers();

        // Clear context and seed again
        context.Dispose();
        using var newContext = CreateContext();
        var newSeeder = CreateSeeder(newContext);

        // Act - Should not throw
        newSeeder.SeedTestUsers();

        // Assert - Should still have 3 users
        var users = newContext.Users.ToList();
        Assert.Equal(3, users.Count);
    }

    [Fact]
    public async Task ClearSeededDataAsync_WhenSeededUsersExist_ShouldDeleteOnlySeededUsers()
    {
        // Arrange
        using var context = CreateContext();
        await CleanupDatabaseAsync(context);
        var seeder = CreateSeeder(context);

        // Seed test users
        await seeder.SeedTestUsersAsync();

        // Add a production user (not @example.com)
        var productionUser = UserEntityFactory.CreateUserEntity(e =>
        {
            e.Email = "production@real-domain.com";
        });
        context.Users.Add(productionUser);
        await context.SaveChangesAsync();

        // Act
        await seeder.ClearSeededDataAsync();

        // Assert - Only production user should remain
        var users = await context.Users.ToListAsync();
        Assert.Single(users);
        Assert.Equal("production@real-domain.com", users[0].Email);
    }

    [Fact]
    public async Task ClearSeededDataAsync_WhenNoSeededUsersExist_ShouldNotThrow()
    {
        // Arrange
        using var context = CreateContext();
        await CleanupDatabaseAsync(context);
        var seeder = CreateSeeder(context);

        // Add a production user
        var productionUser = UserEntityFactory.CreateUserEntity(e =>
        {
            e.Email = "production@real-domain.com";
        });
        context.Users.Add(productionUser);
        await context.SaveChangesAsync();

        // Act
        await seeder.ClearSeededDataAsync();

        // Assert - Production user should still exist
        var users = await context.Users.ToListAsync();
        Assert.Single(users);
        Assert.Equal("production@real-domain.com", users[0].Email);
    }

    [Fact]
    public async Task ClearSeededDataAsync_ShouldDeleteRelatedOptIns()
    {
        // Arrange
        using var context = CreateContext();
        await CleanupDatabaseAsync(context);
        var seeder = CreateSeeder(context);

        // Seed test users
        await seeder.SeedTestUsersAsync();

        // Add opt-ins for seeded users
        var optIn1 = new UserOptInEntity
        {
            Email = "co-loaded@example.com",
            EmailOptIn = true,
            DobOptIn = false
        };
        var optIn2 = new UserOptInEntity
        {
            Email = "non-co-loaded@example.com",
            EmailOptIn = false,
            DobOptIn = true
        };
        context.UserOptIns.AddRange(optIn1, optIn2);

        // Add opt-in for production user (should not be deleted)
        var productionOptIn = new UserOptInEntity
        {
            Email = "production@real-domain.com",
            EmailOptIn = true,
            DobOptIn = true
        };
        context.UserOptIns.Add(productionOptIn);
        await context.SaveChangesAsync();

        // Act
        await seeder.ClearSeededDataAsync();

        // Assert - Only production opt-in should remain
        var optIns = await context.UserOptIns.ToListAsync();
        Assert.Single(optIns);
        Assert.Equal("production@real-domain.com", optIns[0].Email);

        // Verify users were also deleted
        var users = await context.Users.ToListAsync();
        Assert.Empty(users);
    }

    [Fact]
    public async Task ClearSeededDataAsync_WhenEmptyDatabase_ShouldNotThrow()
    {
        // Arrange
        using var context = CreateContext();
        await CleanupDatabaseAsync(context);
        var seeder = CreateSeeder(context);

        // Act & Assert - Should not throw
        await seeder.ClearSeededDataAsync();

        var users = await context.Users.ToListAsync();
        Assert.Empty(users);
    }

    [Fact]
    public async Task ClearSeededDataAsync_ShouldOnlyDeleteUsersWithExampleComDomain()
    {
        // Arrange
        using var context = CreateContext();
        await CleanupDatabaseAsync(context);
        var seeder = CreateSeeder(context);

        // Create various users
        var seededUser1 = UserEntityFactory.CreateUserEntity(e =>
        {
            e.Email = "test1@example.com";
        });
        var seededUser2 = UserEntityFactory.CreateUserEntity(e =>
        {
            e.Email = "test2@example.com";
        });
        var productionUser1 = UserEntityFactory.CreateUserEntity(e =>
        {
            e.Email = "user1@production.com";
        });
        var productionUser2 = UserEntityFactory.CreateUserEntity(e =>
        {
            e.Email = "user2@another-domain.org";
        });
        context.Users.AddRange(seededUser1, seededUser2, productionUser1, productionUser2);
        await context.SaveChangesAsync();

        // Act
        await seeder.ClearSeededDataAsync();

        // Assert - Only production users should remain
        var users = await context.Users.ToListAsync();
        Assert.Equal(2, users.Count);
        var emails = users.Select(u => u.Email).ToHashSet();
        Assert.Contains("user1@production.com", emails);
        Assert.Contains("user2@another-domain.org", emails);
        Assert.DoesNotContain("test1@example.com", emails);
        Assert.DoesNotContain("test2@example.com", emails);
    }

    [Fact]
    public async Task SeedUsersAsync_ShouldCreateUsersWithValidData()
    {
        // Arrange
        using var context = CreateContext();
        await CleanupDatabaseAsync(context);
        var seeder = CreateSeeder(context);

        // Act
        await seeder.SeedUsersAsync(3);

        // Assert - Verify users have valid data
        var users = await context.Users.ToListAsync();
        Assert.Equal(3, users.Count);

        foreach (var user in users)
        {
            Assert.NotEmpty(user.Email);
            Assert.Contains("@", user.Email);
            Assert.InRange(user.IdProofingStatus, 0, 4); // Valid enum range
            Assert.NotEqual(default(DateTime), user.CreatedAt);
            Assert.NotEqual(default(DateTime), user.UpdatedAt);
        }
    }

    [Fact]
    public async Task SeedTestUsersAsync_ShouldNormalizeEmailsToLowercase()
    {
        // Arrange
        using var context = CreateContext();
        await CleanupDatabaseAsync(context);
        var seeder = CreateSeeder(context);

        // Act
        await seeder.SeedTestUsersAsync();

        // Assert - All emails should be lowercase
        var users = await context.Users.ToListAsync();
        Assert.All(users, user =>
            Assert.Equal(user.Email, user.Email.ToLowerInvariant()));
    }

    [Fact]
    public void SeedTestUsers_ShouldNormalizeEmailsToLowercase()
    {
        // Arrange
        using var context = CreateContext();
        CleanupDatabase(context);
        var seeder = CreateSeeder(context);

        // Act
        seeder.SeedTestUsers();

        // Assert - All emails should be lowercase
        var users = context.Users.ToList();
        Assert.All(users, user =>
            Assert.Equal(user.Email, user.Email.ToLowerInvariant()));
    }
}
