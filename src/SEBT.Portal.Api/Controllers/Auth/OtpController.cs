using Microsoft.AspNetCore.Mvc;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.AspNetCore;
using SEBT.Portal.UseCases.Auth;

namespace SEBT.Portal.Api.Controllers;

/// <summary>
/// Controller for handling one-time password (OTP) requests and validations.
/// </summary>  
[ApiController]
[Route("api/auth/otp")]
public class OtpController() : ControllerBase
{
    /// <summary>
    /// Request a one-time password (OTP) to be sent to the specified email address.
    /// </summary>
    /// <param name="command">The command containing the email address.</param>
    /// <returns>A Created result if the OTP was sent successfully; otherwise, a BadRequest result.</returns>
    /// <response code="201">OTP requested successfully.</response>
    /// <response code="400">Invalid request.</response>
    [HttpPost("request")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestOtp(
        [FromBody] RequestOtpCommand command,
        [FromServices] ICommandHandler<RequestOtpCommand> handler)
    {

        var result = await handler.Handle(command);

        if (result.IsSuccess)
        {
            return Created();
        }
        else
        {
            return BadRequest(new { Error = result.Message });
        }

    }

    /// <summary>
    /// Validate a one-time password (OTP) for the specified email address.
    /// </summary>
    /// <param name="command">The command containing the email address and OTP code.</param>
    /// <returns>An OK result if the OTP is valid; otherwise, a BadRequest result.</returns>
    /// <response code="200">OTP validated successfully.</response>
    /// <response code="400">Invalid OTP or request.</response>
    [HttpPost("validate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ValidateOtp(
    [FromBody] ValidateOtpCommand command,
    [FromServices] ICommandHandler<ValidateOtpCommand> handler)
    {

        var result = await handler.Handle(command);

        if (result.IsSuccess)
        {
            return Ok();
        }
        else
        {
            return BadRequest(new { result.Message });
        }
    }
}
