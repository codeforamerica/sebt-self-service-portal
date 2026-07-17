using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace SEBT.Portal.StatePlugins.CO.CbmsApi;

/// <summary>
/// Factory for creating <see cref="CbmsSebtApiClient"/> instances with correct Kiota wiring.
/// Uses a shared <see cref="SocketsHttpHandler"/> with pooled connection lifetime for DNS rotation.
/// Each call creates lightweight <see cref="HttpClient"/> instances that share the handler's connection pool.
/// </summary>
public static class CbmsSebtApiClientFactory
{
    private static readonly Lazy<SocketsHttpHandler> SharedHandler = new(() => new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
    });
    
    private static readonly Lazy<HttpClient> TokenHttpClient = new(() => 
        new HttpClient(SharedHandler.Value, disposeHandler: false));
    
    private static readonly ConcurrentDictionary<Uri, HttpClient> HttpClients = new();

    /// <summary>
    /// Creates a client configured for the specified CBMS environment.
    /// Use <see cref="CbmsDefaults"/> constants for well-known environment URLs.
    /// </summary>
    /// <param name="clientId">OAuth 2.0 client ID.</param>
    /// <param name="clientSecret">OAuth 2.0 client secret.</param>
    /// <param name="apiBaseUrl">Base URL for the CBMS API.</param>
    /// <param name="tokenEndpointUrl">OAuth 2.0 token endpoint URL.</param>
    /// <param name="httpMessageHandler">Optional handler for tests or custom HTTP behavior. When provided, used for both token and API requests.</param>
    /// <param name="logger">Optional logger for token acquisition diagnostics.</param>
    public static CbmsSebtApiClient Create(
        string clientId,
        string clientSecret,
        string apiBaseUrl,
        string tokenEndpointUrl,
        HttpMessageHandler? httpMessageHandler = null,
        ILogger? logger = null)
    {
        // When a logger is provided, wrap the transport handler so every HTTP call
        // (including token requests) gets raw timing logged at the transport layer.
        HttpClient BuildTimedClient(HttpMessageHandler innerHandler, Uri? baseAddress = null)
        {
            if (logger != null)
            {
                var timed = new CbmsHttpTimingHandler(innerHandler, logger);
                var client = new HttpClient(timed, disposeHandler: false);
                if (baseAddress != null) client.BaseAddress = baseAddress;
                return client;
            }

            var plain = new HttpClient(innerHandler, disposeHandler: false);
            if (baseAddress != null) plain.BaseAddress = baseAddress;
            return plain;
        }

        var tokenClient = httpMessageHandler != null
            ? BuildTimedClient(httpMessageHandler)
            : (logger != null ? BuildTimedClient(SharedHandler.Value) : TokenHttpClient.Value);

        var tokenProvider = new ClientCredentialsTokenProvider(
            tokenClient, clientId, clientSecret, tokenEndpointUrl, logger);

        var authProvider = new BaseBearerTokenAuthenticationProvider(tokenProvider);

        var baseAddress = new Uri(apiBaseUrl);
        var apiHttpClient = httpMessageHandler != null
            ? BuildTimedClient(httpMessageHandler, baseAddress)
            : HttpClients.GetOrAdd(baseAddress, uri =>
                BuildTimedClient(SharedHandler.Value, uri));
        var adapter = new HttpClientRequestAdapter(authProvider, httpClient: apiHttpClient);

        return new CbmsSebtApiClient(adapter);
    }
}
