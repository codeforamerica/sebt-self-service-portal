using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.DocVerification;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Services;

public class HttpSocureClientTests
{
    private readonly SocureSettings settings = new()
    {
        UseStub = false,
        ApiKey = "test-api-key",
        BaseUrl = "https://riskos.sandbox.socure.com",
        ApiVersion = "2025-01-01.orion",
        Workflow = "consumer_onboarding",
        DocvEnrichmentName = "SocureDocRequest"
    };

    private HttpSocureClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(settings.BaseUrl) };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("Socure").Returns(httpClient);

        var snapshot = Substitute.For<IOptionsSnapshot<SocureSettings>>();
        snapshot.Value.Returns(settings);

        return new HttpSocureClient(
            factory,
            snapshot,
            NullLogger<HttpSocureClient>.Instance);
    }

    private static MockHttpHandler RespondWith(HttpStatusCode status, object body) =>
        new(status, JsonSerializer.Serialize(body));

    // --- REVIEW with DocV enrichment → DocumentVerificationRequired ---

    [Fact]
    public async Task RunIdProofingAssessment_ShouldReturnDocVerificationRequired_WhenDecisionIsReview()
    {
        var handler = RespondWith(HttpStatusCode.OK, new
        {
            eval_id = "eval-123",
            decision = "REVIEW",
            eval_status = "evaluation_paused",
            data_enrichments = new[]
            {
                new
                {
                    enrichment_name = "Socure Document Request - Default Flow",
                    enrichment_provider = "SocureDocRequest",
                    status_code = 200,
                    response = new
                    {
                        data = new
                        {
                            docvTransactionToken = "token-abc",
                            url = "https://verify.socure.com/#/dv/token-abc"
                        },
                        referenceId = "ref-456"
                    }
                }
            }
        });

        var client = CreateClient(handler);
        var result = await client.RunIdProofingAssessmentAsync(
            1, "test@example.com", "1990-01-01", "ssn", "999-99-9999");

        Assert.True(result.IsSuccess);
        var assessment = result.Value;
        Assert.Equal(IdProofingOutcome.DocumentVerificationRequired, assessment.Outcome);
        Assert.NotNull(assessment.DocvSession);
        Assert.Equal("token-abc", assessment.DocvSession.DocvTransactionToken);
        Assert.Equal("https://verify.socure.com/#/dv/token-abc", assessment.DocvSession.DocvUrl);
        Assert.Equal("ref-456", assessment.DocvSession.ReferenceId);
        Assert.Equal("eval-123", assessment.DocvSession.EvalId);
    }

    // --- ACCEPT → Matched ---

    [Fact]
    public async Task RunIdProofingAssessment_ShouldReturnMatched_WhenDecisionIsAccept()
    {
        var handler = RespondWith(HttpStatusCode.OK, new
        {
            eval_id = "eval-123",
            decision = "ACCEPT",
            eval_status = "evaluation_completed",
            data_enrichments = Array.Empty<object>()
        });

        var client = CreateClient(handler);
        var result = await client.RunIdProofingAssessmentAsync(
            1, "test@example.com", "1990-01-01", "ssn", "999-99-9999");

        Assert.True(result.IsSuccess);
        Assert.Equal(IdProofingOutcome.Matched, result.Value.Outcome);
        Assert.Null(result.Value.DocvSession);
    }

    // --- REJECT → Failed ---

    [Fact]
    public async Task RunIdProofingAssessment_ShouldReturnFailed_WhenDecisionIsReject()
    {
        var handler = RespondWith(HttpStatusCode.OK, new
        {
            eval_id = "eval-123",
            decision = "REJECT",
            eval_status = "evaluation_completed",
            data_enrichments = Array.Empty<object>()
        });

        var client = CreateClient(handler);
        var result = await client.RunIdProofingAssessmentAsync(
            1, "test@example.com", "1990-01-01", "ssn", "999-99-9999");

        Assert.True(result.IsSuccess);
        Assert.Equal(IdProofingOutcome.Failed, result.Value.Outcome);
    }

    // --- HTTP error → DependencyFailed ---

    [Fact]
    public async Task RunIdProofingAssessment_ShouldReturnDependencyFailed_WhenHttpReturns500()
    {
        var handler = RespondWith(HttpStatusCode.InternalServerError, new { error = "Internal Server Error" });

        var client = CreateClient(handler);
        var result = await client.RunIdProofingAssessmentAsync(
            1, "test@example.com", "1990-01-01", "ssn", "999-99-9999");

        Assert.False(result.IsSuccess);
        Assert.IsType<DependencyFailedResult<IdProofingAssessmentResult>>(result);
    }

    [Fact]
    public async Task RunIdProofingAssessment_ShouldReturnDependencyFailed_WhenHttpReturns401()
    {
        var handler = RespondWith(HttpStatusCode.Unauthorized, new { error = "Unauthorized" });

        var client = CreateClient(handler);
        var result = await client.RunIdProofingAssessmentAsync(
            1, "test@example.com", "1990-01-01", "ssn", "999-99-9999");

        Assert.False(result.IsSuccess);
        Assert.IsType<DependencyFailedResult<IdProofingAssessmentResult>>(result);
    }

    // --- Timeout → DependencyFailed ---

    [Fact]
    public async Task RunIdProofingAssessment_ShouldReturnDependencyFailed_OnTimeout()
    {
        var handler = new TimeoutHandler();

        var client = CreateClient(handler);
        var result = await client.RunIdProofingAssessmentAsync(
            1, "test@example.com", "1990-01-01", "ssn", "999-99-9999");

        Assert.False(result.IsSuccess);
        Assert.IsType<DependencyFailedResult<IdProofingAssessmentResult>>(result);
    }

    // --- Missing data_enrichments handled gracefully ---

    [Fact]
    public async Task RunIdProofingAssessment_ShouldReturnNullDocvSession_WhenNoDocRequestEnrichment()
    {
        var handler = RespondWith(HttpStatusCode.OK, new
        {
            eval_id = "eval-123",
            decision = "REVIEW",
            eval_status = "evaluation_paused",
            data_enrichments = Array.Empty<object>()
        });

        var client = CreateClient(handler);
        var result = await client.RunIdProofingAssessmentAsync(
            1, "test@example.com", "1990-01-01", "ssn", "999-99-9999");

        Assert.True(result.IsSuccess);
        Assert.Equal(IdProofingOutcome.DocumentVerificationRequired, result.Value.Outcome);
        Assert.Null(result.Value.DocvSession);
    }

    // --- Sends correct request ---

    [Fact]
    public async Task RunIdProofingAssessment_ShouldSendCorrectRequestShape()
    {
        string? capturedBody = null;
        var handler = new CaptureRequestHandler(body =>
        {
            capturedBody = body;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    eval_id = "eval-123",
                    decision = "ACCEPT",
                    data_enrichments = Array.Empty<object>()
                }), System.Text.Encoding.UTF8, "application/json")
            };
        });

        var client = CreateClient(handler);
        await client.RunIdProofingAssessmentAsync(
            42, "user@example.com", "1990-06-15", "ssn", "123-45-6789");

        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody);
        var root = doc.RootElement;
        Assert.Equal("42", root.GetProperty("id").GetString());
        Assert.Equal("consumer_onboarding", root.GetProperty("workflow").GetString());
        Assert.True(root.TryGetProperty("timestamp", out _), "Request should include timestamp");
        var individual = root.GetProperty("data").GetProperty("individual");
        Assert.Equal("user@example.com", individual.GetProperty("email").GetString());
        Assert.Equal("1990-06-15", individual.GetProperty("date_of_birth").GetString());
        Assert.Equal("123-45-6789", individual.GetProperty("national_id").GetString());
    }

    [Fact]
    public async Task RunIdProofingAssessment_ShouldOmitNationalId_WhenIdTypeIsNull()
    {
        string? capturedBody = null;
        var handler = new CaptureRequestHandler(body =>
        {
            capturedBody = body;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    eval_id = "eval-123",
                    decision = "REJECT",
                    data_enrichments = Array.Empty<object>()
                }), System.Text.Encoding.UTF8, "application/json")
            };
        });

        var client = CreateClient(handler);
        await client.RunIdProofingAssessmentAsync(
            42, "user@example.com", "1990-06-15", null, null);

        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody);
        var individual = doc.RootElement.GetProperty("data").GetProperty("individual");
        // national_id should not be present when idType is null
        Assert.False(individual.TryGetProperty("national_id", out _));
    }

    // --- DI session token ---

    [Fact]
    public async Task RunIdProofingAssessment_ShouldIncludeDiSessionToken_WhenConfigured()
    {
        string? capturedBody = null;
        var handler = new CaptureRequestHandler(body =>
        {
            capturedBody = body;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    eval_id = "eval-123",
                    decision = "ACCEPT",
                    data_enrichments = Array.Empty<object>()
                }), System.Text.Encoding.UTF8, "application/json")
            };
        });

        var settingsWithDiToken = new SocureSettings
        {
            UseStub = false,
            ApiKey = "test-api-key",
            BaseUrl = "https://riskos.sandbox.socure.com",
            ApiVersion = "2025-01-01.orion",
            Workflow = "consumer_onboarding",
            DocvEnrichmentName = "SocureDocRequest",
            DiSessionToken = "test-di-token"
        };

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(settingsWithDiToken.BaseUrl) };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("Socure").Returns(httpClient);
        var snapshot = Substitute.For<IOptionsSnapshot<SocureSettings>>();
        snapshot.Value.Returns(settingsWithDiToken);
        var client = new HttpSocureClient(
            factory,
            snapshot,
            NullLogger<HttpSocureClient>.Instance);

        await client.RunIdProofingAssessmentAsync(
            42, "user@example.com", "1990-06-15", "ssn", "123-45-6789");

        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody);
        var individual = doc.RootElement.GetProperty("data").GetProperty("individual");
        Assert.Equal("test-di-token", individual.GetProperty("di_session_token").GetString());
    }

    [Fact]
    public async Task RunIdProofingAssessment_ShouldOmitDiSessionToken_WhenNotConfigured()
    {
        string? capturedBody = null;
        var handler = new CaptureRequestHandler(body =>
        {
            capturedBody = body;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    eval_id = "eval-123",
                    decision = "ACCEPT",
                    data_enrichments = Array.Empty<object>()
                }), System.Text.Encoding.UTF8, "application/json")
            };
        });

        var client = CreateClient(handler);
        await client.RunIdProofingAssessmentAsync(
            42, "user@example.com", "1990-06-15", "ssn", "123-45-6789");

        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody);
        var individual = doc.RootElement.GetProperty("data").GetProperty("individual");
        Assert.False(individual.TryGetProperty("di_session_token", out _));
    }

    // --- Supplementary PII (IP address, phone) ---

    [Fact]
    public async Task RunIdProofingAssessment_ShouldIncludeIpAddress_WhenProvided()
    {
        string? capturedBody = null;
        var handler = new CaptureRequestHandler(body =>
        {
            capturedBody = body;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    eval_id = "eval-123",
                    decision = "ACCEPT",
                    data_enrichments = Array.Empty<object>()
                }), System.Text.Encoding.UTF8, "application/json")
            };
        });

        var client = CreateClient(handler);
        await client.RunIdProofingAssessmentAsync(
            42, "user@example.com", "1990-06-15", "ssn", "123-45-6789",
            ipAddress: "203.0.0.10");

        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody);
        var individual = doc.RootElement.GetProperty("data").GetProperty("individual");
        Assert.Equal("203.0.0.10", individual.GetProperty("ip_address").GetString());
    }

    [Fact]
    public async Task RunIdProofingAssessment_ShouldIncludePhoneNumber_WhenProvided()
    {
        string? capturedBody = null;
        var handler = new CaptureRequestHandler(body =>
        {
            capturedBody = body;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    eval_id = "eval-123",
                    decision = "ACCEPT",
                    data_enrichments = Array.Empty<object>()
                }), System.Text.Encoding.UTF8, "application/json")
            };
        });

        var client = CreateClient(handler);
        await client.RunIdProofingAssessmentAsync(
            42, "user@example.com", "1990-06-15", "ssn", "123-45-6789",
            phoneNumber: "+12025551234");

        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody);
        var individual = doc.RootElement.GetProperty("data").GetProperty("individual");
        Assert.Equal("+12025551234", individual.GetProperty("phone_number").GetString());
    }

    [Fact]
    public async Task RunIdProofingAssessment_ShouldIncludeNames_WhenProvided()
    {
        string? capturedBody = null;
        var handler = new CaptureRequestHandler(body =>
        {
            capturedBody = body;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    eval_id = "eval-123",
                    decision = "ACCEPT",
                    data_enrichments = Array.Empty<object>()
                }), System.Text.Encoding.UTF8, "application/json")
            };
        });

        var client = CreateClient(handler);
        await client.RunIdProofingAssessmentAsync(
            42, "user@example.com", "1990-06-15", "ssn", "123-45-6789",
            givenName: "Maria", familyName: "Martinez");

        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody);
        var individual = doc.RootElement.GetProperty("data").GetProperty("individual");
        Assert.Equal("Maria", individual.GetProperty("given_name").GetString());
        Assert.Equal("Martinez", individual.GetProperty("family_name").GetString());
    }

    [Fact]
    public async Task RunIdProofingAssessment_ShouldOmitIpAndPhone_WhenNotProvided()
    {
        string? capturedBody = null;
        var handler = new CaptureRequestHandler(body =>
        {
            capturedBody = body;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    eval_id = "eval-123",
                    decision = "ACCEPT",
                    data_enrichments = Array.Empty<object>()
                }), System.Text.Encoding.UTF8, "application/json")
            };
        });

        var client = CreateClient(handler);
        await client.RunIdProofingAssessmentAsync(
            42, "user@example.com", "1990-06-15", "ssn", "123-45-6789");

        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody);
        var individual = doc.RootElement.GetProperty("data").GetProperty("individual");
        Assert.False(individual.TryGetProperty("ip_address", out _));
        Assert.False(individual.TryGetProperty("phone_number", out _));
    }

    // --- Address fields ---

    [Fact]
    public async Task RunIdProofingAssessment_ShouldIncludeAddress_WhenProvided()
    {
        string? capturedBody = null;
        var handler = new CaptureRequestHandler(body =>
        {
            capturedBody = body;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    eval_id = "eval-123",
                    decision = "ACCEPT",
                    data_enrichments = Array.Empty<object>()
                }), System.Text.Encoding.UTF8, "application/json")
            };
        });

        var client = CreateClient(handler);
        var address = new Address
        {
            StreetAddress1 = "123 Main St",
            StreetAddress2 = "Apt 4",
            City = "Washington",
            State = "DC",
            PostalCode = "20001"
        };

        await client.RunIdProofingAssessmentAsync(
            42, "user@example.com", "1990-06-15", "ssn", "123-45-6789",
            address: address);

        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody);
        var individual = doc.RootElement.GetProperty("data").GetProperty("individual");
        var addr = individual.GetProperty("address");
        Assert.Equal("123 Main St", addr.GetProperty("line_1").GetString());
        Assert.Equal("Apt 4", addr.GetProperty("line_2").GetString());
        Assert.Equal("Washington", addr.GetProperty("locality").GetString());
        Assert.Equal("DC", addr.GetProperty("major_admin_division").GetString());
        Assert.Equal("20001", addr.GetProperty("postal_code").GetString());
        Assert.Equal("US", addr.GetProperty("country").GetString());
        Assert.Equal("mailing", addr.GetProperty("type").GetString());
    }

    [Fact]
    public async Task RunIdProofingAssessment_ShouldOmitAddress_WhenNotProvided()
    {
        string? capturedBody = null;
        var handler = new CaptureRequestHandler(body =>
        {
            capturedBody = body;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    eval_id = "eval-123",
                    decision = "ACCEPT",
                    data_enrichments = Array.Empty<object>()
                }), System.Text.Encoding.UTF8, "application/json")
            };
        });

        var client = CreateClient(handler);
        await client.RunIdProofingAssessmentAsync(
            42, "user@example.com", "1990-06-15", "ssn", "123-45-6789");

        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody);
        var individual = doc.RootElement.GetProperty("data").GetProperty("individual");
        Assert.False(individual.TryGetProperty("address", out _));
    }

    // --- DI session token from parameter overrides config ---

    [Fact]
    public async Task RunIdProofingAssessment_ShouldUseDiTokenFromParameter_OverConfig()
    {
        string? capturedBody = null;
        var handler = new CaptureRequestHandler(body =>
        {
            capturedBody = body;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    eval_id = "eval-123",
                    decision = "ACCEPT",
                    data_enrichments = Array.Empty<object>()
                }), System.Text.Encoding.UTF8, "application/json")
            };
        });

        // Config has a placeholder token, but parameter should win
        var settingsWithConfigToken = new SocureSettings
        {
            UseStub = false,
            ApiKey = "test-api-key",
            BaseUrl = "https://riskos.sandbox.socure.com",
            ApiVersion = "2025-01-01.orion",
            Workflow = "consumer_onboarding",
            DocvEnrichmentName = "SocureDocRequest",
            DiSessionToken = "config-placeholder-token"
        };

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(settingsWithConfigToken.BaseUrl) };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("Socure").Returns(httpClient);
        var snapshot = Substitute.For<IOptionsSnapshot<SocureSettings>>();
        snapshot.Value.Returns(settingsWithConfigToken);
        var client = new HttpSocureClient(
            factory,
            snapshot,
            NullLogger<HttpSocureClient>.Instance);

        await client.RunIdProofingAssessmentAsync(
            42, "user@example.com", "1990-06-15", "ssn", "123-45-6789",
            diSessionToken: "real-frontend-token");

        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody);
        var individual = doc.RootElement.GetProperty("data").GetProperty("individual");
        Assert.Equal("real-frontend-token", individual.GetProperty("di_session_token").GetString());
    }

    [Fact]
    public async Task RunIdProofingAssessment_ShouldFallBackToConfigToken_WhenParameterIsNull()
    {
        string? capturedBody = null;
        var handler = new CaptureRequestHandler(body =>
        {
            capturedBody = body;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    eval_id = "eval-123",
                    decision = "ACCEPT",
                    data_enrichments = Array.Empty<object>()
                }), System.Text.Encoding.UTF8, "application/json")
            };
        });

        var settingsWithConfigToken = new SocureSettings
        {
            UseStub = false,
            ApiKey = "test-api-key",
            BaseUrl = "https://riskos.sandbox.socure.com",
            ApiVersion = "2025-01-01.orion",
            Workflow = "consumer_onboarding",
            DocvEnrichmentName = "SocureDocRequest",
            DiSessionToken = "config-fallback-token"
        };

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri(settingsWithConfigToken.BaseUrl) };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("Socure").Returns(httpClient);
        var snapshot = Substitute.For<IOptionsSnapshot<SocureSettings>>();
        snapshot.Value.Returns(settingsWithConfigToken);
        var client = new HttpSocureClient(
            factory,
            snapshot,
            NullLogger<HttpSocureClient>.Instance);

        await client.RunIdProofingAssessmentAsync(
            42, "user@example.com", "1990-06-15", "ssn", "123-45-6789");

        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody);
        var individual = doc.RootElement.GetProperty("data").GetProperty("individual");
        Assert.Equal("config-fallback-token", individual.GetProperty("di_session_token").GetString());
    }

    // --- DocV config shape ---

    [Fact]
    public async Task RunIdProofingAssessment_ShouldSendDocvConfigWithSendMessageAndLanguage()
    {
        string? capturedBody = null;
        var handler = new CaptureRequestHandler(body =>
        {
            capturedBody = body;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    eval_id = "eval-123",
                    decision = "ACCEPT",
                    data_enrichments = Array.Empty<object>()
                }), System.Text.Encoding.UTF8, "application/json")
            };
        });

        var client = CreateClient(handler);
        await client.RunIdProofingAssessmentAsync(
            42, "user@example.com", "1990-06-15", "ssn", "123-45-6789");

        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody);
        var docv = doc.RootElement.GetProperty("data").GetProperty("individual").GetProperty("docv");
        var config = docv.GetProperty("config");
        Assert.True(config.GetProperty("send_message").GetBoolean());
        Assert.Equal("en", config.GetProperty("language").GetString());
    }

    // --- StartDocvSessionAsync throws ---

    [Fact]
    public async Task StartDocvSession_ShouldThrowNotSupported()
    {
        var handler = RespondWith(HttpStatusCode.OK, new { });
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            client.StartDocvSessionAsync(1, "test@example.com"));
    }

    // --- Sends correct headers ---

    [Fact]
    public async Task RunIdProofingAssessment_ShouldSendAuthAndVersionHeaders()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new CaptureFullRequestHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    eval_id = "eval-123",
                    decision = "ACCEPT",
                    data_enrichments = Array.Empty<object>()
                }), System.Text.Encoding.UTF8, "application/json")
            };
        });

        var client = CreateClient(handler);
        await client.RunIdProofingAssessmentAsync(
            1, "test@example.com", "1990-01-01", "ssn", "999-99-9999");

        Assert.NotNull(capturedRequest);
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization?.Scheme);
        Assert.Equal("test-api-key", capturedRequest.Headers.Authorization?.Parameter);
        Assert.True(capturedRequest.Headers.TryGetValues("X-API-Version", out var versions));
        Assert.Contains("2025-01-01.orion", versions);
    }
}

// --- Test helpers ---

internal class MockHttpHandler(HttpStatusCode status, string responseBody) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(responseBody, System.Text.Encoding.UTF8, "application/json")
        });
    }
}

internal class TimeoutHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.");
    }
}

internal class CaptureRequestHandler(Func<string, HttpResponseMessage> handler) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = await request.Content!.ReadAsStringAsync(cancellationToken);
        return handler(body);
    }
}

internal class CaptureFullRequestHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(handler(request));
    }
}
