namespace SEBT.Portal.Core.StateBackends;

public interface IAddressUpdateBackend
{
    Task<WriteResult> UpdateAddressAsync(
        AddressUpdateRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// A household-level mailing-address update. <see cref="HouseholdIdentifier"/> is the operation's
/// subject — the update is household-routed, so <see cref="CaseIds"/> MAY BE EMPTY (a zero-case
/// household still updates). When present, the opaque case tokens span every case the household
/// owns; driver configs collect per-case write-ids from them. No per-case success channel — a
/// single backend call yields a single <see cref="WriteResult"/>.
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
