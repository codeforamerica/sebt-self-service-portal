namespace SEBT.Portal.Core.Models.DocVerification;

/// <summary>
/// Represents the response from Socure's DocV token generation.
/// Contains the transaction token and URL needed by the frontend SDK.
/// </summary>
/// <param name="DocvTransactionToken">The token passed to the Socure DocV frontend SDK.</param>
/// <param name="DocvUrl">The Socure verification URL for the document capture flow.</param>
/// <param name="ReferenceId">Socure's reference ID for correlating webhook callbacks.</param>
/// <param name="EvalId">Socure's evaluation ID for fallback correlation.</param>
public record SocureDocvSession(
    string DocvTransactionToken,
    string DocvUrl,
    string ReferenceId,
    string EvalId);
