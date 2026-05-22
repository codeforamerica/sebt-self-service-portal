using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;

namespace SEBT.Portal.UseCases.IdProofing;

/// <summary>
/// Shared warehouse household reads for ID proofing off-boarding cohort checks.
/// </summary>
internal static class IdProofingHouseholdLookup
{
    internal static async Task<HouseholdData?> TryGetByEmailForCohortCheckAsync(
        IHouseholdRepository householdRepository,
        ILogger logger,
        User user,
        UserIalLevel warehouseIalForEmailReads,
        Guid portalUserId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await householdRepository.GetHouseholdByEmailAsync(
                user.Email!,
                new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false),
                warehouseIalForEmailReads,
                portalUserId,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Household lookup failed ({ExceptionType}) for user {UserId} during off-boarding cohort check",
                ex.GetType().Name,
                portalUserId);
            return null;
        }
    }
}
