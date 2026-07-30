using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Core.StateBackends;

public enum HouseholdLookupStatus { Found, NotFound }

public interface IHouseholdLookupBackend
{
    Task<HouseholdLookupResult> LookupHouseholdAsync(
        HouseholdLookupRequest request, CancellationToken cancellationToken = default);
}

public sealed record IdentitySignal(string Type, string Value);

/// <summary>
/// A household lookup request. <see cref="Signals"/> are household-search keys; <see cref="IsProofed"/>
/// and <see cref="PortalUuid"/> are caller context about the authenticated user.
/// </summary>
/// <remarks>
/// <see cref="IsProofed"/> is passed through to the backend, never used for an authorization decision
/// here; DC gates its email-lookup branch on it, so it must reflect the caller's real proofing status.
/// </remarks>
public sealed record HouseholdLookupRequest(IReadOnlyList<IdentitySignal> Signals)
{
    public bool IsProofed { get; init; }
    public string? PortalUuid { get; init; }

    /// <summary>
    /// The household identifier value this lookup searched by. A caseId composition packs it into
    /// tokens via <c>fromContext</c> when a write must route by an identifier the lookup response
    /// never echoes. Null when the caller has no single household identifier (e.g. an existence
    /// check whose result data is discarded).
    /// </summary>
    public string? HouseholdIdentifier { get; init; }
}
public sealed record HouseholdLookupResult(HouseholdLookupStatus Status, HouseholdData? Household);
