using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;
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
    IUserRepository userRepository,
    IPiiVisibilityService piiVisibilityService,
    IIdProofingService idProofingService,
    ISelfServiceEvaluator selfServiceEvaluator,
    ICardReplacementRequestRepository cardReplacementRepo,
    IIdentifierHasher identifierHasher,
    CoLoadedCohortFilterSettings coLoadedCohortFilter,
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

        if (householdData == null
            && identifier.Type == PreferredHouseholdIdType.Email)
        {
            var userId = query.User.GetUserId();
            if (userId != null)
            {
                var user = await userRepository.GetUserByIdAsync(userId.Value, cancellationToken);
                var benefitIc = string.IsNullOrWhiteSpace(user?.SnapId) ? user?.TanfId : user?.SnapId;
                if (user?.IsCoLoaded == true
                    && user.DateOfBirth is { } verifiedDob
                    && !string.IsNullOrWhiteSpace(benefitIc))
                {
                    householdData = await repository.GetHouseholdByBenefitIdentifierAndGuardianDobAsync(
                        identifier.Value,
                        benefitIc.Trim(),
                        verifiedDob,
                        piiVisibility,
                        userIalLevel,
                        cancellationToken);
                    if (householdData != null)
                    {
                        logger.LogInformation(
                            "Household data loaded via co-loaded IC + DOB fallback for user {UserId}",
                            userId);
                    }
                }
            }
        }

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

        // Classify the household on the PRE-filter state so analytics can
        // distinguish cohorts even after co-loaded cases are suppressed. Then
        // apply the suppression for the excluded cohort.
        householdData.CoLoadedCohort = ClassifyCoLoadedCohort(householdData);

        var nonCoLoaded = householdData.SummerEbtCases.Where(c => !c.IsCoLoaded).ToList();
        if (coLoadedCohortFilter.SuppressCoLoadedCasesForExcludedCohort
            && householdData.CoLoadedCohort == CoLoadedCohort.MixedOrApplicantExcluded)
        {
            // Suppress co-loaded cases from the payload for the excluded cohort
            // (mixed-eligibility families and applicants with co-loaded benefits).
            // Co-loaded-only households keep their cases so the dashboard isn't
            // empty; per-case flags still deny self-service actions for them.
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

        logger.LogDebug(
            "Household data retrieved successfully for identifier type {Type}, cohort {Cohort}",
            identifier.Type,
            householdData.CoLoadedCohort);
        return Result<HouseholdData>.Success(householdData);
    }

    /// <summary>
    /// Classifies the household based on its pre-filter case list and applications.
    /// See <see cref="CoLoadedCohort"/> for the rule.
    /// The rule is intentionally derived at runtime from case and application state; changing
    /// who falls into each cohort still requires a code change. Whether co-loaded cases are
    /// suppressed for the excluded cohort is configured via
    /// <see cref="CoLoadedCohortFilterSettings.SuppressCoLoadedCasesForExcludedCohort"/>.
    /// </summary>
    private static CoLoadedCohort ClassifyCoLoadedCohort(HouseholdData household)
    {
        var hasCoLoaded = household.SummerEbtCases.Any(c => c.IsCoLoaded);
        if (!hasCoLoaded)
        {
            return CoLoadedCohort.NonCoLoaded;
        }

        var hasNonCoLoaded = household.SummerEbtCases.Any(c => !c.IsCoLoaded);
        var hasInFlightHouseholdApplication = household.Applications.Any(IsInFlightHouseholdApplication);
        var hasPendingCase = household.SummerEbtCases.Any(IsPendingApplicant);

        return hasNonCoLoaded || hasInFlightHouseholdApplication || hasPendingCase
            ? CoLoadedCohort.MixedOrApplicantExcluded
            : CoLoadedCohort.CoLoadedOnly;
    }

    /// <summary>
    /// A case whose application hasn't been adjudicated yet represents an
    /// in-flight applicant experience, which places the household in the
    /// applicant-excluded cohort even when the case itself is co-loaded.
    /// </summary>
    private static bool IsPendingApplicant(SummerEbtCase summerEbtCase) =>
        summerEbtCase.ApplicationStatus is ApplicationStatus.Pending or ApplicationStatus.UnderReview;

    /// <summary>
    /// Household-level <see cref="HouseholdData.Applications"/> often retains historical rows
    /// (approved/denied/cancelled). Only pending or under-review applications indicate an active
    /// applicant journey alongside co-loaded cases.
    /// </summary>
    private static bool IsInFlightHouseholdApplication(Application application) =>
        application.ApplicationStatus is ApplicationStatus.Pending or ApplicationStatus.UnderReview;
}
