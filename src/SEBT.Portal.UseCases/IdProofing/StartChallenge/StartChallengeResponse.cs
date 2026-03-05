namespace SEBT.Portal.UseCases.IdProofing;

/// <summary>
/// Response from starting a document verification challenge.
/// Contains the Socure DocV session token and URL for the frontend SDK.
/// </summary>
/// <param name="DocvTransactionToken">The transaction token for the Socure DocV SDK.</param>
/// <param name="DocvUrl">The Socure verification URL for document capture.</param>
public record StartChallengeResponse(
    string DocvTransactionToken,
    string DocvUrl);
