using SEBT.Portal.Core.Models.DocVerification;
using SEBT.Portal.TestUtilities.Helpers;

namespace SEBT.Portal.Tests.Unit.Models;

public class DocVerificationChallengeTests
{
    // --- Valid state transitions ---

    [Fact]
    public void TransitionTo_ShouldSucceed_WhenCreatedToPending()
    {
        var challenge = DocVerificationChallengeFactory.CreateChallenge();
        var beforeTransition = DateTime.UtcNow;

        challenge.TransitionTo(DocVerificationStatus.Pending);

        Assert.Equal(DocVerificationStatus.Pending, challenge.Status);
        Assert.True(challenge.UpdatedAt >= beforeTransition);
    }

    [Fact]
    public void TransitionTo_ShouldSucceed_WhenPendingToVerified()
    {
        var challenge = DocVerificationChallengeFactory.CreatePendingChallenge();
        var beforeTransition = DateTime.UtcNow;

        challenge.TransitionTo(DocVerificationStatus.Verified);

        Assert.Equal(DocVerificationStatus.Verified, challenge.Status);
        Assert.True(challenge.UpdatedAt >= beforeTransition);
    }

    [Fact]
    public void TransitionTo_ShouldSucceed_WhenPendingToRejected()
    {
        var challenge = DocVerificationChallengeFactory.CreatePendingChallenge();
        var beforeTransition = DateTime.UtcNow;

        challenge.TransitionTo(DocVerificationStatus.Rejected);

        Assert.Equal(DocVerificationStatus.Rejected, challenge.Status);
        Assert.True(challenge.UpdatedAt >= beforeTransition);
    }

    [Fact]
    public void TransitionTo_ShouldSucceed_WhenPendingToExpired()
    {
        var challenge = DocVerificationChallengeFactory.CreatePendingChallenge();
        var beforeTransition = DateTime.UtcNow;

        challenge.TransitionTo(DocVerificationStatus.Expired);

        Assert.Equal(DocVerificationStatus.Expired, challenge.Status);
        Assert.True(challenge.UpdatedAt >= beforeTransition);
    }

    [Fact]
    public void TransitionTo_ShouldSucceed_WhenCreatedToExpired()
    {
        var challenge = DocVerificationChallengeFactory.CreateChallenge();
        var beforeTransition = DateTime.UtcNow;

        challenge.TransitionTo(DocVerificationStatus.Expired);

        Assert.Equal(DocVerificationStatus.Expired, challenge.Status);
        Assert.True(challenge.UpdatedAt >= beforeTransition);
    }

    // --- Invalid transitions from terminal states ---

    [Theory]
    [InlineData(DocVerificationStatus.Created)]
    [InlineData(DocVerificationStatus.Pending)]
    [InlineData(DocVerificationStatus.Rejected)]
    [InlineData(DocVerificationStatus.Expired)]
    public void TransitionTo_ShouldThrow_WhenFromVerified(DocVerificationStatus target)
    {
        var challenge = DocVerificationChallengeFactory.CreateVerifiedChallenge();

        var ex = Assert.Throws<InvalidOperationException>(
            () => challenge.TransitionTo(target));
        Assert.Contains("Cannot transition from Verified", ex.Message);
    }

    [Theory]
    [InlineData(DocVerificationStatus.Created)]
    [InlineData(DocVerificationStatus.Pending)]
    [InlineData(DocVerificationStatus.Verified)]
    [InlineData(DocVerificationStatus.Expired)]
    public void TransitionTo_ShouldThrow_WhenFromRejected(DocVerificationStatus target)
    {
        var challenge = DocVerificationChallengeFactory.CreateRejectedChallenge();

        var ex = Assert.Throws<InvalidOperationException>(
            () => challenge.TransitionTo(target));
        Assert.Contains("Cannot transition from Rejected", ex.Message);
    }

