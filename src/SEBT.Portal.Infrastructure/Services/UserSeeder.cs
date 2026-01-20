using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Data.Entities;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Seeds users in the database that correspond to seeded household data.
/// Users are created with emails matching household emails and appropriate ID proofing statuses.
/// </summary>
public static class UserSeeder
{
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
    /// Seeds users in the database that correspond to seeded household data.
    /// Only creates users if they don't already exist.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public static async Task SeedUsersAsync(PortalDbContext context, ILogger logger, CancellationToken cancellationToken = default)
    {
        var mappings = GetHouseholdUserMappings();
        var now = DateTime.UtcNow;

        // Batch fetch existing users to avoid N+1 queries
        var normalizedEmails = mappings.Keys.Select(e => e.ToLowerInvariant().Trim()).ToList();
        var existingEmails = await context.Users
            .Where(u => normalizedEmails.Contains(u.Email))
            .Select(u => u.Email)
            .ToHashSetAsync(cancellationToken);

        var usersToAdd = new List<UserEntity>();

        foreach (var (email, idProofingStatus) in mappings)
        {
            var normalizedEmail = email.ToLowerInvariant().Trim();

            if (existingEmails.Contains(normalizedEmail))
            {
                logger.LogDebug("User with email {Email} already exists, skipping", normalizedEmail);
                continue;
            }

            // Create new user
            var user = new UserEntity
            {
                Email = normalizedEmail,
                IdProofingStatus = (int)idProofingStatus,
                IdProofingSessionId = null,
                IdProofingCompletedAt = idProofingStatus == IdProofingStatus.Completed ? now.AddDays(-30) : null,
                IdProofingExpiresAt = idProofingStatus == IdProofingStatus.Completed ? now.AddDays(335) : null, // Expires in ~1 year
                CreatedAt = now,
                UpdatedAt = now
            };

            usersToAdd.Add(user);
            logger.LogInformation("Preparing to seed user {Email} with ID proofing status {Status}", normalizedEmail, idProofingStatus);
        }

        if (usersToAdd.Count == 0)
        {
            logger.LogInformation("All users already exist, no seeding needed");
            return;
        }

        try
        {
            await context.Users.AddRangeAsync(usersToAdd, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Successfully seeded {Count} users", usersToAdd.Count);
        }
        catch (DbUpdateException ex)
        {
            // Handle race condition: if users were created between our check and save
            if (ex.InnerException?.Message.Contains("PRIMARY KEY") == true ||
                ex.InnerException?.Message.Contains("UNIQUE") == true ||
                ex.InnerException?.Message.Contains("duplicate key") == true)
            {
                logger.LogWarning("Some users may have been created by another process, continuing");
                // This is acceptable for seeding - idempotent operation
            }
            else
            {
                logger.LogError(ex, "Error seeding users");
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error seeding users");
            throw;
        }
    }
}
