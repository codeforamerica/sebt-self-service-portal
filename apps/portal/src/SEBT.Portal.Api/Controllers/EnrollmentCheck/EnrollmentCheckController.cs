using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using SEBT.Portal.Api.Models;
using SEBT.Portal.Api.Models.EnrollmentCheck;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.StateConnector;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.AspNetCore;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.UseCases.EnrollmentCheck;

namespace SEBT.Portal.Api.Controllers.EnrollmentCheck;

/// <summary>
/// Controller for checking child enrollment in Summer EBT benefits.
/// This is a public, unauthenticated endpoint with rate limiting.
/// </summary>
[ApiController]
[Route("api/enrollment")]
public class EnrollmentCheckController : ControllerBase
{
    /// <summary>
    /// Checks enrollment status for one or more children.
    /// This is a public, unauthenticated endpoint.
    /// </summary>
    [HttpPost("check")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.EnrollmentCheck)]
    [ProducesResponseType(typeof(EnrollmentCheckApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CheckEnrollment(
        [FromServices] ICommandHandler<CheckEnrollmentCommand, EnrollmentCheckResult> handler,
        [FromBody] EnrollmentCheckApiRequest request,
        CancellationToken cancellationToken = default)
    {
        // Parse and validate date formats
        var children = new List<CheckEnrollmentCommand.ChildInput>();
        for (var i = 0; i < request.Children.Count; i++)
        {
            var child = request.Children[i];
            if (!DateOnly.TryParse(child.DateOfBirth, out var dob))
            {
                return BadRequest(new ErrorResponse(
                    $"Invalid date format for child at position {i + 1}. Expected yyyy-MM-dd."));
            }

            children.Add(new CheckEnrollmentCommand.ChildInput
            {
                FirstName = child.FirstName,
                LastName = child.LastName,
                DateOfBirth = dob,
                SchoolName = child.SchoolName,
                SchoolCode = child.SchoolCode,
                AdditionalFields = child.AdditionalFields
            });
        }

        var command = new CheckEnrollmentCommand
        {
            Children = children,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        var result = await handler.Handle(command, cancellationToken);

        return result.ToActionResult(
            successMap: data => Ok(MapToApiResponse(data)),
            failureMap: r => r switch
            {
                DependencyFailedResult<EnrollmentCheckResult> =>
                    StatusCode(StatusCodes.Status503ServiceUnavailable,
                        new ProblemDetails
                        {
                            Title = "Enrollment check service is temporarily unavailable.",
                            Status = StatusCodes.Status503ServiceUnavailable
                        }),
                _ => result.ToActionResult()
            });
    }

    /// <summary>
    /// Returns runtime feature state for the standalone enrollment checker app
    /// (the maintenance banner toggle with its per-language copy, and the outage
    /// page state). This is a public, unauthenticated endpoint. In static-hosting
    /// deployments (e.g. CO) the checker has no server of its own, so it polls this
    /// endpoint at runtime, which is what lets these be toggled via AWS AppConfig
    /// without a checker redeploy.
    /// </summary>
    /// <param name="featureManager">Feature manager resolving the banner toggle.</param>
    /// <param name="settings">Enrollment checker settings (banner copy).</param>
    /// <param name="outagePageStateResolver">Resolves the checker's outage page state (schedule + manual flag).</param>
    /// <returns>An OK result with the checker feature state.</returns>
    /// <response code="200">Returns the current checker feature state.</response>
    [HttpGet("features")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.CheckerFeatures)]
    [Produces("application/json")]
    [ProducesResponseType(typeof(EnrollmentCheckerFeaturesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetFeatures(
        [FromServices] IFeatureManager featureManager,
        [FromServices] IOptionsMonitor<EnrollmentCheckerSettings> settings,
        [FromServices] IOutagePageStateResolver outagePageStateResolver)
    {
        var bannerEnabled = await featureManager.IsEnabledAsync(FeatureFlags.EnableCheckerMaintenanceBanner);

        // IOptionsMonitor.CurrentValue (not IOptions) so AWS AppConfig hot-reloads
        // take effect without an app restart.
        var message = settings.CurrentValue.MaintenanceBanner.Message;

        var outagePageEnabled = (await outagePageStateResolver.ResolveAsync(OutageTarget.EnrollmentChecker)).IsActive;

        var incomeEligibilityEnabled =
            await featureManager.IsEnabledAsync(FeatureFlags.EnableCheckerIncomeEligibility);
        var incomeEligibility = settings.CurrentValue.IncomeEligibility;

        var applyEnabled = await featureManager.IsEnabledAsync(FeatureFlags.EnableApply);

        var enrollmentEnabled = await featureManager.IsEnabledAsync(FeatureFlags.EnableEnrollment);

        return Ok(new EnrollmentCheckerFeaturesResponse
        {
            MaintenanceBanner = new MaintenanceBannerFeature
            {
                Enabled = bannerEnabled,
                Message = message
            },
            OutagePage = new OutagePageFeature
            {
                Enabled = outagePageEnabled
            },
            IncomeEligibility = incomeEligibilityEnabled && incomeEligibility.IsConfigured
                ? new IncomeEligibilityFeature
                {
                    BaseThreshold = incomeEligibility.BaseThreshold,
                    PerMemberIncrement = incomeEligibility.PerMemberIncrement,
                    MaxHouseholdSize = incomeEligibility.MaxHouseholdSize
                }
                : null,
            Apply = new ApplyFeature
            {
                Enabled = applyEnabled
            },
            Enrollment = new EnrollmentFeature
            {
                Enabled = enrollmentEnabled
            }
        });
    }

    private static EnrollmentCheckApiResponse MapToApiResponse(EnrollmentCheckResult result)
    {
        return new EnrollmentCheckApiResponse
        {
            Results = result.Results.Select(r => new ChildCheckApiResponse
            {
                CheckId = r.CheckId.ToString(),
                FirstName = r.FirstName,
                LastName = r.LastName,
                DateOfBirth = r.DateOfBirth.ToString("yyyy-MM-dd"),
                Status = r.Status.ToString(),
                MatchConfidence = r.MatchConfidence,
                EligibilityType = r.EligibilityType?.ToString(),
                SchoolName = r.SchoolName,
                StatusMessage = r.StatusMessage
            }).ToList(),
            Message = result.ResponseMessage
        };
    }
}
