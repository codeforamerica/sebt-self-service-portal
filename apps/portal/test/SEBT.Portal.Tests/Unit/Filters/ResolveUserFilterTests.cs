using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.Api.Filters;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Repositories;

namespace SEBT.Portal.Tests.Unit.Filters;

public class ResolveUserFilterTests
{
    private readonly IUserRepository userRepository = Substitute.For<IUserRepository>();

    private readonly NullLogger<ResolveUserFilter> logger =
        NullLogger<ResolveUserFilter>.Instance;

    private ResolveUserFilter CreateFilter() => new(userRepository, logger);

    private static ActionExecutingContext CreateContext(ClaimsPrincipal? user = null)
    {
        var httpContext = new DefaultHttpContext();
        if (user != null)
        {
            httpContext.User = user;
        }

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());

        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller: null!);
    }

    private static ClaimsPrincipal CreateAuthenticatedUserWithSub(string sub)
    {
        var claims = new List<Claim> { new("sub", sub) };
        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task OnActionExecutionAsync_ShouldSetUserId_WhenSubClaimResolvesToUser()
    {
        var filter = CreateFilter();
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "test@example.com" };
        userRepository.GetUserByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(user);

        var context = CreateContext(CreateAuthenticatedUserWithSub(userId.ToString()));
        var nextCalled = false;

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(
                context, new List<IFilterMetadata>(), controller: null!));
        });

        Assert.True(nextCalled);
        Assert.Equal(userId, context.HttpContext.Items[ResolveUserFilter.UserIdKey]);
    }

    [Fact]
    public async Task OnActionExecutionAsync_ShouldReturn401_WhenNoSubClaim()
    {
        var filter = CreateFilter();
        var context = CreateContext(new ClaimsPrincipal(new ClaimsIdentity()));

        await filter.OnActionExecutionAsync(context, () =>
            throw new InvalidOperationException("Next should not be called"));

        var result = Assert.IsType<UnauthorizedObjectResult>(context.Result);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task OnActionExecutionAsync_ShouldReturn401_WhenSubClaimIsNotAGuid()
    {
        var filter = CreateFilter();
        var context = CreateContext(CreateAuthenticatedUserWithSub("notaguid"));

        await filter.OnActionExecutionAsync(context, () =>
            throw new InvalidOperationException("Next should not be called"));

        var result = Assert.IsType<UnauthorizedObjectResult>(context.Result);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task OnActionExecutionAsync_ShouldReturn401_WhenUserNotFoundInDatabase()
    {
        var filter = CreateFilter();
        var missingUserId = Guid.NewGuid();
        userRepository.GetUserByIdAsync(missingUserId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var context = CreateContext(CreateAuthenticatedUserWithSub(missingUserId.ToString()));

        await filter.OnActionExecutionAsync(context, () =>
            throw new InvalidOperationException("Next should not be called"));

        var result = Assert.IsType<UnauthorizedObjectResult>(context.Result);
        Assert.NotNull(result);
    }
}
