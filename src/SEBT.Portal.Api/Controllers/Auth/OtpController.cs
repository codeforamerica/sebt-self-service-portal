using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using SEBT.Portal.Api.Models;
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
public class OtpController(ILogger<OtpController> logger) : ControllerBase
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
    /// <returns>An OK result with a JWT token if the OTP is valid; otherwise, a BadRequest result.</returns>
    /// <response code="200">OTP validated successfully. Returns a JWT token.</response>
    /// <response code="400">Invalid OTP or request.</response>
    /// <response code="500">An error occurred while generating the authentication token.</response>
    [HttpPost("validate")]
    [EnableRateLimiting(RateLimitPolicies.Otp)]
    [ProducesResponseType(typeof(ValidateOtpResponse), StatusCodes.Status200OK)]
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
            return Ok(new ValidateOtpResponse(result.Value));
        }
        else
        {
            logger.LogWarning("OTP validation failed for email {Email}: {Message}", command.Email, result.Message);
            return BadRequest(new ErrorResponse(result.Message));
        }
    }
}
