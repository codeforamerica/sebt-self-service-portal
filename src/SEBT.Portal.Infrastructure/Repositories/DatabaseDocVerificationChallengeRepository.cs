using Microsoft.EntityFrameworkCore;
using SEBT.Portal.Core.Exceptions;
using SEBT.Portal.Core.Models.DocVerification;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Data.Entities;

namespace SEBT.Portal.Infrastructure.Repositories;

/// <summary>
/// Database-backed implementation of <see cref="IDocVerificationChallengeRepository"/> using Entity Framework Core.
/// All read operations are scoped by userId to enforce ownership.
/// </summary>
public class DatabaseDocVerificationChallengeRepository(PortalDbContext dbContext)
    : IDocVerificationChallengeRepository
{
    private const string OneActivePerUserIndex = "IX_DocVerificationChallenges_OneActivePerUser";

    public async Task<DocVerificationChallenge?> GetByPublicIdAsync(
        Guid publicId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.DocVerificationChallenges
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.PublicId == publicId && c.UserId == userId,
                cancellationToken);

        return entity == null ? null : MapToDomainModel(entity);
    }

    public async Task<DocVerificationChallenge?> GetActiveByUserIdAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        // Non-terminal statuses: Created (0) and Pending (1)
        var entity = await dbContext.DocVerificationChallenges
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.UserId == userId
                     && (c.Status == (int)DocVerificationStatus.Created
                         || c.Status == (int)DocVerificationStatus.Pending)
                     && (c.ExpiresAt == null || c.ExpiresAt > DateTime.UtcNow),
                cancellationToken);

        return entity == null ? null : MapToDomainModel(entity);
    }

    public async Task<DocVerificationChallenge?> GetBySocureReferenceIdAsync(
        string referenceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(referenceId))
        {
            return null;
        }

        var entity = await dbContext.DocVerificationChallenges
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.SocureReferenceId == referenceId, cancellationToken);

        return entity == null ? null : MapToDomainModel(entity);
    }

    public async Task<DocVerificationChallenge?> GetByEvalIdAsync(
        string evalId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(evalId))
        {
            return null;
        }

        var entity = await dbContext.DocVerificationChallenges
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.EvalId == evalId, cancellationToken);

        return entity == null ? null : MapToDomainModel(entity);
    }

    public async Task CreateAsync(
        DocVerificationChallenge challenge,
        CancellationToken cancellationToken = default)
    {
        if (challenge == null)
        {
            throw new ArgumentNullException(nameof(challenge));
        }

        var now = DateTime.UtcNow;

        // Single transaction: bulk expire + insert commit together. Without this, a failure after
        // ExecuteUpdateAsync could leave stale rows expired with no new challenge (recoverable on
        // retry but inconsistent until then).
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Bulk expire via SQL: does not use RowVersion optimistic concurrency or
            // DocVerificationChallenge.TransitionTo.  The predicate limits rows to stale
            // Created/Pending; revisit if concurrent writers race here.
            await dbContext.DocVerificationChallenges
                .Where(c => c.UserId == challenge.UserId
                    && (c.Status == (int)DocVerificationStatus.Created
                        || c.Status == (int)DocVerificationStatus.Pending)
                    && c.ExpiresAt != null
                    && c.ExpiresAt <= now)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(e => e.Status, (int)DocVerificationStatus.Expired)
                        .SetProperty(e => e.UpdatedAt, now),
                    cancellationToken);

            var entity = MapToEntity(challenge);
            dbContext.DocVerificationChallenges.Add(entity);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains(OneActivePerUserIndex) == true)
            {
                throw new DuplicateRecordException(
                    $"An active DocVerificationChallenge already exists for user {challenge.UserId}.", ex);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task UpdateAsync(
        DocVerificationChallenge challenge,
        CancellationToken cancellationToken = default)
    {
        if (challenge == null)
        {
            throw new ArgumentNullException(nameof(challenge));
        }

        var entity = await dbContext.DocVerificationChallenges
            .FirstOrDefaultAsync(c => c.Id == challenge.Id, cancellationToken);

        if (entity == null)
        {
            throw new InvalidOperationException(
                $"DocVerificationChallenge with Id {challenge.Id} not found.");
        }

        entity.Status = (int)challenge.Status;
        entity.SocureReferenceId = challenge.SocureReferenceId;
        entity.EvalId = challenge.EvalId;
        entity.SocureEventId = challenge.SocureEventId;
        entity.DocvTransactionToken = challenge.DocvTransactionToken;
        entity.DocvUrl = challenge.DocvUrl;
        entity.OffboardingReason = challenge.OffboardingReason;
        entity.AllowIdRetry = challenge.AllowIdRetry;
        entity.ExpiresAt = challenge.ExpiresAt;
        entity.ProofingDateOfBirth = challenge.ProofingDateOfBirth;
        entity.ProofingIdType = challenge.ProofingIdType;
        entity.ProofingIdValue = challenge.ProofingIdValue;
        entity.DocvTokenIssuedAt = challenge.DocvTokenIssuedAt;
        entity.UpdatedAt = DateTime.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(
                $"DocVerificationChallenge {challenge.Id} was modified by another writer.", ex);
        }
    }

    private static DocVerificationChallenge MapToDomainModel(DocVerificationChallengeEntity entity)
    {
        return DocVerificationChallenge.Reconstitute(
            id: entity.Id,
            publicId: entity.PublicId,
            userId: entity.UserId,
            status: (DocVerificationStatus)entity.Status,
            socureReferenceId: entity.SocureReferenceId,
            evalId: entity.EvalId,
            socureEventId: entity.SocureEventId,
            docvTransactionToken: entity.DocvTransactionToken,
            docvUrl: entity.DocvUrl,
            offboardingReason: entity.OffboardingReason,
            allowIdRetry: entity.AllowIdRetry,
            createdAt: entity.CreatedAt,
            updatedAt: entity.UpdatedAt,
            expiresAt: entity.ExpiresAt,
            proofingDateOfBirth: entity.ProofingDateOfBirth,
            proofingIdType: entity.ProofingIdType,
            proofingIdValue: entity.ProofingIdValue,
            docvTokenIssuedAt: entity.DocvTokenIssuedAt);
    }

    private static DocVerificationChallengeEntity MapToEntity(DocVerificationChallenge challenge)
    {
        return new DocVerificationChallengeEntity
        {
            Id = challenge.Id,
            PublicId = challenge.PublicId,
            UserId = challenge.UserId,
            Status = (int)challenge.Status,
            SocureReferenceId = challenge.SocureReferenceId,
            EvalId = challenge.EvalId,
            SocureEventId = challenge.SocureEventId,
            DocvTransactionToken = challenge.DocvTransactionToken,
            DocvUrl = challenge.DocvUrl,
            OffboardingReason = challenge.OffboardingReason,
            AllowIdRetry = challenge.AllowIdRetry,
            CreatedAt = challenge.CreatedAt,
            UpdatedAt = challenge.UpdatedAt,
            ExpiresAt = challenge.ExpiresAt,
            ProofingDateOfBirth = challenge.ProofingDateOfBirth,
            ProofingIdType = challenge.ProofingIdType,
            ProofingIdValue = challenge.ProofingIdValue,
            DocvTokenIssuedAt = challenge.DocvTokenIssuedAt
        };
    }
}
