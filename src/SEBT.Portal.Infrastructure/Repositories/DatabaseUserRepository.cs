using Microsoft.EntityFrameworkCore;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Data.Entities;

namespace SEBT.Portal.Infrastructure.Repositories;

/// <summary>
/// Database-backed implementation of <see cref="IUserRepository"/> using Entity Framework Core.
/// </summary>
/// <param name="dbContext">The database context for accessing user data.</param>
public class DatabaseUserRepository(PortalDbContext dbContext) : IUserRepository
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

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            throw new ArgumentException("Email cannot be null or empty.", nameof(user));
        }

        var entity = MapToEntity(user);
        // Normalize email to lowercase for consistent storage
        entity.Email = NormalizeEmail(entity.Email);
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

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            throw new ArgumentException("Email cannot be null or empty.", nameof(user));
        }

        var entity = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == user.Id, cancellationToken);

        if (entity == null)
        {
            throw new InvalidOperationException($"User with Id {user.Id} not found.");
        }

        var normalizedEmail = NormalizeEmail(user.Email);

        if (entity.Email != normalizedEmail)
        {
            entity.Email = normalizedEmail;
        }

        // Update properties
        entity.IdProofingStatus = (int)user.IdProofingStatus;
        entity.IdProofingSessionId = user.IdProofingSessionId;
        entity.IdProofingCompletedAt = user.IdProofingCompletedAt;
        entity.IdProofingExpiresAt = user.IdProofingExpiresAt;
        entity.IsCoLoaded = user.IsCoLoaded;
        entity.CoLoadedLastUpdated = user.CoLoadedLastUpdated;
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
            IdProofingStatus = (IdProofingStatus)entity.IdProofingStatus,
            IdProofingSessionId = entity.IdProofingSessionId,
            IdProofingCompletedAt = entity.IdProofingCompletedAt,
            IdProofingExpiresAt = entity.IdProofingExpiresAt,
            IsCoLoaded = entity.IsCoLoaded,
            CoLoadedLastUpdated = entity.CoLoadedLastUpdated,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private static UserEntity MapToEntity(User user)
    {
        return new UserEntity
        {
            Id = user.Id, // Will be 0 for new users, set by database
            Email = user.Email, // Will be normalized in calling method
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
}
