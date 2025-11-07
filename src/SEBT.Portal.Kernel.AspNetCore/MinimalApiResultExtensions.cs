namespace SEBT.Portal.Kernel.AspNetCore;

using System.Net;
using Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using UnauthorizedResult = Results.UnauthorizedResult;
using HttpResults = Microsoft.AspNetCore.Http.Results;

/// <summary>
/// Provides extension methods for converting <see cref="Result"/> objects to ASP.NET Core Minimal API <see cref="IResult"/> objects.
/// </summary>
public static class MinimalApiResultExtensions
{
    /// <summary>
    /// Converts a <see cref="Result"/> into an appropriate <see cref="IResult"/> to be used in a minimal API context.
    /// </summary>
    /// <param name="result">The <see cref="Result"/> to be converted.</param>
    /// <param name="useProblemDetails">Whether to use a RFC7807 problem details body for a failure response. The default is <c>true</c>.</param>
    /// <returns>
    /// An <see cref="IResult"/> that represents the appropriate response:
    /// <list type="bullet">
    /// <item><see cref="NoContent"/> for a <see cref="SuccessResult"/>.</item>
    /// <item><see cref="ProblemHttpResult"/> with RFC7807 <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails"/> for any non-successful result if <paramref name="useProblemDetails"/> is <c>true</c>.</item>
    /// <item><see cref="NotFound"/> for a <see cref="PreconditionFailedResult"/> with a reason of <see cref="PreconditionFailedReason.NotFound"/> if <paramref name="useProblemDetails"/> is <c>false</c>.</item>
    /// <item><see cref="StatusCodeHttpResult"/> with status code 412 for a <see cref="PreconditionFailedResult"/> with a reason of <see cref="PreconditionFailedReason.ConcurrencyMismatch"/> if <paramref name="useProblemDetails"/> is <c>false</c>.</item>
    /// <item><see cref="Conflict"/> for a <see cref="PreconditionFailedResult"/> with a reason of <see cref="PreconditionFailedReason.Conflict"/> if <paramref name="useProblemDetails"/> is <c>false</c>.</item>
    /// <item><see cref="BadRequest"/> with model state populated for a <see cref="ValidationFailedResult"/> if <paramref name="useProblemDetails"/> is <c>false</c>.</item>
    /// <item><see cref="ForbidHttpResult"/> for an <see cref="Kernel.Results.UnauthorizedResult"/> if <paramref name="useProblemDetails"/> is <c>false</c>.</item>
    /// </list>
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the <paramref name="result"/> does not match any known result types.
    /// </exception>
    public static IResult ToMinimalApiResult(this Result result, bool useProblemDetails = true)
        => result switch
        {
            SuccessResult
                => HttpResults.NoContent(),
            AggregateResult { IsSuccess: true }
                => HttpResults.NoContent(),
            AggregateResult { IsSuccess: false, Results.Count: > 0 } aggregateResult
                => aggregateResult.Results.First(i => !i.IsSuccess).ToMinimalApiResult(useProblemDetails),
            PreconditionFailedResult { Reason: PreconditionFailedReason.NotFound } when useProblemDetails
                => result.ToProblemHttpResult(HttpStatusCode.NotFound),
            PreconditionFailedResult { Reason: PreconditionFailedReason.NotFound } when !useProblemDetails
                => HttpResults.NotFound(),
            PreconditionFailedResult { Reason: PreconditionFailedReason.ConcurrencyMismatch } when useProblemDetails
                => result.ToProblemHttpResult(HttpStatusCode.PreconditionFailed),
            PreconditionFailedResult { Reason: PreconditionFailedReason.ConcurrencyMismatch } when !useProblemDetails
                => HttpResults.StatusCode((int) HttpStatusCode.PreconditionFailed),
            PreconditionFailedResult { Reason: PreconditionFailedReason.Conflict } when useProblemDetails
                => result.ToProblemHttpResult(HttpStatusCode.Conflict),
            PreconditionFailedResult { Reason: PreconditionFailedReason.Conflict } when !useProblemDetails
                => HttpResults.Conflict(),
            ValidationFailedResult validationFailed when useProblemDetails
                => HttpResults.ValidationProblem(errors: validationFailed.Errors.CreateErrorDictionary(), title: result.Message),
            ValidationFailedResult validationFailed when !useProblemDetails
                => HttpResults.BadRequest(validationFailed.Errors.ToModelState()),
            UnauthorizedResult when useProblemDetails
                => result.ToProblemHttpResult(HttpStatusCode.Forbidden),
            UnauthorizedResult when !useProblemDetails
                => HttpResults.Forbid(),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };

