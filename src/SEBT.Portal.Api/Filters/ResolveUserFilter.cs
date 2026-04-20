using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SEBT.Portal.Api.Models;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Utilities;

namespace SEBT.Portal.Api.Filters;

/// <summary>
/// Action filter that resolves the authenticated user's numeric ID from their JWT sub claim
/// and stashes it on HttpContext.Items. Controllers read the pre-resolved ID, keeping PII
/// (email) out of controller code entirely. Also verifies the user still exists in the DB.
/// </summary>
public class ResolveUserFilter(
    IUserRepository userRepository,
    ILogger<ResolveUserFilter> logger) : IAsyncActionFilter
{
    /// <summary>
    /// The HttpContext.Items key where the resolved user ID is stored.
    /// </summary>
    public const string UserIdKey = "ResolvedUserId";

    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // The portal JWT sub claim is our internal user ID (set by JwtTokenService).
        // This works uniformly for OIDC and OTP users — no email lookup required.
        var userId = context.HttpContext.User.GetUserId();
        if (userId == null)
        {
            logger.LogWarning("Request to resolved-user endpoint but sub claim missing or invalid");
            context.Result = new UnauthorizedObjectResult(
                new ErrorResponse("Unable to identify user from token."));
            return;
        }

        // Verify the user still exists (defense-in-depth: JWTs remain valid after deletion).
        var user = await userRepository.GetUserByIdAsync(userId.Value, context.HttpContext.RequestAborted);
        if (user == null)
        {
            logger.LogWarning(
                "Request to resolved-user endpoint but UserId {UserId} not found in database", userId);
            context.Result = new UnauthorizedObjectResult(
                new ErrorResponse("Unable to identify user from token."));
            return;
        }

        context.HttpContext.Items[UserIdKey] = user.Id;
        await next();
    }
}
