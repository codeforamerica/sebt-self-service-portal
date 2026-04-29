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
    ISelfServiceEvaluator selfServiceEvaluator,
    ICardReplacementRequestRepository cardReplacementRepo,
    IIdentifierHasher identifierHasher,
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

        // Mixed-eligibility households: hide co-loaded cases so the user only sees
        // and manages their non-co-loaded cases. Co-loaded-only households still see
        // their cases (they're all the user has), but per-case flags prevent actions.
        // MVP intent confirmed by product: mixed households are not visually supported.
        var nonCoLoaded = householdData.SummerEbtCases.Where(c => !c.IsCoLoaded).ToList();
        if (nonCoLoaded.Count > 0)
        {
            householdData.SummerEbtCases = nonCoLoaded;
            // Realign the household-level issuance type with the filtered view.
            // Downstream consumers (e.g. the address-info page's co-loaded guard)
            // key on BenefitIssuanceType; leaving it as the plugin's upstream value
            // would misroute denials that aren't actually co-loaded.
            householdData.BenefitIssuanceType = BenefitIssuanceType.SummerEbt;
        }

        // Hydrate CardRequestedAt from portal DB — the authoritative source for
        // replacement request timestamps. The frontend uses this to enforce cooldown UI.
        var householdHash = identifierHasher.Hash(identifier.Value);
        if (householdHash != null)
        {
            foreach (var summerEbtCase in householdData.SummerEbtCases)
            {
                if (summerEbtCase.SummerEBTCaseID != null)
                {
                    var caseHash = identifierHasher.Hash(summerEbtCase.SummerEBTCaseID);
                    if (caseHash != null)
                    {
                        summerEbtCase.CardRequestedAt = await cardReplacementRepo
                            .GetMostRecentRequestDateAsync(householdHash, caseHash, cancellationToken);
                    }
                }
            }
        }

        foreach (var summerEbtCase in householdData.SummerEbtCases)
        {
            summerEbtCase.AllowedActions = selfServiceEvaluator.Evaluate(summerEbtCase);
        }

        // Household-level rollup for top-level CTAs evaluates only non-co-loaded cases:
        // co-loaded cases are structurally excluded from self-service regardless of rules.
        householdData.AllowedActions = selfServiceEvaluator.EvaluateHousehold(nonCoLoaded);

        logger.LogDebug("Household data retrieved successfully for identifier type {Type}", identifier.Type);
        return Result<HouseholdData>.Success(householdData);
    }
}
