using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SEBT.Portal.Api.Models;
using SEBT.Portal.Core.Repositories;

namespace SEBT.Portal.Api.Filters;

/// <summary>
/// Action filter that resolves the authenticated user's numeric ID from their email claim
/// and stashes it on HttpContext.Items. Controllers read the pre-resolved ID, keeping PII
/// (email) out of controller code entirely.
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
        var email = context.HttpContext.User.FindFirst(ClaimTypes.Email)?.Value
            ?? context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.HttpContext.User.Identity?.Name;

        if (string.IsNullOrWhiteSpace(email))
        {
            logger.LogWarning("Request to resolved-user endpoint but email could not be extracted from claims");
            context.Result = new UnauthorizedObjectResult(
                new ErrorResponse("Unable to identify user from token."));
            return;
        }

        var user = await userRepository.GetUserByEmailAsync(email, context.HttpContext.RequestAborted);
        if (user == null)
        {
            logger.LogWarning("Request to resolved-user endpoint but authenticated user not found in database");
            context.Result = new UnauthorizedObjectResult(
                new ErrorResponse("Unable to identify user from token."));
            return;
        }

        context.HttpContext.Items[UserIdKey] = user.Id;
        await next();
    }
}
