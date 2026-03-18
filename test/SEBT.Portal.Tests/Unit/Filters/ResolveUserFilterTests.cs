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

    private static ClaimsPrincipal CreateAuthenticatedUser(string email, string claimType = ClaimTypes.Email)
    {
        var claims = new List<Claim> { new(claimType, email) };
        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task OnActionExecutionAsync_ShouldSetUserId_WhenEmailClaimResolvesToUser()
    {
        var filter = CreateFilter();
        var user = new User { Id = 42, Email = "test@example.com" };
        userRepository.GetUserByEmailAsync("test@example.com", Arg.Any<CancellationToken>())
            .Returns(user);

        var context = CreateContext(CreateAuthenticatedUser("test@example.com"));
        var nextCalled = false;

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(
                context, new List<IFilterMetadata>(), controller: null!));
        });

        Assert.True(nextCalled);
        Assert.Equal(42, context.HttpContext.Items[ResolveUserFilter.UserIdKey]);
    }

    [Fact]
    public async Task OnActionExecutionAsync_ShouldReturn401_WhenNoEmailClaim()
    {
        var filter = CreateFilter();
        var context = CreateContext(new ClaimsPrincipal(new ClaimsIdentity()));

        await filter.OnActionExecutionAsync(context, () =>
            throw new InvalidOperationException("Next should not be called"));

        var result = Assert.IsType<UnauthorizedObjectResult>(context.Result);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task OnActionExecutionAsync_ShouldReturn401_WhenUserNotFoundInDatabase()
    {
        var filter = CreateFilter();
        userRepository.GetUserByEmailAsync("unknown@example.com", Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var context = CreateContext(CreateAuthenticatedUser("unknown@example.com"));

        await filter.OnActionExecutionAsync(context, () =>
            throw new InvalidOperationException("Next should not be called"));

        var result = Assert.IsType<UnauthorizedObjectResult>(context.Result);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task OnActionExecutionAsync_ShouldFallBackToNameIdentifier_WhenEmailClaimMissing()
    {
        var filter = CreateFilter();
        var user = new User { Id = 7, Email = "fallback@example.com" };
        userRepository.GetUserByEmailAsync("fallback@example.com", Arg.Any<CancellationToken>())
            .Returns(user);

        var context = CreateContext(CreateAuthenticatedUser("fallback@example.com", ClaimTypes.NameIdentifier));

        var nextCalled = false;
        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(
                context, new List<IFilterMetadata>(), controller: null!));
        });

        Assert.True(nextCalled);
        Assert.Equal(7, context.HttpContext.Items[ResolveUserFilter.UserIdKey]);
    }
}
