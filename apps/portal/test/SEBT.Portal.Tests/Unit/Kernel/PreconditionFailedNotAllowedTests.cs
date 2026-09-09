using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.AspNetCore;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.Tests.Unit.Kernel;

/// <summary>
/// Verifies that PreconditionFailedReason.NotAllowed maps to HTTP 412 in both
/// MVC and Minimal API result extensions. 412 keeps the reason in the
/// precondition-failed family alongside NotFound/ConcurrencyMismatch/Conflict
/// and avoids colliding with the frontend's 403-insufficient-IAL interceptor.
/// </summary>
public class PreconditionFailedNotAllowedTests
{
    [Fact]
    public void NotAllowed_HasExpectedEnumValue()
    {
        Assert.Equal(4, (int)PreconditionFailedReason.NotAllowed);
    }

    [Fact]
    public void NotAllowed_ToMessage_ReturnsExpectedText()
    {
        var result = Result.PreconditionFailed(PreconditionFailedReason.NotAllowed);
        Assert.Equal("The requested action is not permitted for this account.", result.Message);
    }

    [Fact]
    public void NotAllowed_ToMessage_CustomMessageOverridesDefault()
    {
        var result = Result.PreconditionFailed(PreconditionFailedReason.NotAllowed, "Address updates are not available.");
        Assert.Equal("Address updates are not available.", result.Message);
    }

    // MVC extension tests

    [Fact]
    public void MvcResult_NotAllowed_WithProblemDetails_Returns412()
    {
        var result = Result.PreconditionFailed(PreconditionFailedReason.NotAllowed);
        var actionResult = result.ToActionResult(useProblemDetails: true);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(412, objectResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(412, problemDetails.Status);
    }

    [Fact]
    public void MvcResult_NotAllowed_WithoutProblemDetails_Returns412StatusCode()
    {
        var result = Result.PreconditionFailed(PreconditionFailedReason.NotAllowed);
        var actionResult = result.ToActionResult(useProblemDetails: false);

        var statusCodeResult = Assert.IsType<StatusCodeResult>(actionResult);
        Assert.Equal(412, statusCodeResult.StatusCode);
    }

    [Fact]
    public void MvcResultT_NotAllowed_WithProblemDetails_Returns412()
    {
        var result = Result<string>.PreconditionFailed(PreconditionFailedReason.NotAllowed);
        var actionResult = result.ToActionResult<string>(useProblemDetails: true);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(412, objectResult.StatusCode);
    }

    [Fact]
    public void MvcResultT_NotAllowed_WithoutProblemDetails_Returns412StatusCode()
    {
        var result = Result<string>.PreconditionFailed(PreconditionFailedReason.NotAllowed);
        var actionResult = result.ToActionResult<string>(useProblemDetails: false);

        var statusCodeResult = Assert.IsType<StatusCodeResult>(actionResult);
        Assert.Equal(412, statusCodeResult.StatusCode);
    }

    // Minimal API extension tests

    [Fact]
    public void MinimalApi_NotAllowed_WithProblemDetails_Returns412()
    {
        var result = Result.PreconditionFailed(PreconditionFailedReason.NotAllowed);
        var apiResult = result.ToMinimalApiResult(useProblemDetails: true);

        var problemResult = Assert.IsType<ProblemHttpResult>(apiResult);
        Assert.Equal(412, problemResult.StatusCode);
        Assert.Equal(412, problemResult.ProblemDetails.Status);
    }

    [Fact]
    public void MinimalApi_NotAllowed_WithoutProblemDetails_Returns412StatusCode()
    {
        var result = Result.PreconditionFailed(PreconditionFailedReason.NotAllowed);
        var apiResult = result.ToMinimalApiResult(useProblemDetails: false);

        var statusResult = Assert.IsType<StatusCodeHttpResult>(apiResult);
        Assert.Equal(412, statusResult.StatusCode);
    }

    [Fact]
    public void MinimalApiT_NotAllowed_WithProblemDetails_Returns412()
    {
        var result = Result<string>.PreconditionFailed(PreconditionFailedReason.NotAllowed);
        var apiResult = result.ToMinimalApiResult<string>(useProblemDetails: true);

        var problemResult = Assert.IsType<ProblemHttpResult>(apiResult);
        Assert.Equal(412, problemResult.StatusCode);
        Assert.Equal(412, problemResult.ProblemDetails.Status);
    }

    [Fact]
    public void MinimalApiT_NotAllowed_WithoutProblemDetails_Returns412StatusCode()
    {
        var result = Result<string>.PreconditionFailed(PreconditionFailedReason.NotAllowed);
        var apiResult = result.ToMinimalApiResult<string>(useProblemDetails: false);

        var statusResult = Assert.IsType<StatusCodeHttpResult>(apiResult);
        Assert.Equal(412, statusResult.StatusCode);
    }
}
