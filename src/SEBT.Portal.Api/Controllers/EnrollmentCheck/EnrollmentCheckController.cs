using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SEBT.Portal.Api.Models;
using SEBT.Portal.Api.Models.EnrollmentCheck;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.AspNetCore;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;
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
