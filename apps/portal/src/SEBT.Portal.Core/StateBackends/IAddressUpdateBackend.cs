namespace SEBT.Portal.Core.StateBackends;

public interface IAddressUpdateBackend
{
    Task<WriteResult> UpdateAddressAsync(
        AddressUpdateRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// A household-level mailing-address update, routed by a batch of opaque <see cref="CaseIds"/> tokens
/// spanning every case the household owns. No per-case success channel — a single backend call yields
/// a single <see cref="AddressUpdateResult"/>.
/// </summary>
public sealed record AddressUpdateRequest(IReadOnlyList<string> CaseIds, AddressUpdateAddress Address);

/// <summary>The validated mailing-address scalars to persist.</summary>
public sealed record AddressUpdateAddress
{
    public string? Line1 { get; init; }
    public string? Line2 { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? Zip { get; init; }
}
