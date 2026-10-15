namespace SEBT.Portal.Core.StateBackends;

public interface IAddressUpdateBackend
{
    Task<WriteResult> UpdateAddressAsync(
        AddressUpdateRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// A household-routed mailing-address update. Routing prefers
/// <see cref="HouseholdIdentifier"/> on this envelope. A single backend
/// call yields a single <see cref="WriteResult"/>.
/// </summary>
public sealed record AddressUpdateRequest(
    string HouseholdIdentifier,
    IReadOnlyList<string> CaseIds,
    AddressUpdateAddress Address);

/// <summary>The validated mailing-address scalars to persist.</summary>
public sealed record AddressUpdateAddress
{
    public string? Line1 { get; init; }
    public string? Line2 { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? Zip { get; init; }
}
