using Microsoft.AspNetCore.Mvc;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.AspNetCore;
using SEBT.Portal.UseCases.Auth;

namespace SEBT.Portal.Api.Controllers;

[ApiController]
[Route("api/auth/otp")]
public class OtpController() : ControllerBase
{

    [HttpPost("request")]
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
            return BadRequest(new {result.Message});
        }

    }

    [HttpPost("validate")]
    public async Task<IActionResult> ValidateOtp(
    [FromBody] ValidateOtpCommand command,
    [FromServices] ICommandHandler<ValidateOtpCommand> handler)
    {

        var result = await handler.Handle(command);

        if(result.IsSuccess)
        {
            return Ok();
        }
        else
        {
            return BadRequest(new {result.Message});
        }
    }
}
