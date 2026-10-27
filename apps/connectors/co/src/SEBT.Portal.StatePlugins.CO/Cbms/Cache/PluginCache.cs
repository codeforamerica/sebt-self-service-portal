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
/// Process-wide holder for <see cref="ICbmsHouseholdCache"/>.
/// Rebuilds when <c>use_mock_cbms_responses</c> changes. Tests substitute a fake
/// via <see cref="OverrideForTesting"/>.
/// </summary>
internal static class PluginCache
{
    private static ICbmsHouseholdCache? _instance;
    private static bool? _builtUseMockResponses;
    private static bool _pinnedForTesting;
    private static readonly object _lock = new();

    public static ICbmsHouseholdCache GetOrBuild(IServiceProvider hostProvider, IConfiguration configuration)
    {
        var current = CurrentInstance(CbmsOptionsHelper.GetCbmsOptions(configuration).UseMockResponses);
        if (current is not null)
        {
            return current;
        }

        lock (_lock)
        {
            var loggerFactory = hostProvider.GetRequiredService<ILoggerFactory>();
            var cbmsLogger = loggerFactory.CreateLogger("CbmsHouseholdCache.CbmsClient");
            var cbmsConnection = CbmsOptionsHelper.GetCbmsOptions(configuration, cbmsLogger);
            current = CurrentInstance(cbmsConnection.UseMockResponses);
            if (current is not null)
            {
                return current;
            }

            var cacheOptions = new CbmsHouseholdCacheOptions();
            configuration.GetSection("Cbms:Cache").Bind(cacheOptions);
            foreach (var error in cacheOptions.Validate())
            {
                throw new InvalidOperationException("Cbms:Cache configuration invalid: " + error);
            }

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

            var built = ActivatorUtilities.CreateInstance<CbmsHouseholdCache>(
                hostProvider,
                Options.Create(cacheOptions),
                fetchFromCbms,
                cbmsConnection.UseMockResponses);
            _instance = built;
            _builtUseMockResponses = cbmsConnection.UseMockResponses;
            _pinnedForTesting = false;

            return built;
        }
    }

    private static ICbmsHouseholdCache? CurrentInstance(bool useMockResponses)
    {
        if (_instance is null)
        {
            return null;
        }

        if (_pinnedForTesting || _builtUseMockResponses == useMockResponses)
        {
            return _instance;
        }

        return null;
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
            _builtUseMockResponses = null;
            _pinnedForTesting = true;
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
            _builtUseMockResponses = null;
            _pinnedForTesting = false;
        }
    }
}
