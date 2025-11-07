namespace SEBT.Portal.Kernel.AspNetCore;

using System.Net;
using SEBT.Portal.Kernel.Results;
using Microsoft.AspNetCore.Mvc;
using UnauthorizedResult = SEBT.Portal.Kernel.Results.UnauthorizedResult;

/// <summary>
/// Provides extension methods for converting <see cref="Result"/> objects to ASP.NET Core MVC <see cref="IActionResult"/> objects.
/// </summary>
public static class MvcResultExtensions
{
    /// <summary>
    /// Converts a <see cref="Result"/> into an appropriate <see cref="IActionResult"/>
    /// that represents the result to be sent in an HTTP response.
    /// </summary>
    /// <param name="result">The <see cref="Result"/> to be converted.</param>
    /// <param name="useProblemDetails">Whether to use a RFC7807 problem details body for a failure response. The default is <c>true</c>.</param>
    /// <returns>
    /// An <see cref="IActionResult"/> representing the HTTP response:
    /// <list type="bullet">
    /// <item><see cref="NoContentResult"/> for <see cref="SuccessResult"/>.</item>
    /// <item><see cref="ObjectResult"/> with <see cref="ProblemDetails"/> for any non-successful result if <paramref name="useProblemDetails"/> is <c>true</c>.</item>
    /// <item><see cref="NotFoundResult"/> for a <see cref="PreconditionFailedResult"/> indicating <see cref="PreconditionFailedReason.NotFound"/> if <paramref name="useProblemDetails"/> is <c>false</c>.</item>
    /// <item><see cref="StatusCodeResult"/> with HTTP status code 412 for a <see cref="PreconditionFailedResult"/> indicating <see cref="PreconditionFailedReason.ConcurrencyMismatch"/> if <paramref name="useProblemDetails"/> is <c>false</c>.</item>
    /// <item><see cref="ConflictResult"/> for a <see cref="PreconditionFailedResult"/> indicating <see cref="PreconditionFailedReason.Conflict"/> if <paramref name="useProblemDetails"/> is <c>false</c>.</item>
    /// <item><see cref="BadRequestObjectResult"/> with model state populated for a <see cref="ValidationFailedResult"/> if <paramref name="useProblemDetails"/> is <c>false</c>.</item>
    /// <item><see cref="ForbidResult"/> in case of an <see cref="UnauthorizedResult"/> if <paramref name="useProblemDetails"/> is <c>false</c>.</item>
    /// </list>
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the <paramref name="result"/> does not match any known result types.
    /// </exception>
    public static IActionResult ToActionResult(this Result result, bool useProblemDetails = true)
        => result switch
        {
            SuccessResult
                => new NoContentResult(),
            AggregateResult { IsSuccess: true }
                => new NoContentResult(),
            AggregateResult { IsSuccess: false, Results.Count: > 0 } aggregateResult
                => aggregateResult.Results.First(i => !i.IsSuccess).ToActionResult(useProblemDetails),
            PreconditionFailedResult { Reason: PreconditionFailedReason.NotFound } when useProblemDetails
                => result.ToProblemDetailsResult(HttpStatusCode.NotFound),
            PreconditionFailedResult { Reason: PreconditionFailedReason.NotFound } when !useProblemDetails
                => new NotFoundResult(),
            PreconditionFailedResult { Reason: PreconditionFailedReason.ConcurrencyMismatch } when useProblemDetails
                => result.ToProblemDetailsResult(HttpStatusCode.PreconditionFailed),
            PreconditionFailedResult { Reason: PreconditionFailedReason.ConcurrencyMismatch } when !useProblemDetails
                => new StatusCodeResult((int) HttpStatusCode.PreconditionFailed),
            PreconditionFailedResult { Reason: PreconditionFailedReason.Conflict } when useProblemDetails
                => result.ToProblemDetailsResult(HttpStatusCode.Conflict),
            PreconditionFailedResult { Reason: PreconditionFailedReason.Conflict } when !useProblemDetails
                => new ConflictResult(),
            ValidationFailedResult validationFailed when useProblemDetails
                => new ObjectResult(new ValidationProblemDetails(validationFailed.Errors.ToModelState())
                {
                    Title = result.Message,
                    Status = (int) HttpStatusCode.BadRequest,
                })
                {
                    StatusCode = (int) HttpStatusCode.BadRequest,
                },
            ValidationFailedResult validationFailed when !useProblemDetails
                => new BadRequestObjectResult(validationFailed.Errors.ToModelState()),
            UnauthorizedResult when useProblemDetails
                => result.ToProblemDetailsResult(HttpStatusCode.Forbidden),
            UnauthorizedResult when !useProblemDetails
                => new ForbidResult(),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };

