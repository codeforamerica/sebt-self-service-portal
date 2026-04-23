using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Seeding;
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
    private readonly SeedingSettings _settings;
    private readonly ILogger<DatabaseSeeder>? _logger;
    private readonly TimeProvider _timeProvider;

    private const int DaysSinceIdProofingCompleted = -30;
    private const int DaysSinceCoLoadedUpdate = -5;
    private const int DaysSinceBasicIdProofingCompleted = -10;

    private bool IsDc => string.Equals(_settings.State, "dc", StringComparison.OrdinalIgnoreCase);

    public DatabaseSeeder(
        IDataSeeder dataSeeder,
        SeedingSettings? settings = null,
        ILogger<DatabaseSeeder>? logger = null,
        TimeProvider? timeProvider = null)
    {
        _dataSeeder = dataSeeder ?? throw new ArgumentNullException(nameof(dataSeeder));
        _settings = settings ?? new SeedingSettings();
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
    private User[] CreateTestUsers(DateTime now)
    {
        return new[]
        {
            UserFactory.CreateCoLoadedUser(u =>
            {
                u.Email = _settings.BuildEmail(SeedScenarios.CoLoaded.Name);
                u.IdProofingStatus = IdProofingStatus.Completed;
                u.IalLevel = UserIalLevel.IAL1plus;
                u.CoLoadedLastUpdated = now.AddDays(-5);
                u.IdProofingCompletedAt = now.AddDays(-10);
                u.Phone = "5551234567";
                u.SnapId = "SNAP-CO-001";
                u.TanfId = "TANF-CO-001";
                u.Ssn = "123456789";
            }),
            UserFactory.CreateNonCoLoadedUser(u =>
            {
                u.Email = _settings.BuildEmail(SeedScenarios.NonCoLoaded.Name);
                u.IdProofingStatus = IdProofingStatus.InProgress;
                u.IalLevel = UserIalLevel.None;
                u.Phone = "5555551234";
                u.SnapId = "SNAP-NCO-001";
            }),
            UserFactory.CreateNonCoLoadedUser(u =>
            {
                u.Email = _settings.BuildEmail(SeedScenarios.NotStarted.Name);
                u.IdProofingStatus = IdProofingStatus.NotStarted;
                u.IalLevel = UserIalLevel.None;
            })
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
            var coLoadedEmail = EmailNormalizer.Normalize(_settings.BuildEmail(SeedScenarios.CoLoaded.Name));
            var coLoadedPendingIdProofingEmail = EmailNormalizer.Normalize(
                _settings.BuildEmail(SeedScenarios.CoLoadedPendingIdProofing.Name));
            var verifiedEmail = EmailNormalizer.Normalize(_settings.BuildEmail(SeedScenarios.Verified.Name));

            foreach (var scenario in SeedScenarios.UserScenarios)
            {
                // Skip DC-only scenarios when not running as DC
                if (!IsDc && SeedScenarios.DcOnlyScenarios.Contains(scenario))
                {
                    continue;
                }

                var normalizedEmail = EmailNormalizer.Normalize(_settings.BuildEmail(scenario.Name));

                var existingEmails = await _dataSeeder.GetExistingUserEmailsAsync(new[] { normalizedEmail }, cancellationToken);
                if (existingEmails.Contains(normalizedEmail))
                {
                    _logger?.LogDebug("User with email {Email} already exists, skipping", normalizedEmail);
                    continue;
                }

                try
                {
                    User user;
                    if (normalizedEmail == coLoadedEmail)
                    {
                        user = UserFactory.CreateCoLoadedUser(u =>
                        {
                            u.Email = normalizedEmail;
                            u.IdProofingStatus = IdProofingStatus.Completed;
                            u.IalLevel = scenario.IalLevel;
                            u.IdProofingCompletedAt = now.AddDays(DaysSinceIdProofingCompleted);

                            u.CoLoadedLastUpdated = now.AddDays(DaysSinceCoLoadedUpdate);
                            u.Phone = "5551234567";
                            u.SnapId = "SNAP-CO-001";
                            u.TanfId = "TANF-CO-001";
                            u.Ssn = "123456789";
                        });
                    }
                    else if (normalizedEmail == coLoadedPendingIdProofingEmail)
                    {
                        user = UserFactory.CreateCoLoadedUser(u =>
                        {
                            u.Email = normalizedEmail;
                            u.IdProofingStatus = IdProofingStatus.NotStarted;
                            u.IalLevel = UserIalLevel.None;
                            u.IdProofingCompletedAt = null;
                            u.IdProofingExpiresAt = null;
                            u.CoLoadedLastUpdated = now.AddDays(DaysSinceCoLoadedUpdate);
                            u.Phone = "8185558438";
                            u.SnapId = "SNAP-CO-001";
                            u.TanfId = "TANF-CO-001";
                            u.Ssn = "123456789";
                        });
                    }
                    else if (normalizedEmail == verifiedEmail)
                    {
                        user = UserFactory.CreateUserWithEmail(normalizedEmail, u =>
                        {
                            u.IdProofingStatus = IdProofingStatus.Completed;
                            u.IalLevel = scenario.IalLevel;
                            u.IdProofingCompletedAt = now.AddDays(DaysSinceIdProofingCompleted);

                            u.IsCoLoaded = false;
                            u.CoLoadedLastUpdated = null;
                            u.Phone = "5559876543";
                            u.SnapId = "SNAP-VER-001";
                            u.TanfId = "TANF-VER-001";
                            u.Ssn = "123456789";
                        });
                    }
                    else
                    {
                        user = UserFactory.CreateUserWithEmail(normalizedEmail, u =>
                        {
                            u.IdProofingStatus = scenario.IalLevel is UserIalLevel.IAL1plus or UserIalLevel.IAL2
                                ? IdProofingStatus.Completed
                                : IdProofingStatus.NotStarted;
                            u.IalLevel = scenario.IalLevel;
                            if (scenario.IalLevel is UserIalLevel.IAL1plus or UserIalLevel.IAL2)
                            {
                                u.IdProofingCompletedAt = now.AddDays(DaysSinceIdProofingCompleted);
                            }
                            u.IsCoLoaded = false;
                            u.CoLoadedLastUpdated = null;
                        });
                    }

                    await _dataSeeder.AddUsersAsync(new[] { user }, cancellationToken);
                    seededCount++;
                    _logger?.LogInformation("Successfully seeded user {Email} with IAL level {IalLevel}", normalizedEmail, scenario.IalLevel);
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
            var userEmails = testUsers
                .Select(u => u.Email)
                .Where(e => e != null)
                .Cast<string>()
                .ToList();
            var existingEmails = await _dataSeeder.GetExistingUserEmailsAsync(userEmails, cancellationToken);

            var usersToAdd = testUsers
                .Where(user => user.Email != null
                    && !existingEmails.Contains(EmailNormalizer.Normalize(user.Email)))
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
            var coLoadedEmail = EmailNormalizer.Normalize(_settings.BuildEmail(SeedScenarios.CoLoaded.Name));
            var coLoadedPendingIdProofingEmail = EmailNormalizer.Normalize(
                _settings.BuildEmail(SeedScenarios.CoLoadedPendingIdProofing.Name));
            var verifiedEmail = EmailNormalizer.Normalize(_settings.BuildEmail(SeedScenarios.Verified.Name));
            foreach (var scenario in SeedScenarios.UserScenarios)
            {
                // Skip DC-only scenarios when not running as DC
                if (!IsDc && SeedScenarios.DcOnlyScenarios.Contains(scenario))
                {
                    continue;
                }

                var normalizedEmail = EmailNormalizer.Normalize(_settings.BuildEmail(scenario.Name));

                var existingEmails = _dataSeeder.GetExistingUserEmails(new[] { normalizedEmail });
                if (existingEmails.Contains(normalizedEmail))
                {
                    _logger?.LogDebug("User with email {Email} already exists, skipping", normalizedEmail);
                    continue;
                }

                try
                {
                    User user;
                    if (normalizedEmail == coLoadedEmail)
                    {
                        user = UserFactory.CreateCoLoadedUser(u =>
                        {
                            u.Email = normalizedEmail;
                            u.IdProofingStatus = IdProofingStatus.Completed;
                            u.IalLevel = scenario.IalLevel;
                            u.IdProofingCompletedAt = now.AddDays(DaysSinceIdProofingCompleted);

                            u.CoLoadedLastUpdated = now.AddDays(DaysSinceCoLoadedUpdate);
                            u.Phone = "5551234567";
                            u.SnapId = "SNAP-CO-001";
                            u.TanfId = "TANF-CO-001";
                            u.Ssn = "123456789";
                        });
                    }
                    else if (normalizedEmail == coLoadedPendingIdProofingEmail)
                    {
                        user = UserFactory.CreateCoLoadedUser(u =>
                        {
                            u.Email = normalizedEmail;
                            u.IdProofingStatus = IdProofingStatus.NotStarted;
                            u.IalLevel = UserIalLevel.None;
                            u.IdProofingCompletedAt = null;
                            u.IdProofingExpiresAt = null;
                            u.CoLoadedLastUpdated = now.AddDays(DaysSinceCoLoadedUpdate);
                            u.Phone = "8185558438";
                            u.SnapId = "SNAP-CO-001";
                            u.TanfId = "TANF-CO-001";
                            u.Ssn = "123456789";
                        });
                    }
                    else if (normalizedEmail == verifiedEmail)
                    {
                        user = UserFactory.CreateUserWithEmail(normalizedEmail, u =>
                        {
                            u.IdProofingStatus = IdProofingStatus.Completed;
                            u.IalLevel = scenario.IalLevel;
                            u.IdProofingCompletedAt = now.AddDays(DaysSinceIdProofingCompleted);

                            u.IsCoLoaded = false;
                            u.CoLoadedLastUpdated = null;
                            u.Phone = "5559876543";
                            u.SnapId = "SNAP-VER-001";
                            u.TanfId = "TANF-VER-001";
                            u.Ssn = "123456789";
                        });
                    }
                    else
                    {
                        user = UserFactory.CreateUserWithEmail(normalizedEmail, u =>
                        {
                            u.IdProofingStatus = scenario.IalLevel is UserIalLevel.IAL1plus or UserIalLevel.IAL2
                                ? IdProofingStatus.Completed
                                : IdProofingStatus.NotStarted;
                            u.IalLevel = scenario.IalLevel;
                            if (scenario.IalLevel is UserIalLevel.IAL1plus or UserIalLevel.IAL2)
                            {
                                u.IdProofingCompletedAt = now.AddDays(DaysSinceIdProofingCompleted);
                            }
                            u.IsCoLoaded = false;
                            u.CoLoadedLastUpdated = null;
                        });
                    }

                    _dataSeeder.AddUsers(new[] { user });
                    seededCount++;
                    _logger?.LogInformation("Successfully seeded user {Email} with IAL level {IalLevel}", normalizedEmail, scenario.IalLevel);
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
            var userEmails = testUsers
                .Select(u => u.Email)
                .Where(e => e != null)
                .Cast<string>()
                .ToList();
            var existingEmails = _dataSeeder.GetExistingUserEmails(userEmails);

            var usersToAdd = testUsers
                .Where(user => user.Email != null
                    && !existingEmails.Contains(EmailNormalizer.Normalize(user.Email)))
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
    /// Deletes users matching the configured email pattern to avoid deleting production data.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task ClearSeededDataAsync(CancellationToken cancellationToken = default)
    {
        var seededEmails = SeedScenarios.UserScenarios
            .Select(scenario => EmailNormalizer.Normalize(_settings.BuildEmail(scenario.Name)))
            .ToList();

        var existingSeededEmails = await _dataSeeder.GetExistingUserEmailsAsync(seededEmails, cancellationToken);

        if (existingSeededEmails.Count > 0)
        {
            await _dataSeeder.RemoveUserOptInsByEmailAsync(existingSeededEmails, cancellationToken);
            await _dataSeeder.RemoveUsersByEmailAsync(existingSeededEmails, cancellationToken);
            await _dataSeeder.SaveChangesAsync(cancellationToken);
        }
    }
}
