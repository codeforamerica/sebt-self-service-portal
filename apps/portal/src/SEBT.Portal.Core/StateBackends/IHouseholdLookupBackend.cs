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
/// <see cref="IsProofed"/> gates DC's email-lookup branch, so it must reflect the caller's real
/// proofing status.
/// </summary>
public sealed record HouseholdLookupRequest(IReadOnlyList<IdentitySignal> Signals)
{
    public bool IsProofed { get; init; }
    public string? PortalUuid { get; init; }

    /// <summary>
    /// The identifier this lookup searched by; <c>fromContext</c> caseId compositions pack it when a
    /// write routes by a value the lookup response never echoes. Null when the caller has none.
    /// </summary>
    public string? HouseholdIdentifier { get; init; }
}
public sealed record HouseholdLookupResult(HouseholdLookupStatus Status, HouseholdData? Household);