    /// <summary>
    /// Converts a <see cref="Result{T}"/> into an appropriate <see cref="IActionResult"/>
    /// that represents the result to be sent in an HTTP response.
    /// </summary>
    /// <typeparam name="T">The type of the value contained in the result, if successful.</typeparam>
    /// <param name="result">The result instance to convert.</param>
    /// <param name="successMap">
    /// An optional function to map a successful result to a custom <see cref="IActionResult"/>.
    /// If not provided, a default mapping is applied.
    /// </param>
    /// <param name="useProblemDetails">Whether to use a RFC7807 problem details body for a failure response. The default is <c>true</c>.</param>
    /// <returns>
    /// An <see cref="IActionResult"/> representing the HTTP response:
    /// <list type="bullet">
    /// <item>The result of <paramref name="successMap"/>, if specified, for a <see cref="SuccessResult{T}"/>.</item>
    /// <item><see cref="OkObjectResult"/> for a <see cref="SuccessResult{T}"/>, when <paramref name="successMap"/> is not specified.</item>
    /// <item><see cref="ObjectResult"/> with <see cref="ProblemDetails"/> for any non-successful result if <paramref name="useProblemDetails"/> is <c>true</c>.</item>
    /// <item><see cref="NotFoundResult"/> for a <see cref="PreconditionFailedResult{T}"/> indicating <see cref="PreconditionFailedReason.NotFound"/> if <paramref name="useProblemDetails"/> is <c>false</c>.</item>
    /// <item><see cref="StatusCodeResult"/> with HTTP status code 412 for a <see cref="PreconditionFailedResult{T}"/> indicating <see cref="PreconditionFailedReason.ConcurrencyMismatch"/> if <paramref name="useProblemDetails"/> is <c>false</c>.</item>
    /// <item><see cref="ConflictResult"/> for a <see cref="PreconditionFailedResult{T}"/> indicating <see cref="PreconditionFailedReason.Conflict"/> if <paramref name="useProblemDetails"/> is <c>false</c>.</item>
    /// <item><see cref="BadRequestObjectResult"/> with model state populated for a <see cref="ValidationFailedResult{T}"/> if <paramref name="useProblemDetails"/> is <c>false</c>.</item>
    /// <item><see cref="ForbidResult"/> in case of an <see cref="UnauthorizedResult{T}"/> if <paramref name="useProblemDetails"/> is <c>false</c>.</item>
    /// </list>
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the <paramref name="result"/> does not match any known result types.
    /// </exception>
    public static IActionResult ToActionResult<T>(this Result<T> result, Func<T, IActionResult>? successMap = null, bool useProblemDetails = true)
        => result switch
        {
            SuccessResult<T> success when successMap != null
                => successMap(success.Value),
            SuccessResult<T> success
                => new OkObjectResult(success.Value),
            PreconditionFailedResult<T> { Reason: PreconditionFailedReason.NotFound } when useProblemDetails
                => result.ToProblemDetailsResult(HttpStatusCode.NotFound),
            PreconditionFailedResult<T> { Reason: PreconditionFailedReason.NotFound } when !useProblemDetails
                => new NotFoundResult(),
            PreconditionFailedResult<T> { Reason: PreconditionFailedReason.ConcurrencyMismatch } when useProblemDetails
                => result.ToProblemDetailsResult(HttpStatusCode.PreconditionFailed),
            PreconditionFailedResult<T> { Reason: PreconditionFailedReason.ConcurrencyMismatch } when !useProblemDetails
                => new StatusCodeResult((int) HttpStatusCode.PreconditionFailed),
            PreconditionFailedResult<T> { Reason: PreconditionFailedReason.Conflict } when useProblemDetails
                => result.ToProblemDetailsResult(HttpStatusCode.Conflict),
            PreconditionFailedResult<T> { Reason: PreconditionFailedReason.Conflict } when !useProblemDetails
                => new ConflictResult(),
            ValidationFailedResult<T> validationFailed when useProblemDetails
                => new ObjectResult(new ValidationProblemDetails(validationFailed.Errors.ToModelState())
                {
                    Title = result.Message,
                    Status = (int) HttpStatusCode.BadRequest,
                })
                {
                    StatusCode = (int) HttpStatusCode.BadRequest,
                },
            ValidationFailedResult<T> validationFailed when !useProblemDetails
                => new BadRequestObjectResult(validationFailed.Errors.ToModelState()),
            UnauthorizedResult<T> when useProblemDetails
                => result.ToProblemDetailsResult(HttpStatusCode.Forbidden),
            UnauthorizedResult<T> when !useProblemDetails
                => new ForbidResult(),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };

    /// <summary>
    /// Converts a <see cref="Result"/> into a <see cref="ObjectResult"/> containing <see cref="ProblemDetails"/>.
    /// </summary>
    /// <param name="result">The <see cref="Result"/> to be converted.</param>
    /// <param name="statusCode">The HTTP status code to be set in the <see cref="ProblemDetails"/>.</param>
    /// <returns>A <see cref="ObjectResult"/> containing the <see cref="ProblemDetails"/>.</returns>
    /// <remarks>
    /// This is primarily an internal API, intended to be used from <see cref="ToActionResult"/> or <see cref="ToActionResult{T}"/>.
    /// However, it might have some desired use cases where direct usage is appropriate, so it is made public.
    /// </remarks>
    public static IActionResult ToProblemDetailsResult(this Result result, HttpStatusCode statusCode)
        => new ObjectResult(new ProblemDetails
        {
            Title = result.Message,
            Status = (int)statusCode,
        })
        {
            StatusCode = (int)statusCode,
        };
}