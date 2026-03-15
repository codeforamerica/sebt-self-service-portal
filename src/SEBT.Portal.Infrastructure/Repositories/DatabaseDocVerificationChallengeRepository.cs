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

        var entity = MapToEntity(challenge);
        dbContext.DocVerificationChallenges.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
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
            expiresAt: entity.ExpiresAt);
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
            ExpiresAt = challenge.ExpiresAt
        };
    }
}
