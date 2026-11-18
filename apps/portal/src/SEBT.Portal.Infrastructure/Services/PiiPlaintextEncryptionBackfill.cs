using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Data.Entities;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Migrates plaintext-at-rest (or ciphertext lacking the email MAC column) into authenticated AES-GCM envelopes.
/// Executes after EF migrations — idempotent skips rows that already have coherent ciphertext.
/// </summary>
public sealed class PiiPlaintextEncryptionBackfill
{
    private readonly PortalDbContext _dbContext;
    private readonly IPiiSymmetricEncryption _crypto;
    private readonly IEmailLookupHasher _emailLookupHasher;
    private readonly ILogger<PiiPlaintextEncryptionBackfill> _logger;

    public PiiPlaintextEncryptionBackfill(
        PortalDbContext dbContext,
        IPiiSymmetricEncryption crypto,
        IEmailLookupHasher emailLookupHasher,
        ILogger<PiiPlaintextEncryptionBackfill> logger)
    {
        _dbContext = dbContext;
        _crypto = crypto;
        _emailLookupHasher = emailLookupHasher;
        _logger = logger;
    }

    public async Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        await MigrateUsersLoopAsync(cancellationToken);
        await MigrateDocVerificationLoopAsync(cancellationToken);
        await MigrateUserCasesLoopAsync(cancellationToken);
        await MigrateUserActionsLoopAsync(cancellationToken);
    }

    private async Task MigrateUsersLoopAsync(CancellationToken cancellationToken)
    {
        int migrated;
        do
        {
            migrated = await MigrateOneUsersBatchAsync(cancellationToken);
        }
        while (migrated > 0);
    }

    private async Task<int> MigrateOneUsersBatchAsync(CancellationToken cancellationToken)
    {
        var prefix = PiiAesGcmSymmetricEncryption.EnvelopePrefix;
        var batch = await _dbContext.Users
            .Where(user =>
                user.Email != null && user.EmailHash == null
                || user.Phone != null && !user.Phone.StartsWith(prefix)
                || user.SnapId != null && !user.SnapId.StartsWith(prefix)
                || user.TanfId != null && !user.TanfId.StartsWith(prefix)
                || user.DateOfBirth != null && !user.DateOfBirth.StartsWith(prefix))
            .OrderBy(u => u.UpdatedAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        foreach (var user in batch)
        {
            BackfillEmail(prefix, user);
            BackfillScalar(prefix, () => user.Phone, value => user.Phone = value);
            BackfillScalar(prefix, () => user.SnapId, value => user.SnapId = value);
            BackfillScalar(prefix, () => user.TanfId, value => user.TanfId = value);
            BackfillScalar(prefix, () => user.DateOfBirth, value => user.DateOfBirth = value);
            user.UpdatedAt = DateTime.UtcNow;
        }

        if (batch.Count == 0)
        {
            return 0;
        }

        await SaveChangesAndDetachBatchAsync(cancellationToken);

        _logger.LogInformation("PII ciphertext backfill: encrypted {Rows} Users row batch.", batch.Count);
        return batch.Count;
    }

    private void BackfillEmail(string envelopePrefix, UserEntity entity)
    {
        if (entity.Email == null)
        {
            entity.EmailHash = null;
            return;
        }

        if (entity.EmailHash != null)
        {
            return;
        }

        var plaintextCandidate = (_crypto.DecryptOrPassThroughLegacy(entity.Email) ?? string.Empty).Trim();
        var normalized = EmailNormalizer.NormalizeOrNull(plaintextCandidate);
        if (normalized == null)
        {
            return;
        }

        entity.Email = _crypto.Encrypt(normalized);
        entity.EmailHash = _emailLookupHasher.HashNormalized(normalized);
    }

    private void BackfillScalar(string prefix, Func<string?> read, Action<string?> write)
    {
        var current = read();
        if (string.IsNullOrEmpty(current))
        {
            return;
        }

        if (current.StartsWith(prefix, StringComparison.Ordinal))
        {
            return;
        }

        var plaintext = (_crypto.DecryptOrPassThroughLegacy(current) ?? string.Empty).Trim();
        write(string.IsNullOrEmpty(plaintext) ? null : _crypto.Encrypt(plaintext));
    }

    /// <summary>
    /// Persists then clears the change tracker. Not redundant with <see cref="DbContext.SaveChangesAsync(System.Threading.CancellationToken)"/> —
    /// saved entities stay tracked as <see cref="EntityState.Unchanged"/>; without <see cref="ChangeTracker.Clear"/>,
    /// each batch would retain prior rows until backfill completes (unbounded tracker growth).
    /// </summary>
    private async Task SaveChangesAndDetachBatchAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
        _dbContext.ChangeTracker.Clear();
    }

    private async Task MigrateDocVerificationLoopAsync(CancellationToken cancellationToken)
    {
        int migrated;
        do
        {
            migrated = await MigrateOneDocVerificationBatchAsync(cancellationToken);
        }
        while (migrated > 0);
    }

    private async Task<int> MigrateOneDocVerificationBatchAsync(CancellationToken cancellationToken)
    {
        var prefix = PiiAesGcmSymmetricEncryption.EnvelopePrefix;
        var batch = await _dbContext.DocVerificationChallenges
            .Where(row =>
                row.ProofingDateOfBirth != null && !row.ProofingDateOfBirth.StartsWith(prefix)
                || row.ProofingIdType != null && !row.ProofingIdType.StartsWith(prefix)
                || row.ProofingIdValue != null && !row.ProofingIdValue.StartsWith(prefix))
            .OrderBy(row => row.UpdatedAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        foreach (var challenge in batch)
        {
            BackfillScalar(prefix, () => challenge.ProofingDateOfBirth, value => challenge.ProofingDateOfBirth = value);
            BackfillScalar(prefix, () => challenge.ProofingIdType, value => challenge.ProofingIdType = value);
            BackfillScalar(prefix, () => challenge.ProofingIdValue, value => challenge.ProofingIdValue = value);
            challenge.UpdatedAt = DateTime.UtcNow;
        }

        if (batch.Count == 0)
        {
            return 0;
        }

        await SaveChangesAndDetachBatchAsync(cancellationToken);

        _logger.LogInformation(
            "PII ciphertext backfill: encrypted {Rows} DocVerificationChallenge row batch.",
            batch.Count);

        return batch.Count;
    }

    private async Task MigrateUserCasesLoopAsync(CancellationToken cancellationToken)
    {
        int migrated;
        do
        {
            migrated = await MigrateOneUserCasesBatchAsync(cancellationToken);
        }
        while (migrated > 0);
    }

    private async Task<int> MigrateOneUserCasesBatchAsync(CancellationToken cancellationToken)
    {
        var prefix = PiiAesGcmSymmetricEncryption.EnvelopePrefix;
        var batch = await _dbContext.UserCases
            .Where(row =>
                row.CaseId != "" && !row.CaseId.StartsWith(prefix)
                || row.ApplicationId != null && !row.ApplicationId.StartsWith(prefix)
                || row.ApplicationStudentId != null && !row.ApplicationStudentId.StartsWith(prefix))
            .OrderBy(row => row.Id)
            .Take(500)
            .ToListAsync(cancellationToken);

        // LastSeenAt records when the user was shown the case, so encrypting doesn't touch it.
        foreach (var userCase in batch)
        {
            BackfillScalar(prefix, () => userCase.CaseId, value => userCase.CaseId = value
                ?? throw new InvalidOperationException($"UserCases row {userCase.Id} has a blank CaseId."));
            BackfillScalar(prefix, () => userCase.ApplicationId, value => userCase.ApplicationId = value);
            BackfillScalar(prefix, () => userCase.ApplicationStudentId, value => userCase.ApplicationStudentId = value);
        }

        if (batch.Count == 0)
        {
            return 0;
        }

        await SaveChangesAndDetachBatchAsync(cancellationToken);

        _logger.LogInformation("PII ciphertext backfill: encrypted {Rows} UserCases row batch.", batch.Count);
        return batch.Count;
    }

    private async Task MigrateUserActionsLoopAsync(CancellationToken cancellationToken)
    {
        int migrated;
        do
        {
            migrated = await MigrateOneUserActionsBatchAsync(cancellationToken);
        }
        while (migrated > 0);
    }

    private async Task<int> MigrateOneUserActionsBatchAsync(CancellationToken cancellationToken)
    {
        var prefix = PiiAesGcmSymmetricEncryption.EnvelopePrefix;
        var batch = await _dbContext.UserActions
            .Where(row =>
                row.CaseId != "" && !row.CaseId.StartsWith(prefix)
                || row.ApplicationId != null && !row.ApplicationId.StartsWith(prefix)
                || row.ApplicationStudentId != null && !row.ApplicationStudentId.StartsWith(prefix))
            .OrderBy(row => row.Id)
            .Take(500)
            .ToListAsync(cancellationToken);

        foreach (var userAction in batch)
        {
            BackfillScalar(prefix, () => userAction.CaseId, value => userAction.CaseId = value
                ?? throw new InvalidOperationException($"UserActions row {userAction.Id} has a blank CaseId."));
            BackfillScalar(prefix, () => userAction.ApplicationId, value => userAction.ApplicationId = value);
            BackfillScalar(prefix, () => userAction.ApplicationStudentId, value => userAction.ApplicationStudentId = value);
        }

        if (batch.Count == 0)
        {
            return 0;
        }

        await SaveChangesAndDetachBatchAsync(cancellationToken);

        _logger.LogInformation("PII ciphertext backfill: encrypted {Rows} UserActions row batch.", batch.Count);
        return batch.Count;
    }
}
