using Microsoft.EntityFrameworkCore;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Data.Entities;

namespace SEBT.Portal.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IUserCaseRepository"/>.
/// </summary>
public class DatabaseUserCaseRepository(
    PortalDbContext dbContext,
    IIdentifierHasher identifierHasher,
    IPiiSymmetricEncryption piiEncryption,
    TimeProvider timeProvider) : IUserCaseRepository
{
    private static readonly TimeSpan LastSeenRefreshInterval = TimeSpan.FromHours(1);

    /// <inheritdoc />
    public async Task UpsertDisplayedCasesAsync(
        Guid userId,
        IReadOnlyList<UserCaseIdentifiers> cases,
        CancellationToken cancellationToken = default)
    {
        var identities = ToCaseIdentities(cases);
        if (identities.Count == 0)
        {
            return;
        }

        try
        {
            await ApplyUpsertAsync(userId, identities, cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueKeyViolation(ex))
        {
            // A concurrent request for the same user inserted one of these cases between our
            // read and save. Retry once so the rows it wrote are updated instead of re-inserted.
            await ApplyUpsertAsync(userId, identities, cancellationToken);
        }
    }

    private List<CaseIdentity> ToCaseIdentities(IReadOnlyList<UserCaseIdentifiers> cases)
    {
        var identities = new List<CaseIdentity>(cases.Count);
        foreach (var displayed in cases)
        {
            var caseIdHash = identifierHasher.Hash(displayed.CaseId);
            if (caseIdHash is null)
            {
                continue;
            }

            identities.Add(new CaseIdentity(
                displayed,
                caseIdHash,
                identifierHasher.Hash(displayed.ApplicationId) ?? string.Empty,
                identifierHasher.Hash(displayed.ApplicationStudentId) ?? string.Empty));
        }

        return identities;
    }

    private async Task ApplyUpsertAsync(
        Guid userId,
        IReadOnlyList<CaseIdentity> identities,
        CancellationToken cancellationToken)
    {
        var caseIdHashes = identities.Select(i => i.CaseIdHash).Distinct().ToList();
        var existing = await dbContext.UserCases
            .AsNoTracking()
            .Where(e => e.UserId == userId && caseIdHashes.Contains(e.CaseIdHash))
            .Select(e => new ExistingRow(
                e.Id, e.CaseIdHash, e.ApplicationIdHash, e.ApplicationStudentIdHash, e.LastSeenAt))
            .ToListAsync(cancellationToken);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var staleCutoff = now - LastSeenRefreshInterval;
        var staleRowIds = new List<Guid>();
        var newRows = new List<UserCaseEntity>();
        var seenIdentities = new HashSet<(string, string, string)>();

        foreach (var identity in identities)
        {
            if (!seenIdentities.Add((identity.CaseIdHash, identity.ApplicationIdHash, identity.ApplicationStudentIdHash)))
            {
                continue;
            }

            var row = existing.FirstOrDefault(e =>
                e.CaseIdHash == identity.CaseIdHash
                && e.ApplicationIdHash == identity.ApplicationIdHash
                && e.ApplicationStudentIdHash == identity.ApplicationStudentIdHash);

            if (row is null)
            {
                newRows.Add(ToNewEntity(userId, identity, now));
            }
            else if (row.LastSeenAt < staleCutoff)
            {
                staleRowIds.Add(row.Id);
            }
        }

        if (staleRowIds.Count > 0)
        {
            await dbContext.UserCases
                .Where(e => staleRowIds.Contains(e.Id) && e.LastSeenAt < staleCutoff)
                .ExecuteUpdateAsync(setters => setters.SetProperty(e => e.LastSeenAt, now), cancellationToken);
        }

        if (newRows.Count > 0)
        {
            await InsertWithoutFlushingOtherChangesAsync(newRows, cancellationToken);
        }
    }

    // SaveChanges commits everything tracked on the shared request-scoped context, so refuse
    // rather than silently persist another component's unsaved changes.
    private async Task InsertWithoutFlushingOtherChangesAsync(
        List<UserCaseEntity> newRows,
        CancellationToken cancellationToken)
    {
        if (dbContext.ChangeTracker.HasChanges())
        {
            throw new InvalidOperationException(
                "Cannot record displayed cases while the database context has other unsaved changes.");
        }

        dbContext.UserCases.AddRange(newRows);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Callers may swallow this failure, so don't leave rows queued for the next save in this scope.
            DetachRows(newRows);
            throw;
        }
    }

    private UserCaseEntity ToNewEntity(Guid userId, CaseIdentity identity, DateTime now) => new()
    {
        UserId = userId,
        CaseId = piiEncryption.Encrypt(identity.Source.CaseId)!,
        CaseIdHash = identity.CaseIdHash,
        ApplicationId = identity.ApplicationIdHash.Length == 0
            ? null
            : piiEncryption.Encrypt(identity.Source.ApplicationId),
        ApplicationIdHash = identity.ApplicationIdHash,
        ApplicationStudentId = identity.ApplicationStudentIdHash.Length == 0
            ? null
            : piiEncryption.Encrypt(identity.Source.ApplicationStudentId),
        ApplicationStudentIdHash = identity.ApplicationStudentIdHash,
        CreatedAt = now,
        LastSeenAt = now
    };

    private static bool IsUniqueKeyViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("IX_UserCases_User_CaseIdentity") == true
        || ex.InnerException?.Message.Contains("duplicate key") == true;

    private void DetachRows(IEnumerable<UserCaseEntity> rows)
    {
        foreach (var row in rows)
        {
            dbContext.Entry(row).State = EntityState.Detached;
        }
    }

    private sealed record ExistingRow(
        Guid Id,
        string CaseIdHash,
        string ApplicationIdHash,
        string ApplicationStudentIdHash,
        DateTime LastSeenAt);

    private sealed record CaseIdentity(
        UserCaseIdentifiers Source,
        string CaseIdHash,
        string ApplicationIdHash,
        string ApplicationStudentIdHash);
}
