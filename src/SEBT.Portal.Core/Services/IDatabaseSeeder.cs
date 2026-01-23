namespace SEBT.Portal.Core.Services;

/// <summary>
/// Service for seeding the database with initial or test data.
/// </summary>
public interface IDatabaseSeeder
{
    /// <summary>
    /// Seeds the database with sample users for development/testing.
    /// </summary>
    /// <param name="userCount">Number of users to create (default: 10).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SeedUsersAsync(int userCount = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Seeds the database with specific test users for development.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SeedTestUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all seeded data from the database.
    /// WARNING: This will delete all users and opt-ins. Use with caution.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task ClearSeededDataAsync(CancellationToken cancellationToken = default);
}
