using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace SEBT.Portal.StatePlugins.CO.CbmsApi.Mocks;

/// <summary>
/// HttpMessageHandler that returns mock CBMS API responses.
/// Phone lookup (get-account-details) and PATCH (update-std-dtls) are delegated
/// to <see cref="MockCbmsDataStore"/> for phone-indexed, cache-backed responses.
/// Token, ping, and check-enrollment return static embedded JSON.
/// </summary>
public sealed class MockCbmsHttpHandler : HttpMessageHandler
{
    private const string TokenPath = "ext-uat-c-cbms-oauth-app/token";
    private const string ApiBase = "ext-uat-c-cbms-cfa-eapi/api";
    private const string GetAccountDetailsPath = "sebt/get-account-details";
    private const string UpdateStdDtlsPath = "sebt/update-std-dtls";

    private readonly MockCbmsDataStore _dataStore;

    private static readonly string MockTokenResponse = LoadStaticMockJson("token.json");
    private static readonly string MockPingResponse = LoadStaticMockJson("ping.json");
    private static readonly string MockCheckEnrollmentResponse = LoadStaticMockJson("check-enrollment.json");

    public MockCbmsHttpHandler(MockCbmsDataStore dataStore)
    {
        ArgumentNullException.ThrowIfNull(dataStore);
        _dataStore = dataStore;
    }

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
            var response = await _dataStore.GetResponseForPhoneAsync(phone, cancellationToken).ConfigureAwait(false);
            if (url.Contains("ebtCardService=N", StringComparison.OrdinalIgnoreCase))
            {
                response = MockCbmsGetAccountDetailsResponse.WithoutCardFields(response);
            }

            return JsonResponse(response);
        }

        if (url.Contains(UpdateStdDtlsPath, StringComparison.OrdinalIgnoreCase) && method == HttpMethod.Patch)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var response = await _dataStore.ApplyPatchAsync(body, cancellationToken).ConfigureAwait(false);

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
