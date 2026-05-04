using Microsoft.EntityFrameworkCore;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Data.Entities;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Infrastructure.Repositories;

/// <summary>
/// Database-backed implementation of <see cref="IUserRepository"/> using Entity Framework Core.
/// </summary>
public class DatabaseUserRepository(
    PortalDbContext dbContext,
    IIdentifierHasher identifierHasher,
    IPiiSymmetricEncryption piiEncryption,
    IEmailLookupHasher emailLookupHasher) : IUserRepository
{
    public async Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail == null)
        {
            return null;
        }

        var lookupHash = emailLookupHasher.HashNormalized(normalizedEmail);
        if (lookupHash == null)
        {
            return null;
        }

        var entity = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                u =>
                    u.EmailHash == lookupHash ||
                    u.EmailHash == null && u.Email != null && u.Email == normalizedEmail,
                cancellationToken);

        return entity == null ? null : UserEncryptedFieldMapper.ToDomain(entity, piiEncryption);
    }

    public async Task CreateUserAsync(User user, CancellationToken cancellationToken = default)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        if (string.IsNullOrWhiteSpace(user.Email) && string.IsNullOrWhiteSpace(user.ExternalProviderId))
        {
            throw new ArgumentException(
                "Either Email or ExternalProviderId must be provided.", nameof(user));
        }

        var entity = NewTrackedEntityStructural(user);
        UserEncryptedFieldMapper.EncryptIdentifiers(
            entity, user, piiEncryption, emailLookupHasher, includeEmailColumns: true);
        entity.Ssn = identifierHasher.HashForStorage(user.Ssn);

        dbContext.Users.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateUserAsync(User user, CancellationToken cancellationToken = default)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        if (user.Id == Guid.Empty)
        {
            throw new ArgumentException("User Id must be assigned for updates.", nameof(user));
        }

        var entity = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == user.Id, cancellationToken);

        if (entity == null)
        {
            throw new InvalidOperationException($"User with Id {user.Id} not found.");
        }

        if (NormalizeEmail(user.Email) != null)
        {
            var normalizedIncoming = NormalizeEmail(user.Email)!;
            var envelopePrefix = PiiAesGcmSymmetricEncryption.EnvelopePrefix;
            var plaintextDupe = await dbContext.Users.AnyAsync(
                u =>
                    u.Id != user.Id &&
                    u.EmailHash == null &&
                    u.Email != null &&
                    !u.Email.StartsWith(envelopePrefix) &&
                    u.Email == normalizedIncoming,
                cancellationToken);

            if (plaintextDupe)
            {
                throw new InvalidOperationException("A user with this email address already exists.");
            }
        }

        if (user.Email != null)
        {
            UserEncryptedFieldMapper.EncryptIdentifiers(
                entity, user, piiEncryption, emailLookupHasher, includeEmailColumns: true);
        }
        else
        {
            UserEncryptedFieldMapper.EncryptIdentifiers(
                entity, user, piiEncryption, emailLookupHasher, includeEmailColumns: false);
        }

        entity.IdProofingStatus = (int)user.IdProofingStatus;
        entity.IalLevel = (int)user.IalLevel;
        entity.IdProofingSessionId = user.IdProofingSessionId;
        entity.IdProofingCompletedAt = user.IdProofingCompletedAt;
        entity.IdProofingExpiresAt = user.IdProofingExpiresAt;
        entity.IsCoLoaded = user.IsCoLoaded;
        entity.CoLoadedLastUpdated = user.CoLoadedLastUpdated;
        entity.Ssn = identifierHasher.HashForStorage(user.Ssn);
        entity.IdProofingAttemptCount = user.IdProofingAttemptCount;
        entity.UpdatedAt = DateTime.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            if (ex.InnerException?.Message.Contains("UNIQUE") == true ||
                ex.InnerException?.Message.Contains("duplicate key") == true ||
                ex.InnerException?.Message.Contains("IX_Users_EmailHash") == true)
            {
                throw new InvalidOperationException("A user with this email address already exists.", ex);
            }

            throw;
        }
    }

    public async Task<(User user, bool isNewUser)> GetOrCreateUserAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email cannot be null or empty.", nameof(email));
        }

        var normalizedEmail = NormalizeEmail(email)!;
        var lookupHash = emailLookupHasher.HashNormalized(normalizedEmail)!;

        var entity = await dbContext.Users
            .FirstOrDefaultAsync(
                u =>
                    u.EmailHash == lookupHash ||
                    u.EmailHash == null && u.Email != null && u.Email == normalizedEmail,
                cancellationToken);

        if (entity != null)
        {
            return (UserEncryptedFieldMapper.ToDomain(entity, piiEncryption), false);
        }

        var draftUser = new User
        {
            Email = normalizedEmail,
            IdProofingStatus = IdProofingStatus.NotStarted,
            IalLevel = UserIalLevel.None,
            IsCoLoaded = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var newEntity = NewTrackedEntityStructural(draftUser);
        UserEncryptedFieldMapper.EncryptIdentifiers(
            newEntity, draftUser, piiEncryption, emailLookupHasher, includeEmailColumns: true);

        dbContext.Users.Add(newEntity);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            if (ex.InnerException?.Message.Contains("PRIMARY KEY") == true ||
                ex.InnerException?.Message.Contains("UNIQUE") == true ||
                ex.InnerException?.Message.Contains("duplicate key") == true)
            {
                entity = await dbContext.Users.FirstOrDefaultAsync(
                    u =>
                        u.EmailHash == lookupHash ||
                        u.EmailHash == null && u.Email != null && u.Email == normalizedEmail,
                    cancellationToken);

                if (entity != null)
                {
                    return (UserEncryptedFieldMapper.ToDomain(entity, piiEncryption), false);
                }
            }

            throw;
        }

        return (UserEncryptedFieldMapper.ToDomain(newEntity, piiEncryption), true);
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

        return entity == null ? null : UserEncryptedFieldMapper.ToDomain(entity, piiEncryption);
    }

    public async Task<User?> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        var entity = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        return entity == null ? null : UserEncryptedFieldMapper.ToDomain(entity, piiEncryption);
    }

    public async Task<User?> GetUserByExternalIdAsync(string externalProviderId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalProviderId))
        {
            return null;
        }

        var entity = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.ExternalProviderId == externalProviderId, cancellationToken);

        return entity == null ? null : UserEncryptedFieldMapper.ToDomain(entity, piiEncryption);
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

        var entity = await dbContext.Users.FirstOrDefaultAsync(
            u => u.ExternalProviderId == externalProviderId,
            cancellationToken);

        if (entity != null)
        {
            return (UserEncryptedFieldMapper.ToDomain(entity, piiEncryption), false);
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            var normalizedEmail = NormalizeEmail(email);
            var lookupHash = normalizedEmail != null ? emailLookupHasher.HashNormalized(normalizedEmail) : null;

            if (normalizedEmail != null && lookupHash != null)
            {
                var legacyEntity = await dbContext.Users.FirstOrDefaultAsync(
                    u =>
                        u.ExternalProviderId == null &&
                        (u.EmailHash == lookupHash ||
                         u.EmailHash == null && u.Email != null && u.Email == normalizedEmail),
                    cancellationToken);

                if (legacyEntity != null)
                {
                    legacyEntity.ExternalProviderId = externalProviderId;
                    UserEncryptedFieldMapper.ClearEmailColumns(legacyEntity);
                    legacyEntity.UpdatedAt = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync(cancellationToken);
                    return (UserEncryptedFieldMapper.ToDomain(legacyEntity, piiEncryption), false);
                }
            }
        }

        var draftOidcUser = new User
        {
            ExternalProviderId = externalProviderId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var newEntity = NewTrackedEntityStructural(draftOidcUser);
        UserEncryptedFieldMapper.EncryptIdentifiers(
            newEntity, draftOidcUser, piiEncryption, emailLookupHasher, includeEmailColumns: true);

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
                entity = await dbContext.Users.FirstOrDefaultAsync(
                    u => u.ExternalProviderId == externalProviderId, cancellationToken);

                if (entity != null)
                {
                    return (UserEncryptedFieldMapper.ToDomain(entity, piiEncryption), false);
                }
            }

            throw;
        }

        return (UserEncryptedFieldMapper.ToDomain(newEntity, piiEncryption), true);
    }

    private static string? NormalizeEmail(string? email) => EmailNormalizer.NormalizeOrNull(email);

    private UserEntity NewTrackedEntityStructural(User user)
    {
        return new UserEntity
        {
            Id = user.Id,
            ExternalProviderId = user.ExternalProviderId,
            IdProofingStatus = (int)user.IdProofingStatus,
            IalLevel = (int)user.IalLevel,
            IdProofingSessionId = user.IdProofingSessionId,
            IdProofingCompletedAt = user.IdProofingCompletedAt,
            IdProofingExpiresAt = user.IdProofingExpiresAt,
            IsCoLoaded = user.IsCoLoaded,
            CoLoadedLastUpdated = user.CoLoadedLastUpdated,
            IdProofingAttemptCount = user.IdProofingAttemptCount,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}
