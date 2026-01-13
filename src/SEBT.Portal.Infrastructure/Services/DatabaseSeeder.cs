using Microsoft.EntityFrameworkCore;
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

        for (int i = 0; i < userCount; i++)
        {
            var user = UserFactory.CreateUser();

            await _userRepository.CreateUserAsync(user, cancellationToken);
        }
    }

    /// <summary>
    /// Seeds the database with specific test users for development.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task SeedTestUsersAsync(CancellationToken cancellationToken = default)
    {
        var testUsers = new[]
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

        foreach (var user in testUsers)
        {
            var existingUser = await _userRepository.GetUserByEmailAsync(user.Email, cancellationToken);
            if (existingUser == null)
            {
                await _userRepository.CreateUserAsync(user, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Clears all seeded data from the database.
    /// WARNING: This will delete all seeded records. Use with caution.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task ClearSeededDataAsync(CancellationToken cancellationToken = default)
    {
        _dbContext.Users.RemoveRange(_dbContext.Users);
        _dbContext.UserOptIns.RemoveRange(_dbContext.UserOptIns);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
