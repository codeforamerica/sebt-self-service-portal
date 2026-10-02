using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Core.StateBackends.Configuration.Auth;

namespace SEBT.Portal.Infrastructure.StateBackends.Auth;

/// <summary>
/// Applies an OAuth2 client-credentials scheme: fetches a token, caches it until near expiry, and
/// attaches it as a bearer token. The client secret is resolved via
/// <see cref="IStateBackendSecretResolver"/> at token-fetch time — never inlined.
/// </summary>
public sealed class StateBackendOAuthClientCredentialsAuthHandler : DelegatingHandler
{
    // Refresh before actual expiry to avoid a token lapsing in flight.
    private static readonly TimeSpan ExpiryLeeway = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Used when the token response omits <c>expires_in</c> or sends 0.  A zero TTL will
    /// make the cache born-expired and refetch on every call; this is a safety net for
    /// backends that don't return an expiration time.
    /// </summary>
    internal const int DefaultExpiresInSeconds = 3600;

    private static readonly JsonSerializerOptions TokenJsonOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private readonly StateBackendOAuthClientCredentialsAuthScheme _scheme;
    private readonly IStateBackendSecretResolver _secretResolver;
    private readonly HttpClient _tokenClient;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private readonly TimeProvider _timeProvider;

    private string? _cachedToken;
    private DateTimeOffset _tokenExpiresAt;

    public StateBackendOAuthClientCredentialsAuthHandler(
        StateBackendOAuthClientCredentialsAuthScheme scheme,
        IStateBackendSecretResolver secretResolver,
        HttpClient tokenClient,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(scheme);
        ArgumentNullException.ThrowIfNull(secretResolver);
        ArgumentNullException.ThrowIfNull(tokenClient);

        _scheme = scheme;
        _secretResolver = secretResolver;
        _tokenClient = tokenClient;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Buffer content up front so a 401 retry can resend the same body.
        if (request.Content is not null)
        {
            byte[] bytes = await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            var buffered = new ByteArrayContent(bytes);
            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Content.Headers)
            {
                buffered.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            request.Content = buffered;
        }

        await AttachBearerAsync(request, forceRefresh: false, cancellationToken).ConfigureAwait(false);
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        // Upstream rejected the bearer token — drop the cache and retry once with a fresh one.
        response.Dispose();
        HttpRequestMessage retry = await CloneAsync(request, cancellationToken).ConfigureAwait(false);
        await AttachBearerAsync(retry, forceRefresh: true, cancellationToken).ConfigureAwait(false);
        return await base.SendAsync(retry, cancellationToken).ConfigureAwait(false);
    }

    private async Task AttachBearerAsync(
        HttpRequestMessage request, bool forceRefresh, CancellationToken cancellationToken)
    {
        string token = await GetTokenAsync(forceRefresh, cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<string> GetTokenAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        if (!forceRefresh && _cachedToken is not null && _timeProvider.GetUtcNow() < _tokenExpiresAt)
        {
            return _cachedToken;
        }

        await _tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!forceRefresh && _cachedToken is not null && _timeProvider.GetUtcNow() < _tokenExpiresAt)
            {
                return _cachedToken;
            }

            TokenResponse tokenResponse = await FetchTokenAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
            {
                throw new InvalidOperationException("Token endpoint returned no access_token.");
            }

            int expiresIn = tokenResponse.ExpiresInSeconds is > 0
                ? tokenResponse.ExpiresInSeconds.Value
                : DefaultExpiresInSeconds;

            _cachedToken = tokenResponse.AccessToken;
            _tokenExpiresAt = _timeProvider.GetUtcNow()
                + TimeSpan.FromSeconds(expiresIn) - ExpiryLeeway;

            return _cachedToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private async Task<TokenResponse> FetchTokenAsync(CancellationToken cancellationToken)
    {
        string clientSecret = _secretResolver.Resolve(_scheme.ClientSecretRef);

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _scheme.ClientId,
            ["client_secret"] = clientSecret,
        };
        if (!string.IsNullOrWhiteSpace(_scheme.Scope))
        {
            form["scope"] = _scheme.Scope;
        }

        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, _scheme.TokenUrl)
        {
            Content = new FormUrlEncodedContent(form),
        };

        using HttpResponseMessage response = await _tokenClient
            .SendAsync(tokenRequest, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using Stream stream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        TokenResponse? tokenResponse = await JsonSerializer
            .DeserializeAsync<TokenResponse>(stream, TokenJsonOptions, cancellationToken)
            .ConfigureAwait(false);

        return tokenResponse
            ?? throw new InvalidOperationException("Token endpoint returned an empty response.");
    }

    private static async Task<HttpRequestMessage> CloneAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
        };

        foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Content is not null)
        {
            byte[] bytes = await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            var content = new ByteArrayContent(bytes);
            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Content.Headers)
            {
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            clone.Content = content;
        }

        return clone;
    }

    private sealed record TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        [JsonPropertyName("expires_in")]
        public int? ExpiresInSeconds { get; init; }
    }
}
