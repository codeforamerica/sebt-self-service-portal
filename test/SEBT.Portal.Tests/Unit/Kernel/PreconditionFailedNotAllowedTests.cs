namespace SEBT.Portal.Tests.Unit.KernelTests;

using System.Net;
using Microsoft.AspNetCore.Mvc;
using SEBT.Portal.Kernel.AspNetCore;
using SEBT.Portal.Kernel.Results;

public class PreconditionFailedNotAllowedTests
{
    [Fact]
    public void NotAllowed_HasValue4()
    {
        Assert.Equal(4, (int)PreconditionFailedReason.NotAllowed);
    }

    // MVC result extensions

    [Fact]
    public void ToActionResult_NotAllowed_WithProblemDetails_Returns403()
    {
        var result = new PreconditionFailedResult(PreconditionFailedReason.NotAllowed, null);

        var actionResult = result.ToActionResult(useProblemDetails: true);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal((int)HttpStatusCode.Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public void ToActionResult_NotAllowed_WithoutProblemDetails_ReturnsForbid()
    {
        var result = new PreconditionFailedResult(PreconditionFailedReason.NotAllowed, null);

        var actionResult = result.ToActionResult(useProblemDetails: false);

        Assert.IsType<ForbidResult>(actionResult);
    }

    // Minimal API result extensions

    [Fact]
    public void ToMinimalApiResult_NotAllowed_WithProblemDetails_Returns403()
    {
        var result = new PreconditionFailedResult(PreconditionFailedReason.NotAllowed, null);

        var apiResult = result.ToMinimalApiResult(useProblemDetails: true);

        // Problem() returns ProblemHttpResult; check via StatusCode
        var statusCode = GetStatusCode(apiResult);
        Assert.Equal((int)HttpStatusCode.Forbidden, statusCode);
    }

    [Fact]
    public void ToMinimalApiResult_NotAllowed_WithoutProblemDetails_ReturnsForbid()
    {
        var result = new PreconditionFailedResult(PreconditionFailedReason.NotAllowed, null);

        var apiResult = result.ToMinimalApiResult(useProblemDetails: false);

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.ForbidHttpResult>(apiResult);
    }

    private static int? GetStatusCode(Microsoft.AspNetCore.Http.IResult result)
        => result switch
        {
            Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult p => p.StatusCode,
            Microsoft.AspNetCore.Http.HttpResults.StatusCodeHttpResult s => s.StatusCode,
            _ => null
        };
}
