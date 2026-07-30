namespace SEBT.Portal.Core.StateBackends;

public interface ICardReplacementBackend
{
    Task<WriteResult> RequestCardReplacementAsync(
        CardReplacementRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// A card-replacement request routed by a batch of opaque decodable <see cref="CaseIds"/> tokens;
/// the backend decodes each token into the routing fields a write needs — including the household
/// identifier, which rides inside the tokens. Cooldown, persistence, and hashing stay portal-side.
/// </summary>
public sealed record CardReplacementRequest(IReadOnlyList<string> CaseIds);
