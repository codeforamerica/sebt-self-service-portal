using Microsoft.EntityFrameworkCore;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Data.Entities;
using SEBT.Portal.Infrastructure.Helpers;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Service for seeding the database with initial or test data.
/// </summary>
public class DatabaseSeeder : IDatabaseSeeder
{
    private readonly IUserRepository _userRepository;
    private readonly PortalDbContext _dbContext;

    public DatabaseSeeder(IUserRepository userRepository, PortalDbContext dbContext)
    {
        _userRepository = userRepository;
        _dbContext = dbContext;
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

        var users = new List<User>();
        for (int i = 0; i < userCount; i++)
        {
            users.Add(UserFactory.CreateUser());
        }

        var entities = users.Select(MapToEntity).ToList();

        _dbContext.Users.AddRange(entities);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            if (!HandleDuplicateKeyException(ex))
            {
                throw;
            }
        }
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
                // CreatedAt and UpdatedAt are init-only, so factory-generated dates are used
            }),
            UserFactory.CreateNonCoLoadedUser(u =>
            {
                u.Email = "non-co-loaded@example.com";
                u.IdProofingStatus = IdProofingStatus.InProgress;
                // CreatedAt and UpdatedAt are init-only, so factory-generated dates are used
            }),
            UserFactory.CreateNonCoLoadedUser(u =>
            {
                u.Email = "not-started@example.com";
                u.IdProofingStatus = IdProofingStatus.NotStarted;
                // CreatedAt and UpdatedAt are init-only, so factory-generated dates are used
            })
        };
    }

    /// <summary>
    /// Normalizes an email address to lowercase for consistent storage and comparison.
    /// </summary>
    /// <param name="email">The email address to normalize.</param>
    /// <returns>The normalized (lowercase) email address.</returns>
    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Maps a User domain model to a UserEntity for database persistence.
    /// </summary>
    /// <param name="user">The User domain model to map.</param>
    /// <returns>A UserEntity ready for database insertion.</returns>
    private static UserEntity MapToEntity(User user)
    {
        var normalizedEmail = NormalizeEmail(user.Email);
        return new UserEntity
        {
            Id = user.Id, // Will be 0 for new users, set by database
            Email = normalizedEmail,
            IdProofingStatus = (int)user.IdProofingStatus,
            IdProofingSessionId = user.IdProofingSessionId,
            IdProofingCompletedAt = user.IdProofingCompletedAt,
            IdProofingExpiresAt = user.IdProofingExpiresAt,
            IsCoLoaded = user.IsCoLoaded,
            CoLoadedLastUpdated = user.CoLoadedLastUpdated,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    /// <summary>
    /// Handles DbUpdateException for unique constraint violations that may occur during seeding.
    /// Returns true if the exception was handled (duplicate key violation), false otherwise.
    /// </summary>
    /// <param name="ex">The DbUpdateException to check.</param>
    /// <returns>True if the exception was a duplicate key violation and was handled, false otherwise.</returns>
    private static bool HandleDuplicateKeyException(DbUpdateException ex)
    {
        if (ex.InnerException?.Message.Contains("UNIQUE") == true ||
            ex.InnerException?.Message.Contains("duplicate key") == true ||
            ex.InnerException?.Message.Contains("IX_Users_Email") == true)
        {
            // Users may have been created by another process
            return true;
        }
        return false;
    }

    /// <summary>
    /// Seeds the database with specific test users for development.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task SeedTestUsersAsync(CancellationToken cancellationToken = default)
    {
        var testUsers = CreateTestUsers();

        var normalizedEmails = testUsers.Select(u => NormalizeEmail(u.Email)).ToList();
        var existingEmails = await _dbContext.Users
            .Where(u => normalizedEmails.Contains(u.Email))
            .Select(u => u.Email)
            .ToListAsync(cancellationToken);

        var existingEmailSet = existingEmails.ToHashSet();

        var entitiesToAdd = testUsers
            .Where(user => !existingEmailSet.Contains(NormalizeEmail(user.Email)))
            .Select(MapToEntity)
            .ToList();

        if (entitiesToAdd.Count > 0)
        {
            _dbContext.Users.AddRange(entitiesToAdd);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                if (!HandleDuplicateKeyException(ex))
                {
                    throw;
                }
            }
        }
    }

    /// <summary>
    /// Synchronous version of SeedTestUsersAsync for use in UseSeeding callback.
    /// </summary>
    public void SeedTestUsers()
    {
        var testUsers = CreateTestUsers();

        foreach (var user in testUsers)
        {
            var normalizedEmail = NormalizeEmail(user.Email);
            var existingEntity = _dbContext.Users
                .AsNoTracking()
                .FirstOrDefault(u => u.Email == normalizedEmail);

            if (existingEntity == null)
            {
                _dbContext.Users.Add(MapToEntity(user));
            }
        }

        try
        {
            _dbContext.SaveChanges();
        }
        catch (DbUpdateException ex)
        {
            if (!HandleDuplicateKeyException(ex))
            {
                throw;
            }
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

        var seededUsers = await _dbContext.Users
            .Where(u => u.Email.EndsWith(seededEmailDomain))
            .ToListAsync(cancellationToken);

        var seededUserEmails = seededUsers.Select(u => u.Email).ToList();

        var seededOptIns = await _dbContext.UserOptIns
            .Where(o => seededUserEmails.Contains(o.Email))
            .ToListAsync(cancellationToken);

        _dbContext.Users.RemoveRange(seededUsers);
        _dbContext.UserOptIns.RemoveRange(seededOptIns);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
