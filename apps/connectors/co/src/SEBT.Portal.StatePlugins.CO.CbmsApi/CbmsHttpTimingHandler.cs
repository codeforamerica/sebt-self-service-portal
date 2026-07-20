using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace SEBT.Portal.StatePlugins.CO.CbmsApi;

/// <summary>
/// Delegating handler that logs the raw HTTP request/response timing,
/// separate from Kiota's serialization overhead. Captures method, URL
/// path, status code, and elapsed milliseconds at the transport layer.
/// Tags every entry with Dependency=CBMS so observability tooling can
/// raise targeted dependency-health alarms.
/// </summary>
internal sealed class CbmsHttpTimingHandler : DelegatingHandler
{
    private const string DependencyName = "CBMS";

    private readonly ILogger _logger;

    public CbmsHttpTimingHandler(HttpMessageHandler innerHandler, ILogger logger)
        : base(innerHandler)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var method = request.Method;
        var path = request.RequestUri?.PathAndQuery ?? "(unknown)";

        _logger.LogInformation(
            "{Dependency} HTTP {Method} {Path}: sending request",
            DependencyName, method, path);

        var sw = Stopwatch.StartNew();
        try
        {
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            sw.Stop();

            var statusCode = (int)response.StatusCode;

            _logger.Log(
                LevelForStatus(statusCode),
                "{Dependency} HTTP {Method} {Path}: {StatusCode} in {ElapsedMs}ms",
                DependencyName, method, path, statusCode, sw.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();

            _logger.LogError(
                ex,
                "{Dependency} HTTP {Method} {Path}: failed after {ElapsedMs}ms — {ErrorMessage}",
                DependencyName, method, path, sw.ElapsedMilliseconds, ex.Message);

            throw;
        }
    }

    // GetAccountDetails returns 404 to signal "no household found" — a normal
    // lookup outcome, not a CBMS failure. Treat it as Information so it doesn't
    // dominate the Dependency=CBMS error signal. Every other non-2xx is escalated
    // to Error so a dependency-health alarm can fire on it.
    private static LogLevel LevelForStatus(int statusCode)
    {
        if (statusCode == 404)
        {
            return LogLevel.Information;
        }

        if (statusCode >= 400)
        {
            return LogLevel.Error;
        }

        return LogLevel.Information;
    }
}
