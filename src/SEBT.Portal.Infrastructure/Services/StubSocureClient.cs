using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Models.DocVerification;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Stub implementation of <see cref="ISocureClient"/> for development and testing.
/// Returns deterministic data without making HTTP calls.
/// Swapped for the real HTTP client when Socure credentials are available.
/// </summary>
public class StubSocureClient(ILogger<StubSocureClient> logger) : ISocureClient
{
    public Task<Result<IdProofingAssessmentResult>> RunIdProofingAssessmentAsync(
        Guid userId,
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
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Stub: Running ID proofing assessment for user {UserId}",
            userId);

        // If no ID was provided, fail the assessment
        if (string.IsNullOrWhiteSpace(idType) || string.IsNullOrWhiteSpace(idValue))
        {
            return Task.FromResult(Result<IdProofingAssessmentResult>.Success(
                new IdProofingAssessmentResult(IdProofingOutcome.Failed, AllowIdRetry: false)));
        }

        // Stub: always require document verification so the full flow can be tested
        var result = new IdProofingAssessmentResult(
            Outcome: IdProofingOutcome.DocumentVerificationRequired,
            AllowIdRetry: true);

        return Task.FromResult(Result<IdProofingAssessmentResult>.Success(result));
    }

    public Task<Result<SocureDocvSession>> StartDocvSessionAsync(
        Guid userId,
        string email,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Stub: Starting DocV session for user {UserId}",
            userId);

        var token = Guid.NewGuid().ToString();
        var session = new SocureDocvSession(
            DocvTransactionToken: token,
            DocvUrl: $"https://verify.socure.com/#/dv/{token}",
            ReferenceId: Guid.NewGuid().ToString(),
            EvalId: Guid.NewGuid().ToString());

        return Task.FromResult(Result<SocureDocvSession>.Success(session));
    }

    public Task<Result<IdProofingAssessmentResult>> RunDocvStepupAssessmentAsync(
        Guid userId,
        string email,
        string? phoneNumber = null,
        string? givenName = null,
        string? familyName = null,
        Address? address = null,
        string? diSessionToken = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Stub: Starting DocV step-up evaluation for user {UserId}", userId);

        var token = Guid.NewGuid().ToString();
        var session = new SocureDocvSession(
            DocvTransactionToken: token,
            DocvUrl: $"https://verify.socure.com/#/dv/{token}",
            ReferenceId: Guid.NewGuid().ToString(),
            EvalId: Guid.NewGuid().ToString());

        return Task.FromResult(Result<IdProofingAssessmentResult>.Success(
            new IdProofingAssessmentResult(
                Outcome: IdProofingOutcome.DocumentVerificationRequired,
                AllowIdRetry: true,
                DocvSession: session)));
    }
}
