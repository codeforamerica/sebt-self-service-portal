using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.UseCases.Household;

/// <summary>
/// Handles retrieval of household data for an authenticated user.
/// Resolves the household identifier from claims (state-configurable), determines PII visibility from IAL level, and fetches household data.
/// </summary>
public class GetHouseholdDataQueryHandler(
    IHouseholdIdentifierResolver resolver,
    IHouseholdRepository repository,
    IPiiVisibilityService piiVisibilityService,
    IIdProofingService idProofingService,
    ILogger<GetHouseholdDataQueryHandler> logger)
    : IQueryHandler<GetHouseholdDataQuery, HouseholdData>
{
    public async Task<Result<HouseholdData>> Handle(GetHouseholdDataQuery query, CancellationToken cancellationToken = default)
    {
        var identifier = await resolver.ResolveAsync(query.User, cancellationToken);

        if (identifier == null)
        {
            logger.LogWarning("Household data request attempted but no household identifier could be resolved from claims");
            return Result<HouseholdData>.Unauthorized("Unable to identify user from token.");
        }

        logger.LogDebug("Household data request received for identifier type {Type}", identifier.Type);

        var userIalLevel = UserIalLevelExtensions.FromClaimsPrincipal(query.User);
        var piiVisibility = piiVisibilityService.GetVisibility(userIalLevel);

        logger.LogInformation(
            "PII visibility for user (IalLevel={IalLevel}): Address={IncludeAddress}, Email={IncludeEmail}, Phone={IncludePhone}",
            userIalLevel,
            piiVisibility.IncludeAddress,
            piiVisibility.IncludeEmail,
            piiVisibility.IncludePhone);

        var householdData = await repository.GetHouseholdByIdentifierAsync(
            identifier,
            piiVisibility,
            userIalLevel,
            cancellationToken);

        if (householdData == null)
        {
            logger.LogWarning("Household data not found for authenticated user");
            return Result<HouseholdData>.PreconditionFailed(PreconditionFailedReason.NotFound, "Household data not found.");
        }

        // SECURITY: Never return household case data when the user has not met
        // the IAL required by their cases. See docs/config/ial/README.md.
        var decision = idProofingService.Evaluate(
            ProtectedResource.Household, ProtectedAction.View,
            userIalLevel, householdData.SummerEbtCases);
        if (!decision.IsAllowed)
        {
            logger.LogInformation(
                "Household data access denied: user IAL {UserIal} is below required {RequiredIal}",
                userIalLevel,
                decision.RequiredLevel);
            return Result<HouseholdData>.Forbidden(
                $"This household requires {decision.RequiredLevel}. Complete identity verification to access this data.",
                new Dictionary<string, object?> { ["requiredIal"] = decision.RequiredLevel.ToString() });
        }

        logger.LogDebug("Household data retrieved successfully for identifier type {Type}", identifier.Type);
        return Result<HouseholdData>.Success(householdData);
    }
}
