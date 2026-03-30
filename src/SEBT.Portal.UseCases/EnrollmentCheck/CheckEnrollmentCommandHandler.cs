using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Models.EnrollmentCheck;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;

namespace SEBT.Portal.UseCases.EnrollmentCheck;

public class CheckEnrollmentCommandHandler(
    IEnrollmentCheckService enrollmentCheckService,
    IEnrollmentCheckSubmissionLogger submissionLogger,
    ILogger<CheckEnrollmentCommandHandler> logger)
    : ICommandHandler<CheckEnrollmentCommand, EnrollmentCheckResult>
{
    public async Task<Result<EnrollmentCheckResult>> Handle(
        CheckEnrollmentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.Children.Count == 0)
        {
            return Result<EnrollmentCheckResult>.ValidationFailed(
                "Children", "At least one child is required.");
        }

        const int maxChildren = 20;
        if (command.Children.Count > maxChildren)
        {
            return Result<EnrollmentCheckResult>.ValidationFailed(
                "Children", $"A maximum of {maxChildren} children can be checked per request.");
        }

        var request = new EnrollmentCheckRequest
        {
            Children = command.Children.Select(c => new ChildCheckRequest
            {
                CheckId = Guid.NewGuid(),
                FirstName = c.FirstName,
                LastName = c.LastName,
                DateOfBirth = c.DateOfBirth,
                SchoolName = c.SchoolName,
                SchoolCode = c.SchoolCode,
                AdditionalFields = c.AdditionalFields
            }).ToList()
        };

        EnrollmentCheckResult result;
        try
        {
            result = await enrollmentCheckService.CheckEnrollmentAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Enrollment check plugin failed");
            return Result<EnrollmentCheckResult>.DependencyFailed(
                DependencyFailedReason.ConnectionFailed,
                "Enrollment check service is temporarily unavailable.");
        }

        // Log de-identified submission (fire and forget, don't fail the request)
        try
        {
            var submission = new EnrollmentCheckSubmission
            {
                SubmissionId = Guid.NewGuid(),
                CheckedAtUtc = DateTime.UtcNow,
                ChildrenChecked = result.Results.Count,
                IpAddressHash = HashIpAddress(command.IpAddress),
                ChildResults = result.Results.Select(r => new DeidentifiedChildResult
                {
                    BirthYear = r.DateOfBirth.Year,
                    Status = r.Status.ToString(),
                    EligibilityType = r.EligibilityType?.ToString(),
                    SchoolName = r.SchoolName
                }).ToList()
            };

            await submissionLogger.LogSubmissionAsync(submission, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to log enrollment check submission (non-fatal)");
        }

        return Result<EnrollmentCheckResult>.Success(result);
    }

    private static string? HashIpAddress(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            return null;

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(ipAddress));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
