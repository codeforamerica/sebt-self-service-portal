extern alias Core;

namespace SEBT.Portal.Api.Models.Household;

using BenefitIssuanceType = Core::SEBT.Portal.Core.Models.Household.BenefitIssuanceType;

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
}
