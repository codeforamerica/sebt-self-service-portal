using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.StatePlugins.CO.CbmsApi;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Mocks;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Sebt.GetAccountDetails;

namespace SEBT.Portal.StatePlugins.CO.Cbms.Cache;

/// <summary>
/// Process-wide singleton holder for <see cref="ICbmsHouseholdCache"/>.
/// Constructed lazily on first call using the host service provider for shared
/// services (HybridCache, IHMACSHA256Hasher, IHostApplicationLifetime,
/// ILoggerFactory) and explicit args for plugin-internal options and the CBMS
/// fetch delegate. Tests substitute a fake via <see cref="OverrideForTesting"/>.
/// </summary>
internal static class PluginCache
{
    private static ICbmsHouseholdCache? _instance;
    private static readonly object _lock = new();

    public static ICbmsHouseholdCache GetOrBuild(IServiceProvider hostProvider, IConfiguration configuration)
    {
        if (_instance is not null) return _instance;
        lock (_lock)
        {
            if (_instance is not null) return _instance;

            var cacheOptions = new CbmsHouseholdCacheOptions();
            configuration.GetSection("Cbms:Cache").Bind(cacheOptions);
            foreach (var error in cacheOptions.Validate())
            {
                throw new InvalidOperationException("Cbms:Cache configuration invalid: " + error);
            }

            // Build the CBMS fetch delegate once using the resolved connection options.
            var cbmsConnection = CbmsOptionsHelper.GetCbmsOptions(configuration);
            var loggerFactory = hostProvider.GetRequiredService<ILoggerFactory>();
            var cbmsLogger = loggerFactory.CreateLogger("CbmsHouseholdCache.CbmsClient");

            HttpMessageHandler? handler = null;
            if (cbmsConnection.UseMockResponses)
            {
                var hybridCache = hostProvider.GetRequiredService<Microsoft.Extensions.Caching.Hybrid.HybridCache>();
                var dataStore = new MockCbmsDataStore(hybridCache);
                handler = new MockCbmsHttpHandler(dataStore);
            }

            var cbmsClient = CbmsSebtApiClientFactory.Create(
                cbmsConnection.UseMockResponses ? "mock-client-id" : cbmsConnection.ClientId,
                cbmsConnection.UseMockResponses ? "mock-client-secret" : cbmsConnection.ClientSecret,
                cbmsConnection.ApiBaseUrl,
                cbmsConnection.TokenEndpointUrl,
                handler,
                cbmsLogger);

            CbmsFetchAccountDetailsDelegate fetchFromCbms = BuildFetchDelegate(cbmsClient);

            _instance = ActivatorUtilities.CreateInstance<CbmsHouseholdCache>(
                hostProvider,
                Options.Create(cacheOptions),
                fetchFromCbms);

            return _instance;
        }
    }

    /// <summary>
    /// Builds the delegate used to fetch account details from CBMS.
    /// Extracted for testability; production code calls this via <see cref="GetOrBuild"/>.
    /// </summary>
    internal static CbmsFetchAccountDetailsDelegate BuildFetchDelegate(CbmsSebtApiClient client)
    {
        return async (phone, includeCardService, ct) =>
        {
            try
            {
                var request = new GetAccountDetailsRequest { PhnNm = phone };
                var ebtCardService = includeCardService
                    ? PostEbtCardServiceQueryParameterType.Y
                    : PostEbtCardServiceQueryParameterType.N;
                return await client.Sebt.GetAccountDetails
                    .PostAsync(request, x => x.QueryParameters.EbtCardService = ebtCardService, cancellationToken: ct)
                    .ConfigureAwait(false);
            }
            catch (ErrorResponse ex) when (ex.ResponseStatusCode == 404)
            {
                // CBMS returns 404 when no household is found for the given phone.
                // Return null to trigger negative-cache storage.
                return null;
            }
        };
    }

    /// <summary>
    /// Replaces the singleton with a test double. Call before exercising code
    /// paths that invoke <see cref="GetOrBuild"/>.
    /// </summary>
    internal static void OverrideForTesting(ICbmsHouseholdCache testCache)
    {
        lock (_lock)
        {
            _instance = testCache;
        }
    }

    /// <summary>
    /// Clears the singleton so the next <see cref="GetOrBuild"/> call rebuilds it.
    /// Must be called in test teardown to prevent state leaking between tests.
    /// </summary>
    internal static void ResetForTesting()
    {
        lock (_lock)
        {
            _instance = null;
        }
    }
}
