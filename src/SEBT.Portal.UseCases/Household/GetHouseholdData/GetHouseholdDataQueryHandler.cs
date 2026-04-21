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
    IIdProofingRequirementsService idProofingRequirementsService,
    IMinimumIalService minimumIalService,
    ISelfServiceEvaluator selfServiceEvaluator,
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
        var piiVisibility = idProofingRequirementsService.GetPiiVisibility(userIalLevel);

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

        var minimumIal = minimumIalService.GetMinimumIal(householdData.SummerEbtCases);
        if (userIalLevel < minimumIal)
        {
            // SECURITY: Never return household case data when the user has not met
            // the minimum IAL required by their cases. See docs/tdd/minimum-ial-determination.md.
            logger.LogInformation(
                "Household data access denied: user IAL {UserIal} is below minimum {MinimumIal}",
                userIalLevel,
                minimumIal);
            return Result<HouseholdData>.Forbidden(
                $"This household requires {minimumIal}. Complete identity verification to access this data.",
                new Dictionary<string, object?> { ["requiredIal"] = minimumIal.ToString() });
        }

        // Co-loaded cases stay in the response so the client can render static-link
        // treatment for them. Per-case AllowedActions flags signal which cases can
        // participate in self-service actions; command handlers enforce server-side.
        foreach (var summerEbtCase in householdData.SummerEbtCases)
        {
            summerEbtCase.AllowedActions = selfServiceEvaluator.Evaluate(summerEbtCase);
        }

        // Household-level rollup for top-level CTAs evaluates only non-co-loaded cases:
        // co-loaded cases are structurally excluded from self-service regardless of rules.
        var nonCoLoaded = householdData.SummerEbtCases.Where(c => !c.IsCoLoaded).ToList();
        householdData.AllowedActions = selfServiceEvaluator.EvaluateHousehold(nonCoLoaded);

        logger.LogDebug("Household data retrieved successfully for identifier type {Type}", identifier.Type);
        return Result<HouseholdData>.Success(householdData);
    }
}
