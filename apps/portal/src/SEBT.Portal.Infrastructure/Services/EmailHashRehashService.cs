using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Data.Entities;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Rewrites <see cref="UserEntity.EmailHash"/> under the configured <see cref="IEmailLookupHasher"/>
/// secret by decrypting stored email. Used for manual IdentifierHasher secret cutover.
/// Does not rotate other IdentifierHasher-backed columns (SSN, cooldown hashes, and similar).
/// </summary>
public sealed class EmailHashRehashService
{
    private const int BatchSize = 500;

    private readonly PortalDbContext _dbContext;
    private readonly IPiiSymmetricEncryption _crypto;
    private readonly IEmailLookupHasher _emailLookupHasher;
    private readonly ILogger<EmailHashRehashService> _logger;

    public EmailHashRehashService(
        PortalDbContext dbContext,
        IPiiSymmetricEncryption crypto,
        IEmailLookupHasher emailLookupHasher,
        ILogger<EmailHashRehashService> logger)
    {
        _dbContext = dbContext;
        _crypto = crypto;
        _emailLookupHasher = emailLookupHasher;
        _logger = logger;
    }

    public async Task<EmailHashRehashResult> ApplyAsync(
        bool dryRun,
        CancellationToken cancellationToken = default)
    {
        var candidates = await _dbContext.Users
            .Where(u => u.Email != null)
            .OrderBy(u => u.Id)
            .Select(u => new { u.Id, u.Email, u.EmailHash })
            .ToListAsync(cancellationToken);

        var plans = new List<PlannedRehash>(candidates.Count);
        var decryptFailures = 0;

        foreach (var row in candidates)
        {
            var planned = PlanRow(row.Id, row.Email!, row.EmailHash);
            if (planned.Status == RehashRowStatus.DecryptFailed)
            {
                decryptFailures++;
                continue;
            }

            plans.Add(planned);
        }

        var collisionUserIds = FindCollisionUserIds(plans);
        var toUpdate = plans
            .Where(p =>
                p.Status == RehashRowStatus.NeedsUpdate &&
                !collisionUserIds.Contains(p.UserId))
            .ToList();

        var alreadyCurrent = plans.Count(p => p.Status == RehashRowStatus.AlreadyCurrent);
        var skippedCollision = plans.Count(p =>
            p.Status == RehashRowStatus.NeedsUpdate && collisionUserIds.Contains(p.UserId));

        if (!dryRun && toUpdate.Count > 0)
        {
            await PersistUpdatesAsync(toUpdate, cancellationToken);
        }

        var result = new EmailHashRehashResult
        {
            Examined = candidates.Count,
            AlreadyCurrent = alreadyCurrent,
            WouldUpdate = toUpdate.Count,
            Updated = dryRun ? 0 : toUpdate.Count,
            SkippedDecryptFailure = decryptFailures,
            SkippedCollision = skippedCollision,
            DryRun = dryRun,
            CollisionUserIds = collisionUserIds.OrderBy(id => id).ToArray()
        };

        _logger.LogInformation(
            "EmailHash rehash finished. DryRun={DryRun}, Examined={Examined}, AlreadyCurrent={AlreadyCurrent}, " +
            "Updated={Updated}, WouldUpdate={WouldUpdate}, DecryptFailures={DecryptFailures}, Collisions={Collisions}",
            result.DryRun,
            result.Examined,
            result.AlreadyCurrent,
            result.Updated,
            result.WouldUpdate,
            result.SkippedDecryptFailure,
            result.SkippedCollision);

        return result;
    }

    internal static HashSet<Guid> FindCollisionUserIds(IEnumerable<PlannedRehash> plans)
    {
        return plans
            .Where(p => p.NewHash != null && p.Status != RehashRowStatus.DecryptFailed)
            .GroupBy(p => p.NewHash!, StringComparer.Ordinal)
            .Where(g => g.Select(x => x.UserId).Distinct().Count() > 1)
            .SelectMany(g => g.Select(x => x.UserId))
            .ToHashSet();
    }

    internal PlannedRehash PlanRow(Guid userId, string storedEmail, string? currentHash)
    {
        string? plaintext;
        try
        {
            plaintext = _crypto.DecryptOrPassThroughLegacy(storedEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "EmailHash rehash: decrypt failed for user {UserId}; row skipped.",
                userId);
            return new PlannedRehash(userId, null, RehashRowStatus.DecryptFailed);
        }

        var normalized = EmailNormalizer.NormalizeOrNull(plaintext);
        if (normalized == null)
        {
            _logger.LogWarning(
                "EmailHash rehash: empty/unnormalizable email for user {UserId}; row skipped.",
                userId);
            return new PlannedRehash(userId, null, RehashRowStatus.DecryptFailed);
        }

        var newHash = _emailLookupHasher.HashNormalized(normalized);
        if (newHash == null)
        {
            return new PlannedRehash(userId, null, RehashRowStatus.DecryptFailed);
        }

        if (string.Equals(currentHash, newHash, StringComparison.Ordinal))
        {
            return new PlannedRehash(userId, newHash, RehashRowStatus.AlreadyCurrent);
        }

        return new PlannedRehash(userId, newHash, RehashRowStatus.NeedsUpdate);
    }

    private async Task PersistUpdatesAsync(
        IReadOnlyList<PlannedRehash> toUpdate,
        CancellationToken cancellationToken)
    {
        for (var offset = 0; offset < toUpdate.Count; offset += BatchSize)
        {
            var batch = toUpdate.Skip(offset).Take(BatchSize).ToList();
            var ids = batch.Select(b => b.UserId).ToList();
            var entities = await _dbContext.Users
                .Where(u => ids.Contains(u.Id))
                .ToListAsync(cancellationToken);

            var byId = batch.ToDictionary(b => b.UserId);
            var now = DateTime.UtcNow;
            foreach (var entity in entities)
            {
                if (!byId.TryGetValue(entity.Id, out var plan) || plan.NewHash == null)
                {
                    continue;
                }

                entity.EmailHash = plan.NewHash;
                entity.UpdatedAt = now;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();

            _logger.LogInformation(
                "EmailHash rehash: persisted batch of {Count} Users rows.",
                entities.Count);
        }
    }

    internal enum RehashRowStatus
    {
        NeedsUpdate,
        AlreadyCurrent,
        DecryptFailed
    }

    internal sealed record PlannedRehash(Guid UserId, string? NewHash, RehashRowStatus Status);
}

public sealed class EmailHashRehashResult
{
    public required int Examined { get; init; }
    public required int AlreadyCurrent { get; init; }
    public required int WouldUpdate { get; init; }
    public required int Updated { get; init; }
    public required int SkippedDecryptFailure { get; init; }
    public required int SkippedCollision { get; init; }
    public required bool DryRun { get; init; }
    public required IReadOnlyList<Guid> CollisionUserIds { get; init; }
}
