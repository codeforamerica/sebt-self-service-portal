using Microsoft.EntityFrameworkCore;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Data.Entities;

namespace SEBT.Portal.Infrastructure.Repositories;

/// <summary>
/// Database-backed implementation of <see cref="IUserRepository"/> using Entity Framework Core.
/// </summary>
public class DatabaseUserRepository(PortalDbContext dbContext, IIdentifierHasher identifierHasher) : IUserRepository
{
    public async Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalizedEmail = NormalizeEmail(email);
        var entity = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        return entity == null ? null : MapToDomainModel(entity);
    }

    public async Task CreateUserAsync(User user, CancellationToken cancellationToken = default)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        // OTP users must have an email; OIDC users must have an ExternalProviderId.
        // At least one identifier is required.
        if (string.IsNullOrWhiteSpace(user.Email) && string.IsNullOrWhiteSpace(user.ExternalProviderId))
        {
            throw new ArgumentException(
                "Either Email or ExternalProviderId must be provided.", nameof(user));
        }

        var entity = MapToEntity(user);
        // Normalize email to lowercase for consistent storage (when present)
        if (entity.Email != null)
        {
            entity.Email = NormalizeEmail(entity.Email);
        }
        dbContext.Users.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateUserAsync(User user, CancellationToken cancellationToken = default)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        if (user.Id <= 0)
        {
            throw new ArgumentException("User Id must be greater than zero for updates.", nameof(user));
        }

        var entity = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == user.Id, cancellationToken);

        if (entity == null)
        {
            throw new InvalidOperationException($"User with Id {user.Id} not found.");
        }

        // Update email only when the caller provides one (OTP users).
        // OIDC users have null email — leave the DB value unchanged in that case.
        if (user.Email != null)
        {
            var normalizedEmail = NormalizeEmail(user.Email);
            if (entity.Email != normalizedEmail)
            {
                entity.Email = normalizedEmail;
            }
        }

        // Update properties
        entity.IdProofingStatus = (int)user.IdProofingStatus;
        entity.IalLevel = (int)user.IalLevel;
        entity.IdProofingSessionId = user.IdProofingSessionId;
        entity.IdProofingCompletedAt = user.IdProofingCompletedAt;
        entity.IdProofingExpiresAt = user.IdProofingExpiresAt;
        entity.IsCoLoaded = user.IsCoLoaded;
        entity.CoLoadedLastUpdated = user.CoLoadedLastUpdated;
        entity.Phone = user.Phone;
        entity.SnapId = user.SnapId;
        entity.TanfId = user.TanfId;
        entity.Ssn = identifierHasher.HashForStorage(user.Ssn);
        entity.IdProofingAttemptCount = user.IdProofingAttemptCount;
        entity.UpdatedAt = DateTime.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Handle unique constraint violation for email (race condition or duplicate email)
            if (ex.InnerException?.Message.Contains("UNIQUE") == true ||
                ex.InnerException?.Message.Contains("duplicate key") == true ||
                ex.InnerException?.Message.Contains("IX_Users_Email") == true)
            {
                throw new InvalidOperationException($"A user with email {user.Email} already exists.", ex);
            }

            // Re-throw if it's not a unique constraint violation
            throw;
        }
    }

    public async Task<(User user, bool isNewUser)> GetOrCreateUserAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email cannot be null or empty.", nameof(email));
        }

        var normalizedEmail = NormalizeEmail(email);
        var entity = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (entity != null)
        {
            return (MapToDomainModel(entity), false);
        }

        // Create new user with normalized email
        var newEntity = new UserEntity
        {
            Email = normalizedEmail,
            IdProofingStatus = (int)IdProofingStatus.NotStarted,
            IalLevel = (int)UserIalLevel.None,
            IsCoLoaded = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.Users.Add(newEntity);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Handle race condition: if another request created the user between our check and save,
            // retry by fetching the existing user
            if (ex.InnerException?.Message.Contains("PRIMARY KEY") == true ||
                ex.InnerException?.Message.Contains("UNIQUE") == true ||
                ex.InnerException?.Message.Contains("duplicate key") == true)
            {
                // User was created by another request, fetch it
                entity = await dbContext.Users
                    .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

                if (entity != null)
                {
                    return (MapToDomainModel(entity), false);
                }
            }

            // Re-throw if it's not a duplicate key violation
            throw;
        }

        return (MapToDomainModel(newEntity), true);
    }

    public async Task<User?> GetUserBySessionIdAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        var entity = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.IdProofingSessionId == sessionId, cancellationToken);

        return entity == null ? null : MapToDomainModel(entity);
    }

    public async Task<User?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return null;
        }

        var entity = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        return entity == null ? null : MapToDomainModel(entity);
    }

    public async Task<User?> GetUserByExternalIdAsync(
        string externalProviderId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalProviderId))
        {
            return null;
        }

        var entity = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.ExternalProviderId == externalProviderId, cancellationToken);

        return entity == null ? null : MapToDomainModel(entity);
    }

    public async Task<(User user, bool isNewUser)> GetOrCreateUserByExternalIdAsync(
        string externalProviderId,
        string? email = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalProviderId))
        {
            throw new ArgumentException(
                "External provider ID cannot be null or empty.", nameof(externalProviderId));
        }

        // Primary lookup: by ExternalProviderId (the steady-state path)
        var entity = await dbContext.Users
            .FirstOrDefaultAsync(u => u.ExternalProviderId == externalProviderId, cancellationToken);

        if (entity != null)
        {
            return (MapToDomainModel(entity), false);
        }

        // Migration fallback: if an email hint is provided, check for a legacy
        // email-only record and adopt it by setting ExternalProviderId.
        // TODO: Remove this fallback once all existing users have logged in
        // under the new sub-based identity flow.
        if (!string.IsNullOrWhiteSpace(email))
        {
            var normalizedEmail = NormalizeEmail(email);
            var legacyEntity = await dbContext.Users
                .FirstOrDefaultAsync(
                    u => u.Email == normalizedEmail && u.ExternalProviderId == null,
                    cancellationToken);

            if (legacyEntity != null)
            {
                legacyEntity.ExternalProviderId = externalProviderId;
                legacyEntity.Email = null; // OIDC users derive email from IdP claims, not DB
                legacyEntity.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
                return (MapToDomainModel(legacyEntity), false);
            }
        }

        // No existing record found — create a new minimal one
        var newEntity = new UserEntity
        {
            ExternalProviderId = externalProviderId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.Users.Add(newEntity);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            if (ex.InnerException?.Message.Contains("UNIQUE") == true ||
                ex.InnerException?.Message.Contains("duplicate key") == true)
            {
                entity = await dbContext.Users
                    .FirstOrDefaultAsync(
                        u => u.ExternalProviderId == externalProviderId, cancellationToken);

                if (entity != null)
                {
                    return (MapToDomainModel(entity), false);
                }
            }
            throw;
        }

        return (MapToDomainModel(newEntity), true);
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

    private static User MapToDomainModel(UserEntity entity)
    {
        return new User
        {
            Id = entity.Id,
            Email = entity.Email,
            ExternalProviderId = entity.ExternalProviderId,
            IdProofingStatus = (IdProofingStatus)entity.IdProofingStatus,
            IalLevel = (UserIalLevel)entity.IalLevel,
            IdProofingSessionId = entity.IdProofingSessionId,
            IdProofingCompletedAt = entity.IdProofingCompletedAt,
            IdProofingExpiresAt = entity.IdProofingExpiresAt,
            IsCoLoaded = entity.IsCoLoaded,
            CoLoadedLastUpdated = entity.CoLoadedLastUpdated,
            Phone = entity.Phone,
            SnapId = entity.SnapId,
            TanfId = entity.TanfId,
            Ssn = entity.Ssn,
            IdProofingAttemptCount = entity.IdProofingAttemptCount,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private UserEntity MapToEntity(User user)
    {
        return new UserEntity
        {
            Id = user.Id, // Will be 0 for new users, set by database
            Email = user.Email, // Will be normalized in calling method
            ExternalProviderId = user.ExternalProviderId,
            IdProofingStatus = (int)user.IdProofingStatus,
            IalLevel = (int)user.IalLevel,
            IdProofingSessionId = user.IdProofingSessionId,
            IdProofingCompletedAt = user.IdProofingCompletedAt,
            IdProofingExpiresAt = user.IdProofingExpiresAt,
            IsCoLoaded = user.IsCoLoaded,
            CoLoadedLastUpdated = user.CoLoadedLastUpdated,
            Phone = user.Phone,
            SnapId = user.SnapId,
            TanfId = user.TanfId,
            Ssn = identifierHasher.HashForStorage(user.Ssn),
            IdProofingAttemptCount = user.IdProofingAttemptCount,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}