    [Theory]
    [InlineData(DocVerificationStatus.Created)]
    [InlineData(DocVerificationStatus.Pending)]
    [InlineData(DocVerificationStatus.Verified)]
    [InlineData(DocVerificationStatus.Rejected)]
    public void TransitionTo_ShouldThrow_WhenFromExpired(DocVerificationStatus target)
    {
        var challenge = DocVerificationChallengeFactory.CreatePendingChallenge();
        challenge.TransitionTo(DocVerificationStatus.Expired);

        var ex = Assert.Throws<InvalidOperationException>(
            () => challenge.TransitionTo(target));
        Assert.Contains("Cannot transition from Expired", ex.Message);
    }

    // --- Invalid transitions that skip Pending ---

    [Fact]
    public void TransitionTo_ShouldThrow_WhenCreatedToVerified()
    {
        var challenge = DocVerificationChallengeFactory.CreateChallenge();

        var ex = Assert.Throws<InvalidOperationException>(
            () => challenge.TransitionTo(DocVerificationStatus.Verified));
        Assert.Contains("Cannot transition from Created to Verified", ex.Message);
    }

    [Fact]
    public void TransitionTo_ShouldThrow_WhenCreatedToRejected()
    {
        var challenge = DocVerificationChallengeFactory.CreateChallenge();

        var ex = Assert.Throws<InvalidOperationException>(
            () => challenge.TransitionTo(DocVerificationStatus.Rejected));
        Assert.Contains("Cannot transition from Created to Rejected", ex.Message);
    }

    // --- IsTerminal property ---

    [Theory]
    [InlineData(DocVerificationStatus.Created, false)]
    [InlineData(DocVerificationStatus.Pending, false)]
    [InlineData(DocVerificationStatus.Verified, true)]
    [InlineData(DocVerificationStatus.Rejected, true)]
    [InlineData(DocVerificationStatus.Expired, true)]
    public void IsTerminal_ShouldReturnExpectedValue_ForEachStatus(
        DocVerificationStatus status, bool expected)
    {
        var challenge = DocVerificationChallenge.Reconstitute(
            id: Guid.NewGuid(),
            publicId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            status: status,
            socureReferenceId: null,
            evalId: null,
            socureEventId: null,
            docvTransactionToken: null,
            docvUrl: null,
            offboardingReason: null,
            allowIdRetry: true,
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow,
            expiresAt: null);

        Assert.Equal(expected, challenge.IsTerminal);
    }

    // --- Reconstitute ---

    [Fact]
    public void Reconstitute_ShouldPreserveAllFields()
    {
        var id = Guid.NewGuid();
        var publicId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var status = DocVerificationStatus.Verified;
        var socureReferenceId = "ref-123";
        var evalId = "eval-456";
        var socureEventId = "evt-789";
        var docvTransactionToken = "token-abc";
        var docvUrl = "https://verify.socure.com/#/dv/token-abc";
        var offboardingReason = "docVerificationFailed";
        var allowIdRetry = false;
        var createdAt = new DateTime(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2025, 6, 1, 12, 30, 0, DateTimeKind.Utc);
        var expiresAt = new DateTime(2025, 6, 1, 12, 30, 0, DateTimeKind.Utc);
        var proofingDob = "1990-01-01";
        var proofingIdType = "ssn";
        var proofingIdValue = "999-99-9999";
        var docvTokenIssuedAt = new DateTime(2025, 6, 1, 12, 15, 0, DateTimeKind.Utc);

        var challenge = DocVerificationChallenge.Reconstitute(
            id, publicId, userId, status,
            socureReferenceId, evalId, socureEventId,
            docvTransactionToken, docvUrl, offboardingReason,
            allowIdRetry, createdAt, updatedAt, expiresAt,
            proofingDob, proofingIdType, proofingIdValue, docvTokenIssuedAt);

        Assert.Equal(id, challenge.Id);
        Assert.Equal(publicId, challenge.PublicId);
        Assert.Equal(userId, challenge.UserId);
        Assert.Equal(status, challenge.Status);
        Assert.Equal(socureReferenceId, challenge.SocureReferenceId);
        Assert.Equal(evalId, challenge.EvalId);
        Assert.Equal(socureEventId, challenge.SocureEventId);
        Assert.Equal(docvTransactionToken, challenge.DocvTransactionToken);
        Assert.Equal(docvUrl, challenge.DocvUrl);
        Assert.Equal(offboardingReason, challenge.OffboardingReason);
        Assert.Equal(allowIdRetry, challenge.AllowIdRetry);
        Assert.Equal(createdAt, challenge.CreatedAt);
        Assert.Equal(updatedAt, challenge.UpdatedAt);
        Assert.Equal(expiresAt, challenge.ExpiresAt);
        Assert.Equal(proofingDob, challenge.ProofingDateOfBirth);
        Assert.Equal(proofingIdType, challenge.ProofingIdType);
        Assert.Equal(proofingIdValue, challenge.ProofingIdValue);
        Assert.Equal(docvTokenIssuedAt, challenge.DocvTokenIssuedAt);
    }

