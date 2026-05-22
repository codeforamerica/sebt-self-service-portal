using SEBT.Portal.StatesPlugins.Interfaces.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

namespace SEBT.Portal.StatesPlugins.Interfaces;

public interface ISummerEbtCaseService : IStatePlugin
{
    /// <summary>
    /// Retrieves household data by identifier type and value (e.g. phone from JWT, email from OTP).
    /// Preferred entry point for state lookups; supports CO phone-from-claims without persisting PII.
    /// </summary>
    /// <param name="identifierType">Type of identifier (e.g. Email, Phone).</param>
    /// <param name="identifierValue">Normalized value (e.g. email or phone number).</param>
    /// <param name="piiVisibility">Which PII elements the caller is allowed to receive.</param>
    /// <param name="identityAssuranceLevel">The IAL the user has achieved.</param>
    /// <param name="portalUserId">Portal <c>User.Id</c>. DC passes this as <c>PortalUUID</c> in <c>@guardianIdentifiers</c> for email lookups; ignored by other states when null.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Household data with Summer EBT cases, or null if none found or type not supported.</returns>
    Task<HouseholdData?> GetHouseholdByIdentifierAsync(
        HouseholdIdentifierType identifierType,
        string identifierValue,
        PiiVisibility piiVisibility,
        IdentityAssuranceLevel identityAssuranceLevel,
        Guid? portalUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves household data for the given guardian email (OTP/authentication email).
    /// Convenience for email-only lookups; implementations may delegate to <see cref="GetHouseholdByIdentifierAsync"/>.
    /// </summary>
    /// <param name="guardianEmail">The guardian's email address used for OTP authentication.</param>
    /// <param name="piiVisibility">Which PII elements the caller is allowed to receive. Required; no default.</param>
    /// <param name="identityAssuranceLevel">The IAL the user has achieved; used for backend policy such as whether to return address.</param>
    /// <param name="portalUserId">Portal <c>User.Id</c>. DC merges this into <c>@guardianIdentifiers</c> as <c>PortalUUID</c> for PortalLog correlation; ignored by other states when null.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Household data with Summer EBT cases, or null if none found.</returns>
    Task<HouseholdData?> GetHouseholdByGuardianEmailAsync(
        string guardianEmail,
        PiiVisibility piiVisibility,
        IdentityAssuranceLevel identityAssuranceLevel,
        Guid? portalUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// For co-loaded ID proofing with SNAP/TANF: attempts to match the submitted benefit identifier
    /// (mapped to the state warehouse <c>IC</c> field) and the guardian's date of birth against
    /// state data. Only the DC plugin implements this (e.g. <c>GetHouseholdByGuardian</c> with JSON
    /// <c>{"IC":...,"DOB":...,"PortalUUID":...}</c>); other states should return <c>false</c>.
    /// </summary>
    /// <param name="benefitIdentifierIc">The SNAP/TANF identifier value from onboarding (IC equivalent).</param>
    /// <param name="guardianDateOfBirth">Guardian DOB from the ID proofing form.</param>
    /// <param name="portalUserId">The portal's User.Id (Guid V7). DC merges this into the GuardianIdentifiers JSON as PortalUUID so the warehouse can correlate calls back to portal records.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when the state warehouse reports at least one matching household case row (DC only).</returns>
    Task<bool> TryMatchCoLoadedGuardianByBenefitIdAndDobAsync(
        string benefitIdentifierIc,
        DateOnly guardianDateOfBirth,
        Guid portalUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves household data when warehouse rows are keyed by benefit IC + guardian DOB (DC co-loaded path).
    /// Populates <see cref="HouseholdData.Email"/> with <paramref name="guardianLoginEmail"/> (portal login email)
    /// because source rows may not include it. Implementations should respect <paramref name="identityAssuranceLevel"/>
    /// for address surfacing the same way as <see cref="GetHouseholdByGuardianEmailAsync"/>.
    /// </summary>
    /// <param name="benefitIdentifierIc">SNAP/TANF identifier matched to warehouse <c>PortalID</c>.</param>
    /// <param name="guardianDateOfBirth">Guardian DOB used with IC in the warehouse lookup.</param>
    /// <param name="guardianLoginEmail">Authenticated guardian email for the returned household envelope.</param>
    /// <param name="portalUserId">The portal's User.Id (Guid V7). DC merges this into the GuardianIdentifiers JSON as PortalUUID so the warehouse can correlate calls back to portal records.</param>
    Task<HouseholdData?> GetHouseholdByBenefitIdentifierAndDobAsync(
        string benefitIdentifierIc,
        DateOnly guardianDateOfBirth,
        string guardianLoginEmail,
        PiiVisibility piiVisibility,
        IdentityAssuranceLevel identityAssuranceLevel,
        Guid portalUserId,
        CancellationToken cancellationToken = default);
}