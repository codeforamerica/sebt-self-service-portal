using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Core.Repositories;

/// <summary>
/// Repository interface for managing household data.
/// Note: The API controller is read-only and only uses GetHouseholdByEmailAsync.
/// UpsertHouseholdAsync is intended for data loading scripts, not for API endpoints.
/// </summary>
public interface IHouseholdRepository
{
    /// <summary>
    /// Retrieves household data by email address.
    /// PII fields (Address, Email, Phone etc.) are filtered based on the visibility flags,
    /// which are determined by the user's ID proofing status and state configuration.
    /// </summary>
    /// <param name="email">The email address of the household.</param>
    /// <param name="piiVisibility">Which PII elements to include. Required; no default. Callers must obtain this from <see cref="IIdProofingRequirementsService.GetPiiVisibility"/> based on the user's ID proofing status.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The household data if found; otherwise, <c>null</c>.</returns>
    Task<HouseholdData?> GetHouseholdByEmailAsync(
        string email,
        PiiVisibility piiVisibility,
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
