using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;

namespace SEBT.Portal.UseCases.IdProofing;

internal enum IdProofingHouseholdLookupOutcome
{
    Found,
    NotFound,
    Failed
}

internal readonly record struct IdProofingHouseholdLookupResult(
    HouseholdData? Household,
    IdProofingHouseholdLookupOutcome Outcome);

/// <summary>
/// Shared warehouse household reads for ID proofing off-boarding cohort checks.
/// </summary>
internal static class IdProofingHouseholdLookup
{
    internal static async Task<IdProofingHouseholdLookupResult> TryGetByEmailForCohortCheckAsync(
        IHouseholdRepository householdRepository,
        ILogger logger,
        User user,
        UserIalLevel warehouseIalForEmailReads,
        Guid portalUserId,
        CancellationToken cancellationToken)
    {
        try
        {
            var household = await householdRepository.GetHouseholdByEmailAsync(
                user.Email!,
                new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false),
                warehouseIalForEmailReads,
                portalUserId,
                includeCardService: false,
                cancellationToken);

            if (household == null)
            {
                logger.LogInformation(
                    "No household found for user {UserId} during off-boarding cohort check",
                    portalUserId);
                return new IdProofingHouseholdLookupResult(null, IdProofingHouseholdLookupOutcome.NotFound);
            }

            return new IdProofingHouseholdLookupResult(household, IdProofingHouseholdLookupOutcome.Found);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(
                ex,
                "Household lookup failed ({ExceptionType}) for user {UserId} during off-boarding cohort check",
                ex.GetType().Name,
                portalUserId);
            return new IdProofingHouseholdLookupResult(null, IdProofingHouseholdLookupOutcome.Failed);
        }
    }

    internal static async Task<string> ResolveOffboardingReasonAsync(
        IHouseholdRepository householdRepository,
        ILogger logger,
        User user,
        UserIalLevel warehouseIalForEmailReads,
        Guid portalUserId,
        string defaultReason,
        CancellationToken cancellationToken)
    {
        var lookup = await TryGetByEmailForCohortCheckAsync(
            householdRepository,
            logger,
            user,
            warehouseIalForEmailReads,
            portalUserId,
            cancellationToken);

        return CoLoadedCohortClassifier.ResolveOffboardingReason(defaultReason, lookup.Household);
    }
}
