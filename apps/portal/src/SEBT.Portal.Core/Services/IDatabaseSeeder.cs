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
    /// Seeds the database with specific test users for development.
    /// </summary>
    /// <param name="useMockHouseholdData">Whether to seed users corresponding to household mock data.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task SeedTestUsersAsync(bool useMockHouseholdData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the seed-scenario users matched by email, along with their opt-ins.
    /// Users who signed in through an identity provider no longer carry an email and are not matched.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task ClearSeededDataAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes every user and opt-in, whatever its email; cascades remove their dependent rows.
    /// For disposable demo databases only.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task ClearAllUserDataAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes and recreates a single known seed scenario user so mutable E2E suites
    /// can restore a clean starting state without clearing the whole seed set.
    /// </summary>
    /// <param name="scenarioName">A name from <c>SeedScenarios</c>.</param>
    /// <param name="useMockHouseholdData">Whether to reseed via the mock-household path.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task ReseedUserScenarioAsync(
        string scenarioName,
        bool useMockHouseholdData,
        CancellationToken cancellationToken = default);
}
