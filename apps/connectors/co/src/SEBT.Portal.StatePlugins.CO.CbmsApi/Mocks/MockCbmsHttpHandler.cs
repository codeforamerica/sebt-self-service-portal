using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace SEBT.Portal.StatePlugins.CO.CbmsApi.Mocks;

/// <summary>
/// HttpMessageHandler that returns mock CBMS API responses.
/// Token, ping, and check-enrollment return static embedded JSON and do not need a data store.
/// Phone lookup (get-account-details) and PATCH (update-std-dtls) require
/// <see cref="MockCbmsDataStore"/>.
/// </summary>
public sealed class MockCbmsHttpHandler : HttpMessageHandler
{
    private const string TokenPath = "ext-uat-c-cbms-oauth-app/token";
    private const string ApiBase = "ext-uat-c-cbms-cfa-eapi/api";
    private const string GetAccountDetailsPath = "sebt/get-account-details";
    private const string UpdateStdDtlsPath = "sebt/update-std-dtls";

    private readonly MockCbmsDataStore? _dataStore;

    private static readonly string MockTokenResponse = LoadStaticMockJson("token.json");
    private static readonly string MockPingResponse = LoadStaticMockJson("ping.json");
    private static readonly string MockCheckEnrollmentResponse = LoadStaticMockJson("check-enrollment.json");

    /// <summary>
    /// Handler for static mock responses (token, ping, check-enrollment).
    /// </summary>
    public MockCbmsHttpHandler()
    {
    }

    public MockCbmsHttpHandler(MockCbmsDataStore dataStore)
    {
        ArgumentNullException.ThrowIfNull(dataStore);
        _dataStore = dataStore;
    }

    private MockCbmsDataStore RequireDataStore() =>
        _dataStore ?? throw new InvalidOperationException(
            "Phone lookup and address updates require a MockCbmsDataStore.");

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) =>
        SendAsync(request, cancellationToken).GetAwaiter().GetResult();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri?.ToString() ?? "";
        var method = request.Method;

        if (url.Contains(TokenPath, StringComparison.OrdinalIgnoreCase) && method == HttpMethod.Post)
        {
            return JsonResponse(MockTokenResponse);
        }

        if (url.Contains($"{ApiBase}/ping", StringComparison.OrdinalIgnoreCase) && method == HttpMethod.Get)
        {
            return JsonResponse(MockPingResponse);
        }

        if (url.Contains($"{ApiBase}/sebt/check-enrollment", StringComparison.OrdinalIgnoreCase) && method == HttpMethod.Post)
        {
            return JsonResponse(MockCheckEnrollmentResponse);
        }

        if (url.Contains(GetAccountDetailsPath, StringComparison.OrdinalIgnoreCase) && method == HttpMethod.Post)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var phone = ExtractPhoneFromRequestBody(body);
            var response = await RequireDataStore().GetResponseForPhoneAsync(phone, cancellationToken).ConfigureAwait(false);
            if (url.Contains("ebtCardService=N", StringComparison.OrdinalIgnoreCase))
            {
                response = MockCbmsGetAccountDetailsResponse.WithoutCardFields(response);
            }

            return JsonResponse(response);
        }

        if (url.Contains(UpdateStdDtlsPath, StringComparison.OrdinalIgnoreCase) && method == HttpMethod.Patch)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var response = await RequireDataStore().ApplyPatchAsync(body, cancellationToken).ConfigureAwait(false);

            if (response.Contains("\"code\":\"404\""))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent(response, Encoding.UTF8, "application/json")
                };
            }

            return JsonResponse(response);
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent(
                $$"""{"error":"Mock handler: no response for {{method}} {{url}}"}""",
                Encoding.UTF8,
                "application/json")
        };
    }

    private static string ExtractPhoneFromRequestBody(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("phnNm", out var phoneEl)
            ? phoneEl.GetString() ?? ""
            : "";
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private static string LoadStaticMockJson(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"SEBT.Portal.StatePlugins.CO.CbmsApi.TestData.CbmsMocks.{fileName}";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new InvalidOperationException(
                $"Mock JSON resource not found: {resourceName}. Ensure TestData/CbmsMocks/{fileName} is set as EmbeddedResource.");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
