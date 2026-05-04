using Microsoft.EntityFrameworkCore;
using SEBT.Portal.Core.Exceptions;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Data.Entities;
using SEBT.Portal.Infrastructure.Repositories;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of IDataSeeder that handles database operations
/// and mapping between domain models and entities.
/// </summary>
public class DataSeeder : IDataSeeder
{
    private readonly PortalDbContext _dbContext;
    private readonly IIdentifierHasher _identifierHasher;
    private readonly IPiiSymmetricEncryption _piiSymmetricEncryption;
    private readonly IEmailLookupHasher _emailLookupHasher;

    public DataSeeder(
        PortalDbContext dbContext,
        IIdentifierHasher identifierHasher,
        IPiiSymmetricEncryption piiSymmetricEncryption,
        IEmailLookupHasher emailLookupHasher)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _identifierHasher = identifierHasher ?? throw new ArgumentNullException(nameof(identifierHasher));
        _piiSymmetricEncryption =
            piiSymmetricEncryption ?? throw new ArgumentNullException(nameof(piiSymmetricEncryption));
        _emailLookupHasher = emailLookupHasher ?? throw new ArgumentNullException(nameof(emailLookupHasher));
    }

    private List<User> FilterOutUsersMatchingExistingEmails(List<User> usersList)
    {
        var keyed = usersList
            .Select(u => (User: u, Normalized: EmailNormalizer.NormalizeOrNull(u.Email)))
            .Where(x => x.Normalized != null)
            .ToList();

        if (keyed.Count == 0)
        {
            return [];
        }

        var distinct = keyed.Select(x => x.Normalized!).Distinct().ToList();
        var existing = GetExistingUserEmails(distinct);
        return keyed
            .Where(x => !existing.Contains(x.Normalized!, StringComparer.Ordinal))
            .Select(x => x.User)
            .ToList();
    }

    private async Task<List<User>> FilterOutUsersMatchingExistingEmailsAsync(
        List<User> usersList,
        CancellationToken cancellationToken)
    {
        var keyed = usersList
            .Select(u => (User: u, Normalized: EmailNormalizer.NormalizeOrNull(u.Email)))
            .Where(x => x.Normalized != null)
            .ToList();

        if (keyed.Count == 0)
        {
            return [];
        }

        var distinct = keyed.Select(x => x.Normalized!).Distinct().ToList();
        var existing = await GetExistingUserEmailsAsync(distinct, cancellationToken);
        return keyed
            .Where(x => !existing.Contains(x.Normalized!, StringComparer.Ordinal))
            .Select(x => x.User)
            .ToList();
    }

    private UserEntity MapToEntity(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var entity = new UserEntity
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
            UpdatedAt = user.UpdatedAt,
            Ssn = _identifierHasher.HashForStorage(user.Ssn)
        };

        UserEncryptedFieldMapper.EncryptIdentifiers(
            entity,
            user,
            _piiSymmetricEncryption,
            _emailLookupHasher,
            includeEmailColumns: true);

        return entity;
    }

    private static bool HandleDuplicateKeyException(DbUpdateException ex)
    {
        if (ex.InnerException?.Message.Contains("UNIQUE") == true ||
            ex.InnerException?.Message.Contains("duplicate key") == true ||
            ex.InnerException?.Message.Contains("IX_Users_EmailHash") == true ||
            ex.InnerException?.Message.Contains("IX_Users_Email") == true)
        {
            return true;
        }

        return false;
    }

    /// <remarks>Decrypt failures are swallowed — callers rely on hashing + legacy plaintext lookups.</remarks>
    private string? TryHydrateNormalizedEmail(string? ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext))
        {
            return null;
        }

        try
        {
            var plain = _piiSymmetricEncryption.DecryptOrPassThroughLegacy(ciphertext);
            return EmailNormalizer.NormalizeOrNull(plain);
        }
        catch (PiiDecryptException)
        {
            return null;
        }
    }

    public async Task<bool> AnyUsersExistAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users.AnyAsync(cancellationToken);
    }

    public async Task<HashSet<string>> GetExistingUserEmailsAsync(
        IEnumerable<string> emails,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(emails);

        var normalizedEmails = emails.Select(EmailNormalizer.Normalize).Distinct().ToList();
        var prefix = PiiAesGcmSymmetricEncryption.EnvelopePrefix;

        var matches = new HashSet<string>();

        foreach (var normalized in normalizedEmails)
        {
            var fingerprint = _emailLookupHasher.HashNormalized(normalized);
            if (fingerprint != null &&
                await _dbContext.Users.AnyAsync(u => u.EmailHash == fingerprint, cancellationToken))
            {
                matches.Add(normalized);
            }
        }

        var plaintextMatches = await _dbContext.Users
            .Where(u => u.EmailHash == null && u.Email != null && normalizedEmails.Contains(u.Email))
            .Select(u => u.Email!)
            .ToListAsync(cancellationToken);
        foreach (var hit in plaintextMatches)
        {
            matches.Add(hit);
        }

        var envelopeOrphans = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Email != null && u.EmailHash == null && u.Email.StartsWith(prefix))
            .ToListAsync(cancellationToken);

        foreach (var row in envelopeOrphans)
        {
            var normalized = TryHydrateNormalizedEmail(row.Email);
            if (normalized != null && normalizedEmails.Contains(normalized))
            {
                matches.Add(normalized);
            }
        }

        return matches;
    }

    public async Task AddUsersAsync(IEnumerable<User> users, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(users);

        var usersList = users.ToList();
        if (usersList.Count == 0)
        {
            return;
        }

        var toInsert = await FilterOutUsersMatchingExistingEmailsAsync(usersList, cancellationToken);
        if (toInsert.Count == 0)
        {
            return;
        }

        var entities = toInsert.Select(MapToEntity).ToList();
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

    public async Task<List<string>> GetUserEmailsByDomainAsync(
        string emailDomain,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(emailDomain);

        var suffix = emailDomain.StartsWith("@", StringComparison.Ordinal)
            ? emailDomain
            : "@" + emailDomain;

        var rows = await _dbContext.Users
            .Where(u => u.Email != null)
            .Select(u => u.Email!)
            .ToListAsync(cancellationToken);

        var results = new List<string>();
        foreach (var ciphertext in rows)
        {
            var normalized = TryHydrateNormalizedEmail(ciphertext);
            if (!string.IsNullOrEmpty(normalized) && normalized.EndsWith(suffix, StringComparison.Ordinal))
            {
                results.Add(normalized);
            }
        }

        return results;
    }

    public async Task RemoveUsersByEmailAsync(IEnumerable<string> emails, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(emails);

        var normalizedEmails = emails.Select(EmailNormalizer.Normalize).Distinct().ToList();

        var usersToRemove = new List<UserEntity>();

        foreach (var normalized in normalizedEmails)
        {
            var fingerprint = _emailLookupHasher.HashNormalized(normalized);
            if (fingerprint != null)
            {
                usersToRemove.AddRange(
                    await _dbContext.Users.Where(u => u.EmailHash == fingerprint).ToListAsync(cancellationToken));
            }
        }

        usersToRemove.AddRange(await _dbContext.Users
            .Where(u =>
                u.EmailHash == null && u.Email != null && normalizedEmails.Contains(u.Email))
            .ToListAsync(cancellationToken));

        var orphans = await _dbContext.Users
            .Where(u =>
                u.Email != null &&
                u.Email.StartsWith(PiiAesGcmSymmetricEncryption.EnvelopePrefix) &&
                u.EmailHash == null)
            .ToListAsync(cancellationToken);
        foreach (var orphan in orphans)
        {
            var normalizedOrphanEmail = TryHydrateNormalizedEmail(orphan.Email);
            if (normalizedOrphanEmail != null && normalizedEmails.Contains(normalizedOrphanEmail))
            {
                usersToRemove.Add(orphan);
            }
        }

        foreach (var user in usersToRemove.DistinctBy(u => u.Id))
        {
            _dbContext.Users.Remove(user);
        }
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

        var normalizedEmails = emails.Select(EmailNormalizer.Normalize).Distinct().ToList();
        var prefix = PiiAesGcmSymmetricEncryption.EnvelopePrefix;

        var matches = new HashSet<string>();

        foreach (var normalized in normalizedEmails)
        {
            var fingerprint = _emailLookupHasher.HashNormalized(normalized);
            if (fingerprint != null && _dbContext.Users.Any(u => u.EmailHash == fingerprint))
            {
                matches.Add(normalized);
            }
        }

        foreach (var row in _dbContext.Users.Where(u =>
                     u.EmailHash == null &&
                     u.Email != null &&
                     normalizedEmails.Contains(u.Email))
                 .Select(u => u.Email!))
        {
            matches.Add(row);
        }

        foreach (var row in _dbContext.Users.Where(u =>
                     u.Email != null &&
                     u.EmailHash == null &&
                     u.Email.StartsWith(prefix))
                 .Select(u => u.Email!))
        {
            var normalized = TryHydrateNormalizedEmail(row);
            if (normalized != null && normalizedEmails.Contains(normalized))
            {
                matches.Add(normalized);
            }
        }

        return matches;
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

        var toInsert = FilterOutUsersMatchingExistingEmails(usersList);
        if (toInsert.Count == 0)
        {
            return;
        }

        var entities = toInsert.Select(MapToEntity).ToList();
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
