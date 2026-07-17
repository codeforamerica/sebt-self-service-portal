using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Models.DocVerification;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Deterministic Socure stub personas for local development and QA.
/// </summary>
internal static class StubSocurePersonas
{
    /// <summary>
    /// SSN (9 digits) that returns egregious DocV reason code R815 when <see cref="SocureSettings.UseStub"/> is true.
    /// </summary>
    public const string EgregiousDocvIdValue = "999997815";

    public static readonly IReadOnlyList<string> EgregiousReasonCodes = ["R815"];

    public static bool IsEgregiousDocvPersona(string? idValue) =>
        NormalizeIdDigits(idValue) == EgregiousDocvIdValue;

    private static string? NormalizeIdDigits(string? idValue)
    {
        if (string.IsNullOrWhiteSpace(idValue))
        {
            return null;
        }

        var digits = new char[idValue.Length];
        var count = 0;
        foreach (var ch in idValue)
        {
            if (char.IsDigit(ch))
            {
                digits[count++] = ch;
            }
        }

        return count == 0 ? null : new string(digits, 0, count);
    }
}

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

        if (StubSocurePersonas.IsEgregiousDocvPersona(idValue))
        {
            logger.LogInformation(
                "Stub: Returning egregious DocV reason codes ({Codes}) for user {UserId}",
                string.Join(',', StubSocurePersonas.EgregiousReasonCodes),
                userId);
            return Task.FromResult(Result<IdProofingAssessmentResult>.Success(
                new IdProofingAssessmentResult(
                    Outcome: IdProofingOutcome.DocumentVerificationRequired,
                    AllowIdRetry: true,
                    DocumentVerificationReasonCodes: StubSocurePersonas.EgregiousReasonCodes)));
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
