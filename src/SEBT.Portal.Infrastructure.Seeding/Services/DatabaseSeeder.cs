using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;
using SEBT.Portal.TestUtilities.Helpers;

namespace SEBT.Portal.Infrastructure.Seeding.Services;

/// <summary>
/// Service for seeding the database with initial or test data.
/// </summary>
public class DatabaseSeeder : Core.Services.IDatabaseSeeder
{
    private readonly IDataSeeder _dataSeeder;
    private readonly ILogger<DatabaseSeeder>? _logger;
    private readonly TimeProvider _timeProvider;

    private const int DaysSinceIdProofingCompleted = -30;
    private const int DaysUntilIdProofingExpires = 335;
    private const int DaysSinceCoLoadedUpdate = -5;
    private const int DaysSinceBasicIdProofingCompleted = -10;
    private const int DaysUntilBasicIdProofingExpires = 355;

    public DatabaseSeeder(IDataSeeder dataSeeder, ILogger<DatabaseSeeder>? logger = null, TimeProvider? timeProvider = null)
    {
        _dataSeeder = dataSeeder ?? throw new ArgumentNullException(nameof(dataSeeder));
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
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
    /// <param name="now">The current time (from TimeProvider) for computing relative dates.</param>
    /// <returns>An array of User instances configured for testing.</returns>
    private static User[] CreateTestUsers(DateTime now)
    {
        return new[]
        {
            UserFactory.CreateCoLoadedUser(u =>
            {
                u.Email = "co-loaded@example.com";
                u.IdProofingStatus = IdProofingStatus.Completed;
                u.CoLoadedLastUpdated = now.AddDays(-5);
                u.IdProofingCompletedAt = now.AddDays(-10);
                u.IdProofingExpiresAt = now.AddDays(355);
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
    /// Gets the list of user seeding data based on household scenarios.
    /// Each entry maps a household email to the appropriate ID proofing status.
    /// This mapping is based on the household data seeded in MockHouseholdRepository.
    /// </summary>
    private static Dictionary<string, IdProofingStatus> GetHouseholdUserMappings()
    {
        return new Dictionary<string, IdProofingStatus>
        {
            // Users with ID verification completed (have addresses in household data)
            { "co-loaded@example.com", IdProofingStatus.Completed },
            { "verified@example.com", IdProofingStatus.Completed },
            { "singlechild@example.com", IdProofingStatus.Completed },
            { "largefamily@example.com", IdProofingStatus.Completed },
            { "expired@example.com", IdProofingStatus.Completed },

            // Users without ID verification (addresses not shown unless explicitly requested)
            { "pending@example.com", IdProofingStatus.NotStarted },
            { "minimal@example.com", IdProofingStatus.NotStarted },
            { "denied@example.com", IdProofingStatus.NotStarted },
            { "review@example.com", IdProofingStatus.InProgress },
            { "cancelled@example.com", IdProofingStatus.NotStarted },
            { "unknown@example.com", IdProofingStatus.NotStarted }
        };
    }

    /// <summary>
    /// Seeds the database with specific test users for development.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task SeedTestUsersAsync(CancellationToken cancellationToken = default)
    {
        await SeedTestUsersAsync(useMockHouseholdData: false, cancellationToken);
    }

    /// <summary>
    /// Seeds the database with specific test users for development.
    /// If useMockHouseholdData is true, seeds users that correspond to household mock data.
    /// </summary>
    /// <param name="useMockHouseholdData">Whether to seed users corresponding to household mock data.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task SeedTestUsersAsync(bool useMockHouseholdData, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var seededCount = 0;

        if (useMockHouseholdData)
        {
            var mappings = GetHouseholdUserMappings();

            foreach (var (email, idProofingStatus) in mappings)
            {
                var normalizedEmail = EmailNormalizer.Normalize(email ?? throw new ArgumentException("Email cannot be null", nameof(email)));

                var existingEmails = await _dataSeeder.GetExistingUserEmailsAsync(new[] { normalizedEmail }, cancellationToken);
                if (existingEmails.Contains(normalizedEmail))
                {
                    _logger?.LogDebug("User with email {Email} already exists, skipping", normalizedEmail);
                    continue;
                }

                try
                {
                    User user;
                    if (normalizedEmail == "co-loaded@example.com")
                    {
                        user = UserFactory.CreateCoLoadedUser(u =>
                        {
                            u.Email = normalizedEmail;
                            u.IdProofingStatus = idProofingStatus;
                            u.IdProofingCompletedAt = now.AddDays(DaysSinceIdProofingCompleted);
                            u.IdProofingExpiresAt = now.AddDays(DaysUntilIdProofingExpires);
                            u.CoLoadedLastUpdated = now.AddDays(DaysSinceCoLoadedUpdate);
                        });
                    }
                    else
                    {
                        user = UserFactory.CreateUserWithEmail(normalizedEmail, u =>
                        {
                            u.IdProofingStatus = idProofingStatus;
                            if (idProofingStatus == IdProofingStatus.Completed)
                            {
                                u.IdProofingCompletedAt = now.AddDays(DaysSinceIdProofingCompleted);
                                u.IdProofingExpiresAt = now.AddDays(DaysUntilIdProofingExpires);
                            }
                            u.IsCoLoaded = false;
                            u.CoLoadedLastUpdated = null;
                        });
                    }

                    await _dataSeeder.AddUsersAsync(new[] { user }, cancellationToken);
                    seededCount++;
                    _logger?.LogInformation("Successfully seeded user {Email} with ID proofing status {Status}", normalizedEmail, idProofingStatus);
                }
                catch (DbUpdateException ex) when (
                    ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true ||
                    ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true ||
                    ex.InnerException?.Message.Contains("IX_Users_Email", StringComparison.OrdinalIgnoreCase) == true)
                {
                    _logger?.LogWarning(ex, "User with email {Email} already exists (race condition), skipping", normalizedEmail);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Unexpected error seeding user {Email}", normalizedEmail);
                    throw;
                }
            }
        }
        else
        {
            var testUsers = CreateTestUsers(now);
            var userEmails = testUsers.Select(u => u.Email).ToList();
            var existingEmails = await _dataSeeder.GetExistingUserEmailsAsync(userEmails, cancellationToken);

            var usersToAdd = testUsers
                .Where(user => !existingEmails.Contains(EmailNormalizer.Normalize(user.Email)))
                .ToList();

            if (usersToAdd.Count > 0)
            {
                await _dataSeeder.AddUsersAsync(usersToAdd, cancellationToken);
                seededCount = usersToAdd.Count;
            }
        }

        if (seededCount > 0)
        {
            _logger?.LogInformation("Successfully seeded {Count} users", seededCount);
        }
        else
        {
            _logger?.LogInformation("All users already exist, no seeding needed");
        }
    }

    /// <summary>
    /// Synchronous version of SeedTestUsersAsync for use in UseSeeding callback.
    /// </summary>
    public void SeedTestUsers()
    {
        SeedTestUsers(useMockHouseholdData: false);
    }

    /// <summary>
    /// Synchronous version of SeedTestUsersAsync for use in UseSeeding callback.
    /// </summary>
    /// <param name="useMockHouseholdData">Whether to seed users corresponding to household mock data.</param>
    public void SeedTestUsers(bool useMockHouseholdData)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var seededCount = 0;

        if (useMockHouseholdData)
        {
            var mappings = GetHouseholdUserMappings();

            foreach (var (email, idProofingStatus) in mappings)
            {
                var normalizedEmail = EmailNormalizer.Normalize(email ?? throw new ArgumentException("Email cannot be null", nameof(email)));

                var existingEmails = _dataSeeder.GetExistingUserEmails(new[] { normalizedEmail });
                if (existingEmails.Contains(normalizedEmail))
                {
                    _logger?.LogDebug("User with email {Email} already exists, skipping", normalizedEmail);
                    continue;
                }

                try
                {
                    User user;
                    if (normalizedEmail == "co-loaded@example.com")
                    {
                        user = UserFactory.CreateCoLoadedUser(u =>
                        {
                            u.Email = normalizedEmail;
                            u.IdProofingStatus = idProofingStatus;
                            u.IdProofingCompletedAt = now.AddDays(DaysSinceIdProofingCompleted);
                            u.IdProofingExpiresAt = now.AddDays(DaysUntilIdProofingExpires);
                            u.CoLoadedLastUpdated = now.AddDays(DaysSinceCoLoadedUpdate);
                        });
                    }
                    else
                    {
                        user = UserFactory.CreateUserWithEmail(normalizedEmail, u =>
                        {
                            u.IdProofingStatus = idProofingStatus;
                            if (idProofingStatus == IdProofingStatus.Completed)
                            {
                                u.IdProofingCompletedAt = now.AddDays(DaysSinceIdProofingCompleted);
                                u.IdProofingExpiresAt = now.AddDays(DaysUntilIdProofingExpires);
                            }
                            u.IsCoLoaded = false;
                            u.CoLoadedLastUpdated = null;
                        });
                    }

                    _dataSeeder.AddUsers(new[] { user });
                    seededCount++;
                    _logger?.LogInformation("Successfully seeded user {Email} with ID proofing status {Status}", normalizedEmail, idProofingStatus);
                }
                catch (DbUpdateException ex) when (
                    ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true ||
                    ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true ||
                    ex.InnerException?.Message.Contains("IX_Users_Email", StringComparison.OrdinalIgnoreCase) == true)
                {
                    _logger?.LogWarning(ex, "User with email {Email} already exists (race condition), skipping", normalizedEmail);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Unexpected error seeding user {Email}", normalizedEmail);
                    throw;
                }
            }
        }
        else
        {
            var testUsers = CreateTestUsers(now);
            var userEmails = testUsers.Select(u => u.Email).ToList();
            var existingEmails = _dataSeeder.GetExistingUserEmails(userEmails);

            var usersToAdd = testUsers
                .Where(user => !existingEmails.Contains(EmailNormalizer.Normalize(user.Email)))
                .ToList();

            if (usersToAdd.Count > 0)
            {
                _dataSeeder.AddUsers(usersToAdd);
                seededCount = usersToAdd.Count;
            }
        }

        if (seededCount > 0)
        {
            _logger?.LogInformation("Successfully seeded {Count} users", seededCount);
        }
        else
        {
            _logger?.LogInformation("All users already exist, no seeding needed");
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
