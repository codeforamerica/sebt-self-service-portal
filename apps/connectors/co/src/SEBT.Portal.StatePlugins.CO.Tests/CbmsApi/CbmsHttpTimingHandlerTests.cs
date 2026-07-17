using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SEBT.Portal.StatePlugins.CO.CbmsApi;

namespace SEBT.Portal.StatePlugins.CO.Tests.CbmsApi;

public class CbmsHttpTimingHandlerTests
{
    [Theory]
    [InlineData(HttpStatusCode.OK, LogLevel.Information)]
    [InlineData(HttpStatusCode.Created, LogLevel.Information)]
    [InlineData(HttpStatusCode.NotFound, LogLevel.Information)] // 404 carve-out: "no household found" is a normal outcome.
    [InlineData(HttpStatusCode.BadRequest, LogLevel.Error)]
    [InlineData(HttpStatusCode.Unauthorized, LogLevel.Error)]
    [InlineData(HttpStatusCode.InternalServerError, LogLevel.Error)]
    [InlineData(HttpStatusCode.BadGateway, LogLevel.Error)]
    public async Task SendAsync_logs_completion_at_expected_level_for_status_code(
        HttpStatusCode statusCode,
        LogLevel expectedLevel)
    {
        var logger = new CapturingLogger();
        var inner = new StubHandler((req, _) => new HttpResponseMessage(statusCode));
        var handler = new CbmsHttpTimingHandler(inner, logger);
        using var client = new HttpClient(handler);

        await client.GetAsync("https://cbms.example/sebt/get-account-details");

        var completion = logger.Entries.Single(e => e.Message.Contains("in ") && e.Message.Contains("ms"));
        Assert.Equal(expectedLevel, completion.Level);
        Assert.Equal("CBMS", completion.Properties["Dependency"]);
    }

    [Fact]
    public async Task SendAsync_logs_transport_failure_as_error_with_dependency_property()
    {
        var logger = new CapturingLogger();
        var inner = new StubHandler((_, _) => throw new HttpRequestException("boom"));
        var handler = new CbmsHttpTimingHandler(inner, logger);
        using var client = new HttpClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetAsync("https://cbms.example/sebt/get-account-details"));

        var failure = logger.Entries.Single(e => e.Message.Contains("failed after"));
        Assert.Equal(LogLevel.Error, failure.Level);
        Assert.Equal("CBMS", failure.Properties["Dependency"]);
        Assert.NotNull(failure.Exception);
    }

    [Fact]
    public async Task SendAsync_attaches_dependency_property_to_send_log_too()
    {
        var logger = new CapturingLogger();
        var inner = new StubHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK));
        var handler = new CbmsHttpTimingHandler(inner, logger);
        using var client = new HttpClient(handler);

        await client.GetAsync("https://cbms.example/sebt/get-account-details");

        var sending = logger.Entries.Single(e => e.Message.Contains("sending request"));
        Assert.Equal("CBMS", sending.Properties["Dependency"]);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly System.Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _respond;

        public StubHandler(System.Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> respond)
        {
            _respond = respond;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_respond(request, cancellationToken));
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<LogEntry> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            System.Exception? exception,
            System.Func<TState, System.Exception?, string> formatter)
        {
            var properties = new Dictionary<string, object?>();
            if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
            {
                foreach (var kv in pairs)
                {
                    properties[kv.Key] = kv.Value;
                }
            }

            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception, properties));
        }
    }

    private sealed record LogEntry(
        LogLevel Level,
        string Message,
        System.Exception? Exception,
        IReadOnlyDictionary<string, object?> Properties);
}
