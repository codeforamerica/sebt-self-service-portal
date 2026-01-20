using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Helpers;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Service for seeding the database with initial or test data.
/// </summary>
public class DatabaseSeeder : IDatabaseSeeder
{
    private readonly IUserRepository _userRepository;
    private readonly PortalDbContext _dbContext;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<DatabaseSeeder>? _logger;

    private const int DaysSinceIdProofingCompleted = -30;
    private const int DaysUntilIdProofingExpires = 335;
    private const int DaysSinceCoLoadedUpdate = -5;
    private const int DaysSinceBasicIdProofingCompleted = -10;
    private const int DaysUntilBasicIdProofingExpires = 355;

    public DatabaseSeeder(IUserRepository userRepository, PortalDbContext dbContext, IConfiguration? configuration = null, ILogger<DatabaseSeeder>? logger = null)
    {
        _userRepository = userRepository;
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Seeds the database with sample users for development/testing.
    /// </summary>
    /// <param name="userCount">Number of users to create (default: 10).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task SeedUsersAsync(int userCount = 10, CancellationToken cancellationToken = default)
    {
        // Check if users already exist
        if (await _dbContext.Users.AnyAsync(cancellationToken))
        {
            return; // Database already seeded
        }

        for (int i = 0; i < userCount; i++)
        {
            var user = UserFactory.CreateUser();

            await _userRepository.CreateUserAsync(user, cancellationToken);
        }
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
    /// If UseMockHouseholdData is enabled, also seeds users that correspond to household mock data.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task SeedTestUsersAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var seededCount = 0;

        var useMockHouseholdData = _configuration?.GetValue<bool>("UseMockHouseholdData", false) ?? false;

        if (useMockHouseholdData)
        {
            var mappings = GetHouseholdUserMappings();

            foreach (var (email, idProofingStatus) in mappings)
            {
                var normalizedEmail = email?.ToLowerInvariant().Trim() ?? throw new ArgumentException("Email cannot be null", nameof(email));

                var existingUser = await _userRepository.GetUserByEmailAsync(normalizedEmail, cancellationToken);
                if (existingUser != null)
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

                    await _userRepository.CreateUserAsync(user, cancellationToken);
                    seededCount++;
                    _logger?.LogInformation("Successfully seeded user {Email} with ID proofing status {Status}", normalizedEmail, idProofingStatus);
                }
                catch (DbUpdateException ex) when (
                    ex.InnerException?.Message.Contains("PRIMARY KEY") == true ||
                    ex.InnerException?.Message.Contains("UNIQUE") == true ||
                    ex.InnerException?.Message.Contains("duplicate key") == true ||
                    ex.InnerException?.Message.Contains("IX_Users_Email") == true)
                {
                    _logger?.LogWarning("User with email {Email} was created by another process, skipping", normalizedEmail);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
                {
                    _logger?.LogWarning("User with email {Email} was created by another process, skipping", normalizedEmail);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error seeding user {Email}", normalizedEmail);
                    throw;
                }
            }
        }
        else
        {
            // Seed basic test users (original behavior - use this to test against 'true' integration)
            var testUsers = new[]
            {
                UserFactory.CreateCoLoadedUser(u =>
                {
                    u.Email = "co-loaded@example.com";
                    u.IdProofingStatus = IdProofingStatus.Completed;
                    u.CoLoadedLastUpdated = now.AddDays(DaysSinceCoLoadedUpdate);
                    u.IdProofingCompletedAt = now.AddDays(DaysSinceBasicIdProofingCompleted);
                    u.IdProofingExpiresAt = now.AddDays(DaysUntilBasicIdProofingExpires);
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

            foreach (var user in testUsers)
            {
                var normalizedEmail = user.Email?.ToLowerInvariant().Trim() ?? throw new InvalidOperationException("User email cannot be null");

                var existingUser = await _userRepository.GetUserByEmailAsync(normalizedEmail, cancellationToken);
                if (existingUser != null)
                {
                    _logger?.LogDebug("User with email {Email} already exists, skipping", normalizedEmail);
                    continue;
                }

                try
                {
                    await _userRepository.CreateUserAsync(user, cancellationToken);
                    seededCount++;
                    _logger?.LogInformation("Successfully seeded user {Email} with ID proofing status {Status}", normalizedEmail, user.IdProofingStatus);
                }
                catch (DbUpdateException ex) when (
                    ex.InnerException?.Message.Contains("PRIMARY KEY") == true ||
                    ex.InnerException?.Message.Contains("UNIQUE") == true ||
                    ex.InnerException?.Message.Contains("duplicate key") == true ||
                    ex.InnerException?.Message.Contains("IX_Users_Email") == true)
                {
                    _logger?.LogWarning("User with email {Email} was created by another process, skipping", normalizedEmail);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
                {
                    _logger?.LogWarning("User with email {Email} was created by another process, skipping", normalizedEmail);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error seeding user {Email}", normalizedEmail);
                    throw;
                }
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
    /// Clears all seeded data from the database.
    /// WARNING: This will delete all seeded records.  This WILL NOT 
    /// delete any records using InMemory Respositories.  Use with caution.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task ClearSeededDataAsync(CancellationToken cancellationToken = default)
    {
        _dbContext.Users.RemoveRange(_dbContext.Users);
        _dbContext.UserOptIns.RemoveRange(_dbContext.UserOptIns);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
