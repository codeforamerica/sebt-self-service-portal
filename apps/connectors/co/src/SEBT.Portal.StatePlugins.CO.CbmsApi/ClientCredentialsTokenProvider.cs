using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;

namespace SEBT.Portal.StatePlugins.CO.CbmsApi;

/// <summary>
/// <see cref="IAccessTokenProvider"/> that obtains OAuth 2.0 bearer tokens via the
/// client_credentials grant. Tokens are cached and refreshed 60 seconds before expiry
/// using a <see cref="SemaphoreSlim"/> for thread safety.
/// </summary>
public class ClientCredentialsTokenProvider : IAccessTokenProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _tokenEndpointUrl;
    private readonly ILogger _logger;

    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;

    /// <summary>
    /// How many seconds before the token's actual expiry to trigger a refresh.
    /// </summary>
    private static readonly TimeSpan RefreshBuffer = TimeSpan.FromSeconds(60);

    public ClientCredentialsTokenProvider(
        HttpClient httpClient,
        string clientId,
        string clientSecret,
        string tokenEndpointUrl,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientSecret);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenEndpointUrl);

        _httpClient = httpClient;
        _clientId = clientId;
        _clientSecret = clientSecret;
        _tokenEndpointUrl = tokenEndpointUrl;
        _logger = logger ?? NullLogger.Instance;
    }

    public AllowedHostsValidator AllowedHostsValidator { get; } = new();

    public async Task<string> GetAuthorizationTokenAsync(
        Uri uri,
        Dictionary<string, object>? additionalAuthenticationContext = null,
        CancellationToken cancellationToken = default)
    {
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
        {
            _logger.LogInformation("CBMS token cache hit, expires in {SecondsRemaining:F0}s",
                (_tokenExpiry - DateTimeOffset.UtcNow).TotalSeconds);
            return _cachedToken;
        }

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring the lock.
            if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
            {
                _logger.LogInformation("CBMS token cache hit (after lock), expires in {SecondsRemaining:F0}s",
                    (_tokenExpiry - DateTimeOffset.UtcNow).TotalSeconds);
                return _cachedToken;
            }

            var isRefresh = _cachedToken is not null;
            _logger.LogInformation("CBMS token {Action}: requesting new token from {TokenEndpoint}",
                isRefresh ? "refresh" : "acquisition", _tokenEndpointUrl);

            var sw = Stopwatch.StartNew();
            var (token, expiresIn) = await RequestTokenAsync(cancellationToken);
            sw.Stop();

            _cachedToken = token;
            _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(expiresIn) - RefreshBuffer;

            _logger.LogInformation(
                "CBMS token {Action} succeeded in {ElapsedMs}ms, expires_in={ExpiresInSec}s (effective TTL={EffectiveTtlSec}s)",
                isRefresh ? "refresh" : "acquisition",
                sw.ElapsedMilliseconds,
                expiresIn,
                (int)(_tokenExpiry - DateTimeOffset.UtcNow).TotalSeconds);

            return _cachedToken;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<(string Token, int ExpiresIn)> RequestTokenAsync(
        CancellationToken cancellationToken)
    {
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));

        using var request = new HttpRequestMessage(HttpMethod.Post, _tokenEndpointUrl);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Basic", credentials);
        request.Content = new FormUrlEncodedContent(
            new[] { new KeyValuePair<string, string>("grant_type", "client_credentials") });

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Token request failed {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var root = json.RootElement;
        var accessToken = root.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Token response missing access_token.");
        var expiresIn = ParseExpiresIn(root.GetProperty("expires_in"));

        return (accessToken, expiresIn);
    }

    /// <summary>
    /// Default expiry in seconds when the token response omits or provides an invalid expires_in.
    /// Using a sensible default avoids a refresh loop that would occur if we returned 0.
    /// </summary>
    private const int DefaultExpiresInSeconds = 3600;

    private static int ParseExpiresIn(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var n) && n > 0)
            return n;
        if (element.ValueKind == JsonValueKind.Number)
            return DefaultExpiresInSeconds; // Number but not positive (e.g. 0)
        var s = element.GetString();
        if (int.TryParse(s, out var parsed) && parsed > 0)
            return parsed;
        return DefaultExpiresInSeconds;
    }
}
