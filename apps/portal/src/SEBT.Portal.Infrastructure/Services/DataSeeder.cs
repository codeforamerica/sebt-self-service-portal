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
        var distinct = CollectDistinctNormalizableEmails(usersList);
        if (distinct.Count == 0)
        {
            return usersList;
        }

        var existing = GetExistingUserEmails(distinct);
        return FilterUsersWithoutMatchingEmails(usersList, existing);
    }

    private async Task<List<User>> FilterOutUsersMatchingExistingEmailsAsync(
        List<User> usersList,
        CancellationToken cancellationToken)
    {
        var distinct = CollectDistinctNormalizableEmails(usersList);
        if (distinct.Count == 0)
        {
            return usersList;
        }

        var existing = await GetExistingUserEmailsAsync(distinct, cancellationToken);
        return FilterUsersWithoutMatchingEmails(usersList, existing);
    }

    private static List<string> CollectDistinctNormalizableEmails(List<User> usersList) =>
        usersList
            .Select(u => EmailNormalizer.NormalizeOrNull(u.Email))
            .Where(normalized => normalized != null)
            .Distinct(StringComparer.Ordinal)
            .Select(normalized => normalized!)
            .ToList();

    private static List<User> FilterUsersWithoutMatchingEmails(
        List<User> usersList,
        IReadOnlySet<string> existingNormalizedEmails) =>
        usersList
            .Where(user =>
            {
                var normalized = EmailNormalizer.NormalizeOrNull(user.Email);
                return normalized == null || !existingNormalizedEmails.Contains(normalized);
            })
            .ToList();

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

        var normalizedEmails = emails.Select(EmailNormalizer.Normalize).Distinct(StringComparer.Ordinal).ToList();
        var matches = new HashSet<string>(StringComparer.Ordinal);

        await AddHashMatchesAsync(normalizedEmails, matches, cancellationToken);
        await AddPlaintextAndOrphanMatchesAsync(normalizedEmails, matches, cancellationToken);

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

        var normalizedEmails = emails.Select(EmailNormalizer.Normalize).Distinct(StringComparer.Ordinal).ToList();
        var matches = new HashSet<string>(StringComparer.Ordinal);

        AddHashMatchesSync(normalizedEmails, matches);
        AddPlaintextAndOrphanMatchesSync(normalizedEmails, matches);

        return matches;
    }

    private List<(string Normalized, string Fingerprint)> BuildFingerprintPairs(List<string> normalizedEmails) =>
        normalizedEmails
            .Select(normalized => (Normalized: normalized, Fingerprint: _emailLookupHasher.HashNormalized(normalized)))
            .Where(pair => pair.Fingerprint != null)
            .Select(pair => (pair.Normalized, pair.Fingerprint!))
            .ToList();

    private async Task AddHashMatchesAsync(
        List<string> normalizedEmails,
        HashSet<string> matches,
        CancellationToken cancellationToken)
    {
        var pairs = BuildFingerprintPairs(normalizedEmails);
        if (pairs.Count == 0)
        {
            return;
        }

        var fingerprints = pairs.Select(pair => pair.Fingerprint).Distinct(StringComparer.Ordinal).ToList();
        var foundHashes = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.EmailHash != null && fingerprints.Contains(user.EmailHash))
            .Select(user => user.EmailHash!)
            .ToListAsync(cancellationToken);

        var foundHashSet = foundHashes.ToHashSet(StringComparer.Ordinal);
        foreach (var (normalized, fingerprint) in pairs)
        {
            if (foundHashSet.Contains(fingerprint))
            {
                matches.Add(normalized);
            }
        }
    }

    private void AddHashMatchesSync(List<string> normalizedEmails, HashSet<string> matches)
    {
        var pairs = BuildFingerprintPairs(normalizedEmails);
        if (pairs.Count == 0)
        {
            return;
        }

        var fingerprints = pairs.Select(pair => pair.Fingerprint).Distinct(StringComparer.Ordinal).ToList();
        var foundHashSet = _dbContext.Users
            .AsNoTracking()
            .Where(user => user.EmailHash != null && fingerprints.Contains(user.EmailHash))
            .Select(user => user.EmailHash!)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var (normalized, fingerprint) in pairs)
        {
            if (foundHashSet.Contains(fingerprint))
            {
                matches.Add(normalized);
            }
        }
    }

    private async Task AddPlaintextAndOrphanMatchesAsync(
        List<string> normalizedEmails,
        HashSet<string> matches,
        CancellationToken cancellationToken)
    {
        var prefix = PiiAesGcmSymmetricEncryption.EnvelopePrefix;

        var plaintextMatches = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.EmailHash == null && user.Email != null && normalizedEmails.Contains(user.Email))
            .Select(user => user.Email!)
            .ToListAsync(cancellationToken);
        foreach (var hit in plaintextMatches)
        {
            matches.Add(hit);
        }

        var envelopeOrphans = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.Email != null && user.EmailHash == null && user.Email.StartsWith(prefix))
            .ToListAsync(cancellationToken);

        AddEnvelopeOrphanMatches(normalizedEmails, matches, envelopeOrphans);
    }

    private void AddPlaintextAndOrphanMatchesSync(List<string> normalizedEmails, HashSet<string> matches)
    {
        var prefix = PiiAesGcmSymmetricEncryption.EnvelopePrefix;

        foreach (var plaintext in _dbContext.Users
                     .AsNoTracking()
                     .Where(user =>
                         user.EmailHash == null &&
                         user.Email != null &&
                         normalizedEmails.Contains(user.Email))
                     .Select(user => user.Email!))
        {
            matches.Add(plaintext);
        }

        var envelopeOrphans = _dbContext.Users
            .AsNoTracking()
            .Where(user => user.Email != null && user.EmailHash == null && user.Email.StartsWith(prefix))
            .ToList();

        AddEnvelopeOrphanMatches(normalizedEmails, matches, envelopeOrphans);
    }

    private void AddEnvelopeOrphanMatches(
        List<string> normalizedEmails,
        HashSet<string> matches,
        IEnumerable<UserEntity> envelopeOrphans)
    {
        foreach (var row in envelopeOrphans)
        {
            var normalized = TryHydrateNormalizedEmail(row.Email);
            if (normalized != null && normalizedEmails.Contains(normalized))
            {
                matches.Add(normalized);
            }
        }
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
