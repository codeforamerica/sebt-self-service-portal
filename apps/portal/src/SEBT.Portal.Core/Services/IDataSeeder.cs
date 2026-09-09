using SEBT.Portal.Core.Models.Auth;

namespace SEBT.Portal.Core.Services;

/// <summary>
/// Abstraction for seeding data into the database.
/// </summary>
public interface IDataSeeder
{
    /// <summary>
    /// Checks if any users exist in the database.
    /// </summary>
    Task<bool> AnyUsersExistAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets existing user emails from the database.
    /// </summary>
    Task<HashSet<string>> GetExistingUserEmailsAsync(IEnumerable<string> emails, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds users to the database.
    /// </summary>
    Task AddUsersAsync(IEnumerable<User> users, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets user emails that end with the specified domain.
    /// </summary>
    Task<List<string>> GetUserEmailsByDomainAsync(string emailDomain, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes users with specified emails from the database.
    /// </summary>
    Task RemoveUsersByEmailAsync(IEnumerable<string> emails, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes user opt-ins with specified emails from the database.
    /// </summary>
    Task RemoveUserOptInsByEmailAsync(IEnumerable<string> emails, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves changes to the database.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronous version for UseSeeding callback.
    /// </summary>
    bool AnyUsersExist();

    /// <summary>
    /// Synchronous version for UseSeeding callback.
    /// Gets existing user emails from the database.
    /// </summary>
    HashSet<string> GetExistingUserEmails(IEnumerable<string> emails);

    /// <summary>
    /// Synchronous version for UseSeeding callback.
    /// </summary>
    void AddUsers(IEnumerable<User> users);

    /// <summary>
    /// Synchronous version for UseSeeding callback.
    /// </summary>
    void SaveChanges();
}
