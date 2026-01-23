using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Seeding.Helpers;

namespace SEBT.Portal.Infrastructure.Seeding.Services;

/// <summary>
/// Service for seeding the database with initial or test data.
/// </summary>
public class DatabaseSeeder : IDatabaseSeeder
{
    private readonly IDataSeeder _dataSeeder;

    public DatabaseSeeder(IDataSeeder dataSeeder)
    {
        _dataSeeder = dataSeeder ?? throw new ArgumentNullException(nameof(dataSeeder));
    }

    /// <summary>
    /// Seeds the database with sample users for development/testing.
    /// </summary>
    /// <param name="userCount">Number of users to create (default: 10).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task SeedUsersAsync(int userCount = 10, CancellationToken cancellationToken = default)
    {
        // Check if users already exist
        if (await _dataSeeder.AnyUsersExistAsync(cancellationToken))
        {
            return; // Database already seeded
        }

        var users = new List<User>();
        for (int i = 0; i < userCount; i++)
        {
            users.Add(UserFactory.CreateUser());
        }

        await _dataSeeder.AddUsersAsync(users, cancellationToken);
    }

    /// <summary>
    /// Creates the standard set of test users for seeding.
    /// </summary>
    /// <returns>An array of User instances configured for testing.</returns>
    private static User[] CreateTestUsers()
    {
        return new[]
        {
            UserFactory.CreateCoLoadedUser(u =>
            {
                u.Email = "co-loaded@example.com";
                u.IdProofingStatus = IdProofingStatus.Completed;
                u.CoLoadedLastUpdated = DateTime.UtcNow.AddDays(-5);
                u.IdProofingCompletedAt = DateTime.UtcNow.AddDays(-10);
                u.IdProofingExpiresAt = DateTime.UtcNow.AddDays(355);
            }),
            UserFactory.CreateNonCoLoadedUser(u =>
            {
                u.Email = "non-co-loaded@example.com";
                u.IdProofingStatus = IdProofingStatus.InProgress;
            }),
            UserFactory.CreateNonCoLoadedUser(u =>
            {
                u.Email = "not-started@example.com";
                u.IdProofingStatus = IdProofingStatus.NotStarted;
            })
        };
    }

    /// <summary>
    /// Seeds the database with specific test users for development.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task SeedTestUsersAsync(CancellationToken cancellationToken = default)
    {
        var testUsers = CreateTestUsers();
        // Get raw emails - IDataSeeder will normalize them internally
        var userEmails = testUsers.Select(u => u.Email).ToList();
        var existingEmails = await _dataSeeder.GetExistingUserEmailsAsync(userEmails, cancellationToken);

        // Normalize for comparison since GetExistingUserEmailsAsync returns normalized emails
        var usersToAdd = testUsers
            .Where(user => !existingEmails.Contains(user.Email.Trim().ToLowerInvariant()))
            .ToList();

        if (usersToAdd.Count > 0)
        {
            await _dataSeeder.AddUsersAsync(usersToAdd, cancellationToken);
        }
    }

    /// <summary>
    /// Synchronous version of SeedTestUsersAsync for use in UseSeeding callback.
    /// </summary>
    public void SeedTestUsers()
    {
        var testUsers = CreateTestUsers();
        // Get raw emails - IDataSeeder will normalize them internally
        var userEmails = testUsers.Select(u => u.Email).ToList();
        var existingEmails = _dataSeeder.GetExistingUserEmails(userEmails);

        // Normalize for comparison since GetExistingUserEmails returns normalized emails
        var usersToAdd = testUsers
            .Where(user => !existingEmails.Contains(user.Email.Trim().ToLowerInvariant()))
            .ToList();

        if (usersToAdd.Count > 0)
        {
            _dataSeeder.AddUsers(usersToAdd);
            // AddUsers already saves internally, so we don't need to call SaveChanges
        }
    }

    /// <summary>
    /// Clears seeded test data from the database.
    /// Only deletes users with @example.com email addresses to avoid deleting production data.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task ClearSeededDataAsync(CancellationToken cancellationToken = default)
    {
        const string seededEmailDomain = "@example.com";

        var seededUserEmails = await _dataSeeder.GetUserEmailsByDomainAsync(seededEmailDomain, cancellationToken);

        if (seededUserEmails.Count > 0)
        {
            await _dataSeeder.RemoveUserOptInsByEmailAsync(seededUserEmails, cancellationToken);
            await _dataSeeder.RemoveUsersByEmailAsync(seededUserEmails, cancellationToken);
            await _dataSeeder.SaveChangesAsync(cancellationToken);
        }
    }
}
