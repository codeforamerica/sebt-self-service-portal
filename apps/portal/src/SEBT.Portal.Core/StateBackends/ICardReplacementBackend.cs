namespace SEBT.Portal.Core.StateBackends;

public interface ICardReplacementBackend
{
    Task<WriteResult> RequestCardReplacementAsync(
        CardReplacementRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// A card-replacement request routed by opaque case tokens. The household identifier rides inside
/// this envelope. Cooldown, persistence, and hashing stay portal-side.
/// </summary>
public sealed record CardReplacementRequest(IReadOnlyList<string> CaseIds)
{
    /// <summary>The identifier the write binds as <c>householdIdentifier</c>; optional when the backend keys only on case tokens.</summary>
    public string? HouseholdIdentifier { get; init; }
}
