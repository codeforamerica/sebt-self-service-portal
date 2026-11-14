using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Models.UserActions;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Data.Entities;

namespace SEBT.Portal.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IUserActionRepository"/>.
/// </summary>
public class DatabaseUserActionRepository : IUserActionRepository
{
    private readonly PortalDbContext _dbContext;
    private readonly IIdentifierHasher _identifierHasher;
    private readonly IPiiSymmetricEncryption _piiEncryption;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Creates a repository that stores user actions with encrypted identifiers and HMAC lookup hashes.
    /// </summary>
    public DatabaseUserActionRepository(
        PortalDbContext dbContext,
        IIdentifierHasher identifierHasher,
        IPiiSymmetricEncryption piiEncryption,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _identifierHasher = identifierHasher;
        _piiEncryption = piiEncryption;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task RecordAsync(
        Guid userId,
        UserActionType action,
        UserActionOutcome outcome,
        IReadOnlyList<UserCaseIdentifiers> cases,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var activity = Activity.Current;
        var rows = new List<UserActionEntity>(cases.Count);

        foreach (var userCase in cases)
        {
            var caseIdHash = _identifierHasher.Hash(userCase.CaseId);
            if (caseIdHash is null)
            {
                continue;
            }

            var applicationIdHash = _identifierHasher.Hash(userCase.ApplicationId) ?? string.Empty;
            var applicationStudentIdHash = _identifierHasher.Hash(userCase.ApplicationStudentId) ?? string.Empty;

            rows.Add(new UserActionEntity
            {
                UserId = userId,
                Action = action,
                Outcome = outcome,
                CaseId = _piiEncryption.Encrypt(userCase.CaseId)
                    ?? throw new InvalidOperationException("Encrypting a case ID that hashed successfully returned null."),
                CaseIdHash = caseIdHash,
                ApplicationId = applicationIdHash.Length == 0
                    ? null
                    : _piiEncryption.Encrypt(userCase.ApplicationId),
                ApplicationIdHash = applicationIdHash,
                ApplicationStudentId = applicationStudentIdHash.Length == 0
                    ? null
                    : _piiEncryption.Encrypt(userCase.ApplicationStudentId),
                ApplicationStudentIdHash = applicationStudentIdHash,
                SpanId = activity?.SpanId.ToHexString(),
                TraceId = activity?.TraceId.ToHexString(),
                CreatedAt = now
            });
        }

        if (rows.Count == 0)
        {
            return;
        }

        await InsertWithoutFlushingOtherChangesAsync(rows, cancellationToken);
    }

    // SaveChanges commits everything tracked on the shared request-scoped context, so refuse
    // rather than silently persist another component's unsaved changes.
    private async Task InsertWithoutFlushingOtherChangesAsync(
        List<UserActionEntity> rows,
        CancellationToken cancellationToken)
    {
        if (_dbContext.ChangeTracker.HasChanges())
        {
            throw new InvalidOperationException(
                "Cannot record user actions while the database context has other unsaved changes.");
        }

        _dbContext.UserActions.AddRange(rows);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Callers swallow this failure, so don't leave rows queued for the next save in this scope.
            foreach (var row in rows)
            {
                _dbContext.Entry(row).State = EntityState.Detached;
            }

            throw;
        }
    }
}
