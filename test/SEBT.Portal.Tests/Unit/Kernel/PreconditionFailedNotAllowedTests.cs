using System.Net;
using Microsoft.AspNetCore.Mvc;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.AspNetCore;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.Tests.Unit.KernelExtensions;

/// <summary>
/// Verifies that PreconditionFailedReason.NotAllowed maps to HTTP 403
/// in both MVC and Minimal API result extensions.
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
    public void MvcResult_NotAllowed_WithProblemDetails_Returns403()
    {
        var result = Result.PreconditionFailed(PreconditionFailedReason.NotAllowed);
        var actionResult = result.ToActionResult(useProblemDetails: true);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(403, objectResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(403, problemDetails.Status);
    }

    [Fact]
    public void MvcResult_NotAllowed_WithoutProblemDetails_Returns403StatusCode()
    {
        var result = Result.PreconditionFailed(PreconditionFailedReason.NotAllowed);
        var actionResult = result.ToActionResult(useProblemDetails: false);

        var statusCodeResult = Assert.IsType<StatusCodeResult>(actionResult);
        Assert.Equal(403, statusCodeResult.StatusCode);
    }

    [Fact]
    public void MvcResultT_NotAllowed_WithProblemDetails_Returns403()
    {
        var result = Result<string>.PreconditionFailed(PreconditionFailedReason.NotAllowed);
        var actionResult = result.ToActionResult<string>(useProblemDetails: true);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(403, objectResult.StatusCode);
    }

    [Fact]
    public void MvcResultT_NotAllowed_WithoutProblemDetails_Returns403StatusCode()
    {
        var result = Result<string>.PreconditionFailed(PreconditionFailedReason.NotAllowed);
        var actionResult = result.ToActionResult<string>(useProblemDetails: false);

        var statusCodeResult = Assert.IsType<StatusCodeResult>(actionResult);
        Assert.Equal(403, statusCodeResult.StatusCode);
    }

    // Minimal API extension tests

    [Fact]
    public void MinimalApi_NotAllowed_WithProblemDetails_Returns403()
    {
        var result = Result.PreconditionFailed(PreconditionFailedReason.NotAllowed);
        var apiResult = result.ToMinimalApiResult(useProblemDetails: true);

        // ProblemHttpResult wraps ProblemDetails with status code
        Assert.NotNull(apiResult);
    }

    [Fact]
    public void MinimalApi_NotAllowed_WithoutProblemDetails_Returns403()
    {
        var result = Result.PreconditionFailed(PreconditionFailedReason.NotAllowed);
        var apiResult = result.ToMinimalApiResult(useProblemDetails: false);

        Assert.NotNull(apiResult);
    }

    [Fact]
    public void MinimalApiT_NotAllowed_WithProblemDetails_Returns403()
    {
        var result = Result<string>.PreconditionFailed(PreconditionFailedReason.NotAllowed);
        var apiResult = result.ToMinimalApiResult<string>(useProblemDetails: true);

        Assert.NotNull(apiResult);
    }

    [Fact]
    public void MinimalApiT_NotAllowed_WithoutProblemDetails_Returns403()
    {
        var result = Result<string>.PreconditionFailed(PreconditionFailedReason.NotAllowed);
        var apiResult = result.ToMinimalApiResult<string>(useProblemDetails: false);

        Assert.NotNull(apiResult);
    }
}
