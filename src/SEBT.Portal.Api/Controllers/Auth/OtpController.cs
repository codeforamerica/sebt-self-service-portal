using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.Api.Models;
using SEBT.Portal.Api.Services;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.AspNetCore;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.UseCases.Auth;

namespace SEBT.Portal.Api.Controllers;

/// <summary>
/// Controller for handling one-time password (OTP) requests and validations.
/// </summary>
[ApiController]
[Route("api/auth/otp")]
public class OtpController(
    ILogger<OtpController> logger,
    IOptions<JwtSettings> jwtSettingsOptions) : ControllerBase
{
    /// <summary>
    /// Request a one-time password (OTP) to be sent to the specified email address.
    /// </summary>
    /// <param name="command">The command containing the email address.</param>
    /// <param name="handler">The command handler for processing the OTP request.</param>
    /// <returns>A Created result if the OTP was sent successfully; otherwise, a BadRequest result.</returns>
    /// <response code="201">OTP requested successfully.</response>
    /// <response code="400">Invalid request.</response>
    /// <response code="429">Rate limit exceeded. Maximum 5 OTP requests per minute allowed.</response>
    [HttpPost("request")]
    [EnableRateLimiting(RateLimitPolicies.Otp)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RequestOtp(
        [FromBody] RequestOtpCommand command,
        [FromServices] ICommandHandler<RequestOtpCommand> handler)
    {
        if (command == null)
        {
            return BadRequest(new ErrorResponse("Request body is required."));
        }

        logger.LogInformation("OTP request received for email {Email}", command.Email);

        var result = await handler.Handle(command);

        if (result.IsSuccess)
        {
            return Created();
        }
        else
        {
            return BadRequest(new ErrorResponse(result.Message));
        }

    }

    /// <summary>
    /// Validate a one-time password (OTP) for the specified email address.
    /// </summary>
    /// <param name="command">The command containing the email address and OTP code.</param>
    /// <param name="handler">The command handler for processing the OTP validation.</param>
    /// <returns>204 No Content on success; the session JWT is set via HttpOnly cookie.</returns>
    /// <response code="204">OTP validated successfully. Session cookie set; no body.</response>
    /// <response code="400">Invalid OTP or request.</response>
    /// <response code="500">An error occurred while generating the authentication token.</response>
    [HttpPost("validate")]
    [EnableRateLimiting(RateLimitPolicies.Otp)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ValidateOtp(
        [FromBody] ValidateOtpCommand command,
        [FromServices] ICommandHandler<ValidateOtpCommand, string> handler)
    {
        if (command == null)
        {
            return BadRequest(new ErrorResponse("Request body is required."));
        }

        logger.LogInformation("OTP validation request received for email {Email}", command.Email);

        var result = await handler.Handle(command);

        if (result.IsSuccess)
        {
            logger.LogInformation("JWT token generated successfully for email {Email}", command.Email);
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(jwtSettingsOptions.Value.ExpirationMinutes);
            AuthCookies.SetAuthCookie(Response, result.Value, expiresAt);
            return NoContent();
        }
        else
        {
            logger.LogWarning("OTP validation failed for email {Email}: {Message}", command.Email, result.Message);
            return BadRequest(new ErrorResponse(result.Message));
        }
    }
}
