using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.EnrollmentCheck;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.UseCases.EnrollmentCheck;

public class CheckEnrollmentCommandHandler(
    IEnrollmentCheckBackend enrollmentCheckBackend,
    IEnrollmentCheckSubmissionLogger submissionLogger,
    ILogger<CheckEnrollmentCommandHandler> logger,
    IFeatureManager featureManager)
    : ICommandHandler<CheckEnrollmentCommand, EnrollmentCheckOutcome>
{
    public async Task<Result<EnrollmentCheckOutcome>> Handle(
        CheckEnrollmentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.Children.Count == 0)
        {
            return Result<EnrollmentCheckOutcome>.ValidationFailed(
                "Children", "At least one child is required.");
        }

        const int maxChildren = 20;
        if (command.Children.Count > maxChildren)
        {
            return Result<EnrollmentCheckOutcome>.ValidationFailed(
                "Children", $"A maximum of {maxChildren} children can be checked per request.");
        }

        logger.LogInformation("Enrollment check requested for {ChildCount} child(ren)", command.Children.Count);

        // Mint one correlation id per submitted child; the backend echoes it on each result,
        // and the outcome's identity fields always come from these submitted children.
        var submittedChildren = command.Children
            .Select(c => (CheckId: Guid.NewGuid(), Child: c))
            .ToList();

        var request = new EnrollmentCheckRequest(
            submittedChildren
                .Select(x => new EnrollmentChild(
                    x.CheckId.ToString(),
                    x.Child.FirstName,
                    x.Child.LastName,
                    x.Child.DateOfBirth,
                    SchoolIdentifier: x.Child.SchoolCode ?? x.Child.SchoolName))
                .ToList());

        EnrollmentCheckResult result;
        try
        {
            result = await enrollmentCheckBackend.CheckEnrollmentAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Enrollment check backend failed");
            return Result<EnrollmentCheckOutcome>.DependencyFailed(
                DependencyFailedReason.ConnectionFailed,
                "Enrollment check service is temporarily unavailable.");
        }

        var outcome = BuildOutcome(submittedChildren, result);

        if (await featureManager.IsEnabledAsync(FeatureFlags.EnrollmentCheckRequiresAtLeastOneExactMatchedField))
        {
            outcome = AppendSyntheticNonMatches(submittedChildren, outcome);
        }

        // Log de-identified submission (fire and forget, don't fail the request)
        try
        {
            var submission = new EnrollmentCheckSubmission
            {
                SubmissionId = Guid.NewGuid(),
                CheckedAtUtc = DateTime.UtcNow,
                ChildrenChecked = outcome.Results.Count,
                IpAddressHash = HashIpAddress(command.IpAddress),
                ChildResults = outcome.Results.Select(r => new DeidentifiedChildResult
                {
                    BirthYear = r.DateOfBirth.Year,
                    Status = r.IsMatch ? "Match" : "NonMatch"
                    // EligibilityType and SchoolName stay null — no state connector ever populated them.
                }).ToList()
            };

            await submissionLogger.LogSubmissionAsync(submission, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to log enrollment check submission (non-fatal)");
        }

        return Result<EnrollmentCheckOutcome>.Success(outcome);
    }

    /// <summary>
    /// Joins backend results to the submitted children by correlation id. Identity fields come
    /// from the submission, never the backend, so no state-system PII reaches the UI; a result
    /// matching no submitted child is dropped.
    /// </summary>
    private static EnrollmentCheckOutcome BuildOutcome(
        IReadOnlyList<(Guid CheckId, CheckEnrollmentCommand.ChildInput Child)> submittedChildren,
        EnrollmentCheckResult result)
    {
        var submittedByCheckId = submittedChildren.ToDictionary(x => x.CheckId.ToString());

        var results = new List<EnrollmentChildOutcome>(result.Results.Count);
        foreach (var childResult in result.Results)
        {
            if (!submittedByCheckId.TryGetValue(childResult.CheckId, out var submitted))
            {
                continue;
            }

            results.Add(new EnrollmentChildOutcome(
                submitted.CheckId,
                submitted.Child.FirstName,
                submitted.Child.LastName,
                submitted.Child.DateOfBirth,
                childResult.IsMatch,
                childResult.MatchConfidence,
                childResult.StatusMessage));
        }

        return new EnrollmentCheckOutcome(results, result.Message);
    }

    /// <summary>
    /// For any submitted child with no backend result (e.g. dropped by the exact-match guard),
    /// appends a bare NonMatch so the response always contains one result per submitted child.
    /// </summary>
    private static EnrollmentCheckOutcome AppendSyntheticNonMatches(
        IReadOnlyList<(Guid CheckId, CheckEnrollmentCommand.ChildInput Child)> submittedChildren,
        EnrollmentCheckOutcome outcome)
    {
        var surfacedIds = outcome.Results.Select(r => r.CheckId).ToHashSet();
        var synthetic = submittedChildren
            .Where(x => !surfacedIds.Contains(x.CheckId))
            .Select(x => new EnrollmentChildOutcome(
                x.CheckId,
                x.Child.FirstName,
                x.Child.LastName,
                x.Child.DateOfBirth,
                IsMatch: false));

        return outcome with { Results = [.. outcome.Results, .. synthetic] };
    }

    private static string? HashIpAddress(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return null;
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(ipAddress));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
