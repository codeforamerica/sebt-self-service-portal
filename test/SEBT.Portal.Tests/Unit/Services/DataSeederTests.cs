using Microsoft.EntityFrameworkCore;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Data.Entities;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.TestUtilities.Helpers;
using SEBT.Portal.Tests.Unit.Repositories;
using UserEntityFactory = SEBT.Portal.Infrastructure.Helpers.UserFactory;

namespace SEBT.Portal.Tests.Unit.Services;

[Collection("SqlServer")]
public class DataSeederTests : IClassFixture<SqlServerTestFixture>
{
    private readonly SqlServerTestFixture _fixture;

    public DataSeederTests(SqlServerTestFixture fixture)
    {
        _fixture = fixture;
    }

    private PortalDbContext CreateContext() => _fixture.CreateContext();

    private static DataSeeder CreateDataSeeder(PortalDbContext context) =>
        new(context);

    private async Task CleanupDatabaseAsync(PortalDbContext context)
    {
        context.ChangeTracker.Clear();
        var allUsers = await context.Users.ToListAsync();
        var allOptIns = await context.UserOptIns.ToListAsync();
        context.UserOptIns.RemoveRange(allOptIns);
        context.Users.RemoveRange(allUsers);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task AnyUsersExistAsync_WhenDatabaseEmpty_ReturnsFalse()
    {
        using var context = CreateContext();
        await CleanupDatabaseAsync(context);
        var seeder = CreateDataSeeder(context);

        var result = await seeder.AnyUsersExistAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task AnyUsersExistAsync_WhenUsersExist_ReturnsTrue()
    {
        using var context = CreateContext();
        await CleanupDatabaseAsync(context);
        var entity = UserEntityFactory.CreateUserEntity(e => e.Email = $"any-{Guid.NewGuid()}@example.com");
        context.Users.Add(entity);
        await context.SaveChangesAsync();

        var seeder = CreateDataSeeder(context);

        var result = await seeder.AnyUsersExistAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task GetExistingUserEmailsAsync_WhenSomeExist_ReturnsMatchingEmails()
    {
        using var context = CreateContext();
        await CleanupDatabaseAsync(context);
        var existing1 = $"existing1-{Guid.NewGuid()}@example.com";
        var existing2 = $"existing2-{Guid.NewGuid()}@example.com";
        var notExisting = $"notexisting-{Guid.NewGuid()}@example.com";
        context.Users.AddRange(
            UserEntityFactory.CreateUserEntity(e => e.Email = existing1),
            UserEntityFactory.CreateUserEntity(e => e.Email = existing2));
        await context.SaveChangesAsync();

        var seeder = CreateDataSeeder(context);
        var emailsToCheck = new[] { existing1, existing2, notExisting };

        var result = await seeder.GetExistingUserEmailsAsync(emailsToCheck);

        Assert.Equal(2, result.Count);
        Assert.Contains(existing1, result);
        Assert.Contains(existing2, result);
        Assert.DoesNotContain(notExisting, result);
    }

    [Fact]
    public async Task GetExistingUserEmailsAsync_WhenNoneExist_ReturnsEmptySet()
    {
        using var context = CreateContext();
        await CleanupDatabaseAsync(context);
        var seeder = CreateDataSeeder(context);

        var result = await seeder.GetExistingUserEmailsAsync(new[] { "nonexistent@example.com" });

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetExistingUserEmailsAsync_WhenNullEmails_ThrowsArgumentNullException()
    {
        using var context = CreateContext();
        var seeder = CreateDataSeeder(context);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            seeder.GetExistingUserEmailsAsync(null!));
    }

    [Fact]
    public async Task GetExistingUserEmailsAsync_NormalizesEmailsForLookup()
    {
        using var context = CreateContext();
        await CleanupDatabaseAsync(context);
        var email = $"normalize-{Guid.NewGuid()}@example.com";
        context.Users.Add(UserEntityFactory.CreateUserEntity(e => e.Email = email.ToLowerInvariant()));
        await context.SaveChangesAsync();

        var seeder = CreateDataSeeder(context);

        var result = await seeder.GetExistingUserEmailsAsync(new[] { email.ToUpperInvariant() });

        Assert.Single(result);
        Assert.Equal(email.ToLowerInvariant(), result.First());
    }

    [Fact]
    public async Task AddUsersAsync_AddsUsersToDatabase()
    {
        using var context = CreateContext();
        await CleanupDatabaseAsync(context);
        var email = $"add-{Guid.NewGuid()}@example.com";
        var user = UserFactory.CreateUserWithEmail(email);
        var seeder = CreateDataSeeder(context);

        await seeder.AddUsersAsync(new[] { user });

        var stored = await context.Users.FirstOrDefaultAsync(u => u.Email == email);
        Assert.NotNull(stored);
        Assert.Equal(email, stored!.Email);
    }

    [Fact]
    public async Task AddUsersAsync_WhenEmptyList_DoesNothing()
    {
        using var context = CreateContext();
        await CleanupDatabaseAsync(context);
        var seeder = CreateDataSeeder(context);

        await seeder.AddUsersAsync(Array.Empty<User>());

        var count = await context.Users.CountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task AddUsersAsync_WhenNullUsers_ThrowsArgumentNullException()
    {
        using var context = CreateContext();
        var seeder = CreateDataSeeder(context);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            seeder.AddUsersAsync(null!));
    }

    [Fact]
    public async Task AddUsersAsync_WhenDuplicateKey_HandlesGracefully()
    {
        using var context = CreateContext();
        await CleanupDatabaseAsync(context);
        var email = $"duplicate-{Guid.NewGuid()}@example.com";
        context.Users.Add(UserEntityFactory.CreateUserEntity(e => e.Email = email));
        await context.SaveChangesAsync();

        context.Dispose();
        using var newContext = CreateContext();
        var seeder = CreateDataSeeder(newContext);
        var user = UserFactory.CreateUserWithEmail(email);

        await seeder.AddUsersAsync(new[] { user });

        var users = await newContext.Users.Where(u => u.Email == email).ToListAsync();
        Assert.Single(users);
    }

    [Fact]
    public async Task GetUserEmailsByDomainAsync_ReturnsEmailsMatchingDomain()
    {
        using var context = CreateContext();
        await CleanupDatabaseAsync(context);
        var guid = Guid.NewGuid();
        var match1 = $"user1-{guid}@example.com";
        var match2 = $"user2-{guid}@example.com";
        var noMatch = $"user-{guid}@other.com";
        context.Users.AddRange(
            UserEntityFactory.CreateUserEntity(e => e.Email = match1),
            UserEntityFactory.CreateUserEntity(e => e.Email = match2),
            UserEntityFactory.CreateUserEntity(e => e.Email = noMatch));
        await context.SaveChangesAsync();

        var seeder = CreateDataSeeder(context);

        var result = await seeder.GetUserEmailsByDomainAsync("@example.com");

        Assert.Equal(2, result.Count);
        Assert.Contains(match1, result);
        Assert.Contains(match2, result);
        Assert.DoesNotContain(noMatch, result);
    }

    [Fact]
    public async Task GetUserEmailsByDomainAsync_WhenNullDomain_ThrowsArgumentNullException()
    {
        using var context = CreateContext();
        var seeder = CreateDataSeeder(context);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            seeder.GetUserEmailsByDomainAsync(null!));
    }

    [Fact]
    public async Task RemoveUsersByEmailAsync_RemovesSpecifiedUsers()
    {
        using var context = CreateContext();
        await CleanupDatabaseAsync(context);
        var toRemove = $"remove-{Guid.NewGuid()}@example.com";
        var toKeep = $"keep-{Guid.NewGuid()}@example.com";
        context.Users.AddRange(
            UserEntityFactory.CreateUserEntity(e => e.Email = toRemove),
            UserEntityFactory.CreateUserEntity(e => e.Email = toKeep));
        await context.SaveChangesAsync();

        var seeder = CreateDataSeeder(context);

        await seeder.RemoveUsersByEmailAsync(new[] { toRemove });
        await seeder.SaveChangesAsync();

        var remaining = await context.Users.Select(u => u.Email).ToListAsync();
        Assert.DoesNotContain(toRemove, remaining);
        Assert.Contains(toKeep, remaining);
    }

    [Fact]
    public async Task RemoveUsersByEmailAsync_WhenNullEmails_ThrowsArgumentNullException()
    {
        using var context = CreateContext();
        var seeder = CreateDataSeeder(context);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            seeder.RemoveUsersByEmailAsync(null!));
    }

    [Fact]
    public async Task RemoveUsersByEmailAsync_NormalizesEmails()
    {
        using var context = CreateContext();
        await CleanupDatabaseAsync(context);
        var email = $"normalize-remove-{Guid.NewGuid()}@example.com";
        context.Users.Add(UserEntityFactory.CreateUserEntity(e => e.Email = email.ToLowerInvariant()));
        await context.SaveChangesAsync();

        var seeder = CreateDataSeeder(context);

        await seeder.RemoveUsersByEmailAsync(new[] { email.ToUpperInvariant() });
        await seeder.SaveChangesAsync();

        var count = await context.Users.CountAsync(u => u.Email == email);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task RemoveUserOptInsByEmailAsync_RemovesSpecifiedOptIns()
    {
        using var context = CreateContext();
        await CleanupDatabaseAsync(context);
        var toRemove = $"optin-remove-{Guid.NewGuid()}@example.com";
        var toKeep = $"optin-keep-{Guid.NewGuid()}@example.com";
        context.UserOptIns.AddRange(
            new UserOptInEntity { Email = toRemove, EmailOptIn = true, DobOptIn = false },
            new UserOptInEntity { Email = toKeep, EmailOptIn = false, DobOptIn = true });
        await context.SaveChangesAsync();

        var seeder = CreateDataSeeder(context);

        await seeder.RemoveUserOptInsByEmailAsync(new[] { toRemove });
        await seeder.SaveChangesAsync();

        var remaining = await context.UserOptIns.Select(o => o.Email).ToListAsync();
        Assert.DoesNotContain(toRemove, remaining);
        Assert.Contains(toKeep, remaining);
    }

    [Fact]
    public async Task RemoveUserOptInsByEmailAsync_WhenNullEmails_ThrowsArgumentNullException()
    {
        using var context = CreateContext();
        var seeder = CreateDataSeeder(context);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            seeder.RemoveUserOptInsByEmailAsync(null!));
    }

    [Fact]
    public async Task SaveChangesAsync_PersistsPendingChanges()
    {
        using var context = CreateContext();
        await CleanupDatabaseAsync(context);
        var email = $"save-{Guid.NewGuid()}@example.com";
        context.Users.Add(UserEntityFactory.CreateUserEntity(e => e.Email = email));
        var seeder = CreateDataSeeder(context);

        await seeder.SaveChangesAsync();

        var stored = await context.Users.FirstOrDefaultAsync(u => u.Email == email);
        Assert.NotNull(stored);
    }

    [Fact]
    public async Task AnyUsersExist_WhenDatabaseEmpty_ReturnsFalse()
    {
        using var context = CreateContext();
        await CleanupDatabaseAsync(context);

        var seeder = CreateDataSeeder(context);

        var result = seeder.AnyUsersExist();

        Assert.False(result);
    }

    [Fact]
    public async Task AnyUsersExist_WhenUsersExist_ReturnsTrue()
    {
        using var context = CreateContext();
        await CleanupDatabaseAsync(context);
        var email = $"sync-any-{Guid.NewGuid()}@example.com";
        context.Users.Add(UserEntityFactory.CreateUserEntity(e => e.Email = email));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var seeder = CreateDataSeeder(context);

        var result = seeder.AnyUsersExist();

        Assert.True(result);
    }

    [Fact]
    public async Task GetExistingUserEmails_WhenSomeExist_ReturnsMatchingEmails()
    {
        using var context = CreateContext();
        await CleanupDatabaseAsync(context);
        var existing = $"sync-existing-{Guid.NewGuid()}@example.com";
        context.Users.Add(UserEntityFactory.CreateUserEntity(e => e.Email = existing));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var seeder = CreateDataSeeder(context);

        var result = seeder.GetExistingUserEmails(new[] { existing, "nonexistent@example.com" });

        Assert.Single(result);
        Assert.Contains(existing, result);
    }

    [Fact]
    public void GetExistingUserEmails_WhenNullEmails_ThrowsArgumentNullException()
    {
        using var context = CreateContext();
        var seeder = CreateDataSeeder(context);

        Assert.Throws<ArgumentNullException>(() =>
            seeder.GetExistingUserEmails(null!));
    }

    [Fact]
    public void AddUsers_AddsUsersToDatabase()
    {
        using var context = CreateContext();
        var email = $"sync-add-{Guid.NewGuid()}@example.com";
        var user = UserFactory.CreateUserWithEmail(email);
        var seeder = CreateDataSeeder(context);

        seeder.AddUsers(new[] { user });

        var stored = context.Users.FirstOrDefault(u => u.Email == email);
        Assert.NotNull(stored);
        Assert.Equal(email, stored!.Email);
    }

    [Fact]
    public async Task AddUsers_WhenEmptyList_DoesNothing()
    {
        using var context = CreateContext();
        await CleanupDatabaseAsync(context);
        var seeder = CreateDataSeeder(context);

        seeder.AddUsers(Array.Empty<User>());

        Assert.Equal(0, context.Users.Count());
    }

    [Fact]
    public void AddUsers_WhenNullUsers_ThrowsArgumentNullException()
    {
        using var context = CreateContext();
        var seeder = CreateDataSeeder(context);

        Assert.Throws<ArgumentNullException>(() =>
            seeder.AddUsers(null!));
    }

    [Fact]
    public void SaveChanges_PersistsPendingChanges()
    {
        using var context = CreateContext();
        var email = $"sync-save-{Guid.NewGuid()}@example.com";
        context.Users.Add(UserEntityFactory.CreateUserEntity(e => e.Email = email));
        var seeder = CreateDataSeeder(context);

        seeder.SaveChanges();

        var stored = context.Users.FirstOrDefault(u => u.Email == email);
        Assert.NotNull(stored);
    }

    [Fact]
    public void Constructor_WhenDbContextNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new DataSeeder(null!));
    }
}
