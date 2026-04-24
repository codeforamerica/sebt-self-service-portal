using Microsoft.EntityFrameworkCore;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Data.Entities;

namespace SEBT.Portal.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ICardReplacementRequestRepository"/>.
/// Queries and writes to the CardReplacementRequests table.
/// </summary>
public class CardReplacementRequestRepository(PortalDbContext dbContext)
    : ICardReplacementRequestRepository
{
    /// <inheritdoc />
    public async Task<bool> HasRecentRequestAsync(
        string householdIdentifierHash,
        string caseIdHash,
        TimeSpan cooldownPeriod,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow - cooldownPeriod;

        return await dbContext.CardReplacementRequests.AnyAsync(
            r => r.HouseholdIdentifierHash == householdIdentifierHash
                 && r.CaseIdHash == caseIdHash
                 && r.RequestedAt > cutoff,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DateTime?> GetMostRecentRequestDateAsync(
        string householdIdentifierHash,
        string caseIdHash,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.CardReplacementRequests
            .Where(r => r.HouseholdIdentifierHash == householdIdentifierHash
                        && r.CaseIdHash == caseIdHash)
            .OrderByDescending(r => r.RequestedAt)
            .Select(r => (DateTime?)r.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task CreateAsync(
        string householdIdentifierHash,
        string caseIdHash,
        Guid requestedByUserId,
        CancellationToken cancellationToken = default)
    {
        var entity = new CardReplacementRequestEntity
        {
            HouseholdIdentifierHash = householdIdentifierHash,
            CaseIdHash = caseIdHash,
            RequestedAt = DateTime.UtcNow,
            RequestedByUserId = requestedByUserId
        };

        dbContext.CardReplacementRequests.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
