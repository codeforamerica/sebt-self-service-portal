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
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CreateUserAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing user record.
    /// </summary>
    /// <param name="user">The user to update.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateUserAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets or creates a user by email. If the user doesn't exist, creates a new one.
    /// </summary>
    /// <param name="email">The email address of the user.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A tuple containing the existing or newly created user and a boolean indicating if the user was newly created.</returns>
    Task<(User user, bool isNewUser)> GetOrCreateUserAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a user by their ID proofing session ID.
    /// </summary>
    /// <param name="sessionId">The session ID from the proofing provider.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The user if found; otherwise, <c>null</c>.</returns>
    Task<User?> GetUserBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a user by their database primary key.
    /// </summary>
    /// <param name="id">The user's ID.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The user if found; otherwise, <c>null</c>.</returns>
    Task<User?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets or creates a user by their external identity provider subject ID.
    /// Used for OIDC-authenticated users whose identity is anchored by the IdP's sub claim.
    /// Creates a minimal record (no email, no IAL) — those attributes come from IdP claims.
    ///
    /// When <paramref name="email"/> is provided and no user exists for the given
    /// <paramref name="externalProviderId"/>, falls back to an email-based lookup and
    /// adopts that record by setting its ExternalProviderId. This migrates pre-existing
    /// email-only user records to the new sub-based identity model.
    /// TODO: Remove email fallback migration once all existing users have logged in
    /// under the new flow.
    /// </summary>
    Task<(User user, bool isNewUser)> GetOrCreateUserByExternalIdAsync(
        string externalProviderId,
        string? email = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a user by their external identity provider subject ID.
    /// </summary>
    Task<User?> GetUserByExternalIdAsync(
        string externalProviderId, CancellationToken cancellationToken = default);
}
