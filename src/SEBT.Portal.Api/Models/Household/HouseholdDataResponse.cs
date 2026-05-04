extern alias Core;

namespace SEBT.Portal.Api.Models.Household;

using BenefitIssuanceType = Core::SEBT.Portal.Core.Models.Household.BenefitIssuanceType;
using CoLoadedCohort = Core::SEBT.Portal.Core.Models.Household.CoLoadedCohort;

/// <summary>
/// API response model for household data.
/// </summary>
public record HouseholdDataResponse
{
    /// <summary>
    /// The email address on file for the household.
    /// Null when excluded due to ID proofing requirements.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// The phone number on file for the household.
    /// </summary>
    public string? Phone { get; init; }

    /// <summary>
    /// The list of Summer EBT cases (per-child) for this household.
    /// </summary>
    public IReadOnlyList<SummerEbtCaseResponse> SummerEbtCases { get; init; } = Array.Empty<SummerEbtCaseResponse>();

    /// <summary>
    /// The list of applications for this household.
    /// </summary>
    public IReadOnlyList<ApplicationResponse> Applications { get; init; } = Array.Empty<ApplicationResponse>();

    /// <summary>
    /// The address on file. Only populated when ID verification is completed.
    /// </summary>
    public AddressResponse? AddressOnFile { get; init; }

    /// <summary>
    /// The logged-in user's profile (first, middle, last name) for display.
    /// </summary>
    public UserProfileResponse? UserProfile { get; init; }

    /// <summary>
    /// The type of benefit issuance for this household.
    /// </summary>
    public BenefitIssuanceType BenefitIssuanceType { get; init; }

    /// <summary>
    /// Computed permissions for self-service portal actions.
    /// </summary>
    public AllowedActionsResponse? AllowedActions { get; init; }

    /// <summary>
    /// Classification of this household relative to co-loaded benefits.
    /// Used by the frontend to emit a standardized analytics dimension so
    /// the mixed-eligibility/applicant exclusion cohort can be segmented
    /// from non-co-loaded and co-loaded-only households.
    /// </summary>
    public CoLoadedCohort CoLoadedCohort { get; init; }
}
