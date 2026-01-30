using SEBT.Portal.Core.Models.Household;

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
    /// </summary>
    /// <param name="identifier">The household identifier (type and value) to look up.</param>
    /// <param name="includeAddress">Whether to include address information. Should only be true if ID verification is completed.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The household data if found; otherwise, <c>null</c>.</returns>
    Task<HouseholdData?> GetHouseholdByIdentifierAsync(
        HouseholdIdentifier identifier,
        bool includeAddress = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves household data by email address. Prefer <see cref="GetHouseholdByIdentifierAsync"/> for API use so lookup is state-configurable.
    /// </summary>
    /// <param name="email">The email address of the household.</param>
    /// <param name="includeAddress">Whether to include address information. Should only be true if ID verification is completed.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The household data if found; otherwise, <c>null</c>.</returns>
    Task<HouseholdData?> GetHouseholdByEmailAsync(
        string email,
        bool includeAddress = false,
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
