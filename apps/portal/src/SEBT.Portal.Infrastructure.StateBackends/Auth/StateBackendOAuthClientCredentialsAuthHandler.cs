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
        string token = await GetTokenAsync(cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (_cachedToken is not null && _timeProvider.GetUtcNow() < _tokenExpiresAt)
        {
            return _cachedToken;
        }

        await _tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cachedToken is not null && _timeProvider.GetUtcNow() < _tokenExpiresAt)
            {
                return _cachedToken;
            }

            TokenResponse tokenResponse = await FetchTokenAsync(cancellationToken).ConfigureAwait(false);
            _cachedToken = tokenResponse.AccessToken;
            _tokenExpiresAt = _timeProvider.GetUtcNow()
                + TimeSpan.FromSeconds(tokenResponse.ExpiresInSeconds) - ExpiryLeeway;

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
            .DeserializeAsync<TokenResponse>(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return tokenResponse
            ?? throw new InvalidOperationException("Token endpoint returned an empty response.");
    }

    private sealed record TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresInSeconds { get; init; }
    }
}