    /// <summary>
    /// Converts a <see cref="Result{T}"/> into an appropriate <see cref="IResult"/> to be used in a minimal API context.
    /// </summary>
    /// <param name="result">The <see cref="Result{T}"/> instance to be converted.</param>
    /// <param name="successMap">
    /// An optional function to map the value of a successful result into a user-defined <see cref="IResult"/>.
    /// If not provided, successful results will default to an HTTP 200 response with the value serialized as the body.
    /// </param>
    /// <param name="useProblemDetails">Whether to use a RFC7807 problem details body for a failure response. The default is <c>true</c>.</param>
    /// <typeparam name="T">The type of the result's value.</typeparam>
    /// <returns>
    /// An <see cref="IResult"/> that represents the appropriate response:
    /// <list type="bullet">
    /// <item><see cref="Ok"/> response for a <see cref="SuccessResult{T}"/> if <paramref name="successMap"/> is not provided.</item>
    /// <item>The result of <paramref name="successMap"/> if provided and the result is a <see cref="SuccessResult{T}"/>.</item>
    /// <item><see cref="ProblemHttpResult"/> with RFC7807 <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails"/> for any non-successful result if <paramref name="useProblemDetails"/> is <c>true</c>.</item>
    /// <item><see cref="NotFound"/> for a <see cref="PreconditionFailedResult{T}"/> with a reason of <see cref="PreconditionFailedReason.NotFound"/> if <paramref name="useProblemDetails"/> is <c>false</c>.</item>
    /// <item><see cref="StatusCodeHttpResult"/> with status code 412 for a <see cref="PreconditionFailedResult{T}"/> with a reason of <see cref="PreconditionFailedReason.ConcurrencyMismatch"/> if <paramref name="useProblemDetails"/> is <c>false</c>.</item>
    /// <item><see cref="Conflict"/> for a <see cref="PreconditionFailedResult{T}"/> with a reason of <see cref="PreconditionFailedReason.Conflict"/> if <paramref name="useProblemDetails"/> is <c>false</c>.</item>
    /// <item><see cref="BadRequest"/> with model state populated for a <see cref="ValidationFailedResult{T}"/> if <paramref name="useProblemDetails"/> is <c>false</c>.</item>
    /// <item><see cref="ForbidHttpResult"/> for an <see cref="UnauthorizedResult{T}"/> if <paramref name="useProblemDetails"/> is <c>false</c>.</item>
    /// </list>
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the <paramref name="result"/> does not match any known result types.
    /// </exception>
    public static IResult ToMinimalApiResult<T>(this Result<T> result, Func<T, IResult>? successMap = null, bool useProblemDetails = true)
        => result switch
        {
            SuccessResult<T> success when successMap != null
                => successMap(success.Value),
            SuccessResult<T> success
                => HttpResults.Ok(success.Value),
            PreconditionFailedResult<T> { Reason: PreconditionFailedReason.NotFound } when useProblemDetails
                => result.ToProblemHttpResult(HttpStatusCode.NotFound),
            PreconditionFailedResult<T> { Reason: PreconditionFailedReason.NotFound } when !useProblemDetails
                => HttpResults.NotFound(),
            PreconditionFailedResult<T> { Reason: PreconditionFailedReason.ConcurrencyMismatch } when useProblemDetails
                => result.ToProblemHttpResult(HttpStatusCode.PreconditionFailed),
            PreconditionFailedResult<T> { Reason: PreconditionFailedReason.ConcurrencyMismatch } when !useProblemDetails
                => HttpResults.StatusCode((int)HttpStatusCode.PreconditionFailed),
            PreconditionFailedResult<T> { Reason: PreconditionFailedReason.Conflict } when useProblemDetails
                => result.ToProblemHttpResult(HttpStatusCode.Conflict),
            PreconditionFailedResult<T> { Reason: PreconditionFailedReason.Conflict } when !useProblemDetails
                => HttpResults.Conflict(),
            ValidationFailedResult<T> validationFailed when useProblemDetails
                => HttpResults.ValidationProblem(errors: validationFailed.Errors.CreateErrorDictionary(), title: result.Message),
            ValidationFailedResult<T> validationFailed when !useProblemDetails
                => HttpResults.BadRequest(validationFailed.Errors.ToModelState()),
            UnauthorizedResult<T> when useProblemDetails
                => result.ToProblemHttpResult(HttpStatusCode.Forbidden),
            UnauthorizedResult<T> when !useProblemDetails
                => HttpResults.Forbid(),
            _ => throw new ArgumentOutOfRangeException(nameof(result)),
        };

    /// <summary>
    /// Converts a <see cref="Result"/> into an <see cref="ProblemHttpResult"/> containing RFC7807 <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails"/>.
    /// </summary>
    /// <param name="result">The <see cref="Result"/> to be converted.</param>
    /// <param name="statusCode">The HTTP status code to be set in the <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails"/>.</param>
    /// <returns>A <see cref="ProblemHttpResult"/> containing the <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails"/>.</returns>
    /// <remarks>
    /// This is primarily an internal API, intended to be used from <see cref="ToMinimalApiResult"/> or <see cref="ToMinimalApiResult{T}"/>.
    /// However, it might have some desired use cases where direct usage is appropriate, so it is made public.
    /// </remarks>
    public static IResult ToProblemHttpResult(this Result result, HttpStatusCode statusCode)
        => HttpResults.Problem(title: result.Message, statusCode: (int)statusCode);
}
