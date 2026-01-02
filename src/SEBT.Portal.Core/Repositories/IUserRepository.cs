using SEBT.Portal.Core.Models.Auth;

namespace SEBT.Portal.Core.Repositories;

/// <summary>
/// Repository interface for managing user data and ID proofing status.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Retrieves a user by their email address.
    /// </summary>
    /// <param name="email">The email address of the user.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The user if found; otherwise, <c>null</c>.</returns>
    Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new user record.
    /// </summary>
    /// <param name="user">The user to create.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CreateUserAsync(User user);

    /// <summary>
    /// Updates an existing user record.
    /// </summary>
    /// <param name="user">The user to update.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateUserAsync(User user);

    /// <summary>
    /// Gets or creates a user by email. If the user doesn't exist, creates a new one.
    /// </summary>
    /// <param name="email">The email address of the user.</param>
    /// <returns>The existing or newly created user.</returns>
    Task<User> GetOrCreateUserAsync(string email);

    /// <summary>
    /// Retrieves a user by their ID proofing session ID.
    /// </summary>
    /// <param name="sessionId">The session ID from the proofing provider.</param>
    /// <returns>The user if found; otherwise, <c>null</c>.</returns>
    Task<User?> GetUserBySessionIdAsync(string sessionId);
}
