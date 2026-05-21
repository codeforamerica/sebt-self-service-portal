namespace SEBT.Portal.UseCases.IdProofing;

/// <summary>
/// Response from a successful resubmit retry. Returns the new challenge's public ID so the
/// frontend can poll its status, plus the fresh DocV transaction token + URL for the SDK
/// or new-tab handoff.
/// </summary>
/// <param name="ChallengeId">Public ID of the freshly created <c>DocVerificationChallenge</c>.</param>
/// <param name="DocvTransactionToken">The transaction token for the Socure DocV SDK.</param>
/// <param name="DocvUrl">The Socure verification URL for document capture.</param>
public record ResubmitChallengeResponse(
    Guid ChallengeId,
    string DocvTransactionToken,
    string DocvUrl);
