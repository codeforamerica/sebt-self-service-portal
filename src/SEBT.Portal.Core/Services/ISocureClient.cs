using SEBT.Portal.Core.Models.DocVerification;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Kernel;

namespace SEBT.Portal.Core.Services;

/// <summary>
/// Abstraction over the Socure identity verification API.
/// Workflow-oriented methods that avoid leaking Socure-specific payload shapes into use cases.
/// Backed by StubSocureClient in development, real HTTP client when credentials are available.
/// </summary>
public interface ISocureClient
{
    /// <summary>
    /// Performs an ID proofing risk assessment based on user-provided identity data.
    /// Returns whether the user matched, needs document verification, or failed.
    /// </summary>
    /// <param name="userId">Internal user ID for correlation.</param>
    /// <param name="email">User's email address.</param>
    /// <param name="dateOfBirth">User's date of birth (yyyy-MM-dd).</param>
    /// <param name="idType">Type of government ID provided (ssn, itin, etc.), or null.</param>
    /// <param name="idValue">The ID value, or null.</param>
    /// <param name="ipAddress">The user's IP address from the HTTP request, or null.</param>
    /// <param name="phoneNumber">The user's phone number, or null.</param>
    /// <param name="givenName">The user's first name, or null.</param>
    /// <param name="familyName">The user's last name, or null.</param>
    /// <param name="address">The user's mailing address from household data, or null.</param>
    /// <param name="diSessionToken">Device Intelligence session token from the frontend SDK, or null to use config fallback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating the assessment outcome.</returns>
    Task<Result<IdProofingAssessmentResult>> RunIdProofingAssessmentAsync(
        int userId,
        string email,
        string dateOfBirth,
        string? idType,
        string? idValue,
        string? ipAddress = null,
        string? phoneNumber = null,
        string? givenName = null,
        string? familyName = null,
        Address? address = null,
        string? diSessionToken = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a Socure DocV session for document capture. Generates a short-lived transaction token.
    /// Called just-in-time when the user is ready to start document capture.
    /// </summary>
    /// <param name="userId">Internal user ID.</param>
    /// <param name="email">User's email address.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A session containing the DocV transaction token and URL.</returns>
    Task<Result<SocureDocvSession>> StartDocvSessionAsync(
        int userId,
        string email,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The outcome of an ID proofing risk assessment.
/// </summary>
public enum IdProofingOutcome
{
    /// <summary>User's identity was verified against existing records.</summary>
    Matched,

    /// <summary>User needs to upload identity documents for further verification.</summary>
    DocumentVerificationRequired,

    /// <summary>Identity verification failed.</summary>
    Failed
}

/// <summary>
/// Result of an ID proofing risk assessment from Socure.
/// </summary>
/// <param name="Outcome">Whether the user matched, needs doc verification, or failed.</param>
/// <param name="AllowIdRetry">Whether the user can retry with a different ID number.</param>
/// <param name="DocvSession">DocV session data from the evaluation response, if document verification is required.</param>
public record IdProofingAssessmentResult(
    IdProofingOutcome Outcome,
    bool AllowIdRetry,
    SocureDocvSession? DocvSession = null);
