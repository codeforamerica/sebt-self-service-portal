using Microsoft.EntityFrameworkCore;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Data.Entities;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of IDataSeeder that handles database operations
/// and mapping between domain models and entities.
/// </summary>
public class DataSeeder : IDataSeeder
{
    private readonly PortalDbContext _dbContext;

    public DataSeeder(PortalDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Maps a User domain model to a UserEntity for database persistence.
    /// </summary>
    private static UserEntity MapToEntity(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(user.Email);

        var normalizedEmail = EmailNormalizer.Normalize(user.Email);
        return new UserEntity
        {
            Id = user.Id, // Will be 0 for new users, set by database
            Email = normalizedEmail,
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

    /// <summary>
    /// Handles DbUpdateException for unique constraint violations that may occur during seeding.
    /// Returns true if the exception was handled (duplicate key violation), false otherwise.
    /// </summary>
    private static bool HandleDuplicateKeyException(DbUpdateException ex)
    {
        if (ex.InnerException?.Message.Contains("UNIQUE") == true ||
            ex.InnerException?.Message.Contains("duplicate key") == true ||
            ex.InnerException?.Message.Contains("IX_Users_Email") == true)
        {
            // Users may have been created by another process
            return true;
        }
        return false;
    }

    public async Task<bool> AnyUsersExistAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users.AnyAsync(cancellationToken);
    }

    public async Task<HashSet<string>> GetExistingUserEmailsAsync(IEnumerable<string> emails, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(emails);

        var normalizedEmails = emails.Select(EmailNormalizer.Normalize).ToList();
        var existingEmails = await _dbContext.Users
            .Where(u => normalizedEmails.Contains(u.Email))
            .Select(u => u.Email)
            .ToListAsync(cancellationToken);
        return existingEmails.ToHashSet();
    }

    public async Task AddUsersAsync(IEnumerable<User> users, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(users);

        var usersList = users.ToList();
        if (usersList.Count == 0)
        {
            return;
        }

        var entities = usersList.Select(MapToEntity).ToList();
        _dbContext.Users.AddRange(entities);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            if (!HandleDuplicateKeyException(ex))
            {
                throw;
            }
        }
    }

    public async Task<List<string>> GetUserEmailsByDomainAsync(string emailDomain, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(emailDomain);

        return await _dbContext.Users
            .Where(u => u.Email.EndsWith(emailDomain))
            .Select(u => u.Email)
            .ToListAsync(cancellationToken);
    }

    public async Task RemoveUsersByEmailAsync(IEnumerable<string> emails, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(emails);

        var normalizedEmails = emails.Select(EmailNormalizer.Normalize).ToList();
        var usersToRemove = await _dbContext.Users
            .Where(u => normalizedEmails.Contains(u.Email))
            .ToListAsync(cancellationToken);

        _dbContext.Users.RemoveRange(usersToRemove);
    }

    public async Task RemoveUserOptInsByEmailAsync(IEnumerable<string> emails, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(emails);

        var normalizedEmails = emails.Select(EmailNormalizer.Normalize).ToList();
        var optInsToRemove = await _dbContext.UserOptIns
            .Where(o => normalizedEmails.Contains(o.Email))
            .ToListAsync(cancellationToken);

        _dbContext.UserOptIns.RemoveRange(optInsToRemove);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public HashSet<string> GetExistingUserEmails(IEnumerable<string> emails)
    {
        ArgumentNullException.ThrowIfNull(emails);

        var normalizedEmails = emails.Select(EmailNormalizer.Normalize).ToList();
        var existingEmails = _dbContext.Users
            .Where(u => normalizedEmails.Contains(u.Email))
            .Select(u => u.Email)
            .ToList();
        return existingEmails.ToHashSet();
    }

    public bool AnyUsersExist()
    {
        return _dbContext.Users.Any();
    }

    public void AddUsers(IEnumerable<User> users)
    {
        ArgumentNullException.ThrowIfNull(users);

        var usersList = users.ToList();
        if (usersList.Count == 0)
        {
            return;
        }

        var entities = usersList.Select(MapToEntity).ToList();
        _dbContext.Users.AddRange(entities);

        try
        {
            _dbContext.SaveChanges();
        }
        catch (DbUpdateException ex)
        {
            if (!HandleDuplicateKeyException(ex))
            {
                throw;
            }
        }
    }

    public void SaveChanges()
    {
        _dbContext.SaveChanges();
    }
}
