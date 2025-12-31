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
    public async Task<User?> GetUserByEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalizedEmail = NormalizeEmail(email);
        var entity = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        return entity == null ? null : MapToDomainModel(entity);
    }

    public async Task CreateUserAsync(User user)
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
        await dbContext.SaveChangesAsync();
    }

    public async Task UpdateUserAsync(User user)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            throw new ArgumentException("Email cannot be null or empty.", nameof(user));
        }

        var normalizedEmail = NormalizeEmail(user.Email);
        var entity = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        if (entity == null)
        {
            throw new InvalidOperationException($"User with email {user.Email} not found.");
        }

        // Update properties
        entity.IdProofingStatus = (int)user.IdProofingStatus;
        entity.IdProofingSessionId = user.IdProofingSessionId;
        entity.IdProofingCompletedAt = user.IdProofingCompletedAt;
        entity.IdProofingExpiresAt = user.IdProofingExpiresAt;
        entity.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();
    }

    public async Task<User> GetOrCreateUserAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email cannot be null or empty.", nameof(email));
        }

        var normalizedEmail = NormalizeEmail(email);
        var entity = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        if (entity != null)
        {
            return MapToDomainModel(entity);
        }

        // Create new user with normalized email
        var newEntity = new UserEntity
        {
            Email = normalizedEmail,
            IdProofingStatus = (int)IdProofingStatus.NotStarted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.Users.Add(newEntity);
        await dbContext.SaveChangesAsync();

        return MapToDomainModel(newEntity);
    }

    public async Task<User?> GetUserBySessionIdAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        var entity = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.IdProofingSessionId == sessionId);

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
            Email = entity.Email,
            IdProofingStatus = (IdProofingStatus)entity.IdProofingStatus,
            IdProofingSessionId = entity.IdProofingSessionId,
            IdProofingCompletedAt = entity.IdProofingCompletedAt,
            IdProofingExpiresAt = entity.IdProofingExpiresAt,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private static UserEntity MapToEntity(User user)
    {
        return new UserEntity
        {
            Email = user.Email, // Will be normalized in calling method
            IdProofingStatus = (int)user.IdProofingStatus,
            IdProofingSessionId = user.IdProofingSessionId,
            IdProofingCompletedAt = user.IdProofingCompletedAt,
            IdProofingExpiresAt = user.IdProofingExpiresAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}
