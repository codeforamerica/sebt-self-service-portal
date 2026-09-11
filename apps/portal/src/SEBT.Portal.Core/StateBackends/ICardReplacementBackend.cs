namespace SEBT.Portal.Core.StateBackends;

public interface ICardReplacementBackend
{
    Task<WriteResult> RequestCardReplacementAsync(
        CardReplacementRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// A card-replacement request routed by opaque case tokens; the household identifier rides inside
/// them. Cooldown, persistence, and hashing stay portal-side.
/// </summary>
public sealed record CardReplacementRequest(IReadOnlyList<string> CaseIds);
