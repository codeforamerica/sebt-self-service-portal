using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Services;
using Xunit.Abstractions;

namespace SEBT.Portal.Tests.Integration;

/// <summary>
/// Smoke tests that call the real Socure sandbox API.
/// Gated by the SOCURE_API_KEY environment variable — skipped when absent.
///
/// Run manually:
///   SOCURE_API_KEY=your-key dotnet test --filter "Category=Socure"
/// </summary>
[Trait("Category", "Socure")]
public class HttpSocureClientSmokeTests(ITestOutputHelper output)
{
    private static string? ApiKey => Environment.GetEnvironmentVariable("SOCURE_API_KEY");

    private HttpSocureClient CreateRealClient(SocureSettings? settingsOverride = null)
    {
        var settings = settingsOverride ?? new SocureSettings
        {
            UseStub = false,
            ApiKey = ApiKey!,
            BaseUrl = "https://riskos.sandbox.socure.com",
            ApiVersion = "2025-01-01.orion",
            Workflow = "consumer_onboarding",
            DocvEnrichmentName = "SocureDocRequest"
        };

        var httpClient = new HttpClient();
        var factory = new SingleClientFactory(httpClient);
        var logger = new TestOutputLogger<HttpSocureClient>(output);

        return new HttpSocureClient(factory, Options.Create(settings), logger);
    }

    [Fact]
    public async Task RunIdProofingAssessmentAsync_ShouldReturnParsableResponse_WithTestData()
    {
        if (string.IsNullOrEmpty(ApiKey))
        {
            output.WriteLine("SOCURE_API_KEY not set — skipping smoke test.");
            return;
        }

        var client = CreateRealClient();

        var result = await client.RunIdProofingAssessmentAsync(
            userId: 99999,
            email: "smoketest@example.com",
            dateOfBirth: "1990-01-15",
            idType: "ssn",
            idValue: "999-99-9999",
            cancellationToken: CancellationToken.None);

        output.WriteLine($"IsSuccess: {result.IsSuccess}");
        output.WriteLine($"Message: {result.Message}");

        if (result.IsSuccess)
        {
            var assessment = result.Value;
            output.WriteLine($"Outcome: {assessment.Outcome}");
            output.WriteLine($"AllowIdRetry: {assessment.AllowIdRetry}");
            output.WriteLine($"DocvSession: {(assessment.DocvSession != null ? "present" : "null")}");

            if (assessment.DocvSession != null)
            {
                output.WriteLine($"  Token: {assessment.DocvSession.DocvTransactionToken[..8]}...");
                output.WriteLine($"  URL: {assessment.DocvSession.DocvUrl}");
                output.WriteLine($"  ReferenceId: {assessment.DocvSession.ReferenceId}");
                output.WriteLine($"  EvalId: {assessment.DocvSession.EvalId}");
            }

            // The response should be parseable regardless of outcome
            Assert.True(
                assessment.Outcome is IdProofingOutcome.Matched
                    or IdProofingOutcome.Failed
                    or IdProofingOutcome.DocumentVerificationRequired,
                $"Unexpected outcome: {assessment.Outcome}");
        }
        else
        {
            // If it fails, we want to see why — but don't hard-fail on sandbox issues
            output.WriteLine("WARNING: Socure sandbox call failed. Check API key and network.");
            output.WriteLine($"Failure reason: {result.Message}");
        }
    }

    [Fact]
    public async Task RunIdProofingAssessmentAsync_ShouldReturnParsableResponse_WithoutSsn()
    {
        if (string.IsNullOrEmpty(ApiKey))
        {
            output.WriteLine("SOCURE_API_KEY not set — skipping smoke test.");
            return;
        }

        var client = CreateRealClient();

        // No SSN — tests the null national_id path
        var result = await client.RunIdProofingAssessmentAsync(
            userId: 99998,
            email: "smoketest-noid@example.com",
            dateOfBirth: "1985-06-20",
            idType: null,
            idValue: null,
            cancellationToken: CancellationToken.None);

        output.WriteLine($"IsSuccess: {result.IsSuccess}");
        output.WriteLine($"Message: {result.Message}");

        if (result.IsSuccess)
        {
            var assessment = result.Value;
            output.WriteLine($"Outcome: {assessment.Outcome}");
            output.WriteLine($"DocvSession: {(assessment.DocvSession != null ? "present" : "null")}");
        }
        else
        {
            output.WriteLine($"Failure reason: {result.Message}");
        }
    }

    /// <summary>
    /// Diagnostic: raw HTTP call to see the exact error response from Socure.
    /// Useful when the client returns a generic "401" but we need the response body.
    /// </summary>
    [Fact]
    public async Task Diagnostic_RawHttpCall_ShouldLogResponseBody()
    {
        if (string.IsNullOrEmpty(ApiKey))
        {
            output.WriteLine("SOCURE_API_KEY not set — skipping.");
            return;
        }

        using var httpClient = new HttpClient();

        var requestBody = JsonSerializer.Serialize(new
        {
            id = $"smoke-test-diag-{Guid.NewGuid():N}",
            timestamp = DateTime.UtcNow.ToString("o"),
            workflow = "consumer_onboarding",
            data = new
            {
                individual = new
                {
                    email = "smoketest@example.com",
                    date_of_birth = "1990-01-15",
                    national_id = "999999999",
                    docv = new { config = new { } }
                }
            }
        });

        output.WriteLine($"Request body: {requestBody}");

        var request = new HttpRequestMessage(HttpMethod.Post, "https://riskos.sandbox.socure.com/api/evaluation")
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
        request.Headers.Add("X-API-Version", "2025-01-01.orion");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var response = await httpClient.SendAsync(request);

        output.WriteLine($"Status: {(int)response.StatusCode} {response.StatusCode}");

        var body = await response.Content.ReadAsStringAsync();
        output.WriteLine($"Response body: {body}");

        foreach (var header in response.Headers)
        {
            output.WriteLine($"Response header: {header.Key} = {string.Join(", ", header.Value)}");
        }
    }

    /// <summary>
    /// Minimal IHttpClientFactory that returns a single shared HttpClient.
    /// </summary>
    private class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    /// <summary>
    /// Routes ILogger output to xUnit's ITestOutputHelper so results appear in test output.
    /// </summary>
    private class TestOutputLogger<T>(ITestOutputHelper output) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            output.WriteLine($"[{logLevel}] {formatter(state, exception)}");
            if (exception != null)
                output.WriteLine($"  Exception: {exception}");
        }
    }
}
