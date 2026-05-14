using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Core.Repositories;

/// <summary>
/// Repository interface for managing household data.
/// Uses state-configurable household identifiers (e.g. email, SNAP ID); the API resolves the identifier via <see cref="Services.IHouseholdIdentifierResolver"/> then calls <see cref="GetHouseholdByIdentifierAsync"/>.
/// UpsertHouseholdAsync is intended for data loading scripts, not for API endpoints.
/// </summary>
public interface IHouseholdRepository
{
    /// <summary>
    /// Retrieves household data by the given household identifier (type + value). The identifier type is determined by state configuration (e.g. Email, SNAP ID).
    /// PII fields (Address, Email, Phone etc.) are filtered based on the visibility flags.
    /// </summary>
    /// <param name="identifier">The household identifier (type and value) to look up.</param>
    /// <param name="piiVisibility">Which PII elements to include. Required; no default. Callers must obtain this from <see cref="IIdProofingService"/> based on the user's IAL level.</param>
    /// <param name="userIalLevel">Identity Assurance Level the user has achieved. Passed to state plugins for backend policy (e.g. whether to return address).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The household data if found; otherwise, <c>null</c>.</returns>
    Task<HouseholdData?> GetHouseholdByIdentifierAsync(
        HouseholdIdentifier identifier,
        PiiVisibility piiVisibility,
        UserIalLevel userIalLevel,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves household data by email address. Prefer <see cref="GetHouseholdByIdentifierAsync"/> for API use so lookup is state-configurable.
    /// PII fields are filtered based on the visibility flags.
    /// </summary>
    /// <param name="email">The email address of the household.</param>
    /// <param name="piiVisibility">Which PII elements to include. Required; no default. Callers must obtain this from <see cref="IIdProofingService"/> based on the user's ID proofing status.</param>
    /// <param name="userIalLevel">Identity Assurance Level the user has achieved. Passed to state plugins for backend policy.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The household data if found; otherwise, <c>null</c>.</returns>
    Task<HouseholdData?> GetHouseholdByEmailAsync(
        string email,
        PiiVisibility piiVisibility,
        UserIalLevel userIalLevel,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// For co-loaded SNAP/TANF ID proofing: asks the state plugin whether the submitted benefit identifier
    /// (IC) and guardian date of birth match state warehouse data. Implemented for DC only; other states return <c>false</c>.
    /// </summary>
    /// <param name="benefitIdentifierIc">SNAP/TANF identifier from onboarding, mapped to warehouse IC.</param>
    /// <param name="guardianDateOfBirth">Guardian DOB from ID proofing.</param>
    /// <param name="portalUserId">Portal <c>User.Id</c>. DC merges this into the warehouse GuardianIdentifiers JSON as <c>PortalUUID</c> for cross-system correlation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the state reports a match.</returns>
    Task<bool> TryMatchCoLoadedGuardianByBenefitIdAndDobAsync(
        string benefitIdentifierIc,
        DateOnly guardianDateOfBirth,
        Guid portalUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// DC co-loaded fallback: loads household via warehouse IC + guardian DOB when rows use IC as <c>PortalID</c>.
    /// Sets envelope email to <paramref name="guardianLoginEmail"/>; non-DC plugins return <c>null</c>.
    /// </summary>
    /// <param name="portalUserId">Portal <c>User.Id</c>. DC merges this into the warehouse GuardianIdentifiers JSON as <c>PortalUUID</c> for cross-system correlation.</param>
    Task<HouseholdData?> GetHouseholdByBenefitIdentifierAndGuardianDobAsync(
        string guardianLoginEmail,
        string benefitIdentifierIc,
        DateOnly guardianDateOfBirth,
        PiiVisibility piiVisibility,
        UserIalLevel userIalLevel,
        Guid portalUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates household data.
    /// </summary>
    /// <param name="householdData">The household data to create or update.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpsertHouseholdAsync(
        HouseholdData householdData,
        CancellationToken cancellationToken = default);
}