    // --- PII scrubbing on terminal transitions ---

    [Theory]
    [InlineData(DocVerificationStatus.Verified)]
    [InlineData(DocVerificationStatus.Rejected)]
    [InlineData(DocVerificationStatus.Expired)]
    public void TransitionTo_ShouldScrubProofingPii_WhenTransitioningToTerminalState(
        DocVerificationStatus terminalStatus)
    {
        var challenge = DocVerificationChallengeFactory.CreatePendingChallenge();
        challenge.ProofingDateOfBirth = "1990-01-01";
        challenge.ProofingIdType = "ssn";
        challenge.ProofingIdValue = "999-99-9999";
        var issuedAt = DateTime.UtcNow.AddMinutes(-5);
        challenge.DocvTokenIssuedAt = issuedAt;

        challenge.TransitionTo(terminalStatus);

        Assert.Null(challenge.ProofingDateOfBirth);
        Assert.Null(challenge.ProofingIdType);
        Assert.Null(challenge.ProofingIdValue);
        Assert.Equal(issuedAt, challenge.DocvTokenIssuedAt);
    }

    [Fact]
    public void TransitionTo_ShouldScrubProofingPii_WhenCreatedExpiresDirectly()
    {
        var challenge = DocVerificationChallengeFactory.CreateChallenge();
        challenge.ProofingDateOfBirth = "1990-01-01";
        challenge.ProofingIdType = "ssn";
        challenge.ProofingIdValue = "999-99-9999";

        challenge.TransitionTo(DocVerificationStatus.Expired);

        Assert.Null(challenge.ProofingDateOfBirth);
        Assert.Null(challenge.ProofingIdType);
        Assert.Null(challenge.ProofingIdValue);
    }

    [Fact]
    public void TransitionTo_ShouldPreserveProofingPii_WhenTransitioningToNonTerminalState()
    {
        var challenge = DocVerificationChallengeFactory.CreateChallenge();
        challenge.ProofingDateOfBirth = "1990-01-01";
        challenge.ProofingIdType = "ssn";
        challenge.ProofingIdValue = "999-99-9999";

        challenge.TransitionTo(DocVerificationStatus.Pending);

        Assert.Equal("1990-01-01", challenge.ProofingDateOfBirth);
        Assert.Equal("ssn", challenge.ProofingIdType);
        Assert.Equal("999-99-9999", challenge.ProofingIdValue);
    }

    [Fact]
    public void Reconstitute_ShouldAcceptInvalidEnumValue()
    {
        var invalidStatus = (DocVerificationStatus)99;

        var challenge = DocVerificationChallenge.Reconstitute(
            id: Guid.NewGuid(),
            publicId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            status: invalidStatus,
            socureReferenceId: null,
            evalId: null,
            socureEventId: null,
            docvTransactionToken: null,
            docvUrl: null,
            offboardingReason: null,
            allowIdRetry: true,
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow,
            expiresAt: null);

        Assert.Equal(invalidStatus, challenge.Status);
    }
}
