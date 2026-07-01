using System.Text;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SEBT.Portal.StatePlugins.CO.Cbms;
using SEBT.Portal.StatePlugins.CO.Cbms.Cache;
using SEBT.Portal.StatePlugins.CO.CbmsApi;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Mocks;

namespace SEBT.Portal.StatePlugins.CO;

public abstract class ColoradoCbmsServiceBase
{
    private readonly object _clientCacheLock = new();
    private readonly HybridCache? _cache;
    private readonly ILogger _logger;

    private CbmsConnectionOptions? _cachedOptions;
    private CbmsSebtApiClient? _cachedClient;

    /// <summary>
    /// Plugin-wide household-cache singleton. Read paths use this; write paths
    /// also read from it before PATCHing, and (for address updates) write through.
    /// </summary>
    private protected ICbmsHouseholdCache? HouseholdCache { get; }

    /// <summary>
    /// Full constructor for services that use the household cache (e.g., read paths).
    /// Wires up <see cref="HouseholdCache"/> via <see cref="PluginCache.GetOrBuild"/>.
    /// </summary>
    protected ColoradoCbmsServiceBase(
        IServiceProvider hostProvider,
        IConfiguration configuration,
        HybridCache? cache,
        ILogger logger)
    {
        _cache = cache;
        _logger = logger;
        HouseholdCache = PluginCache.GetOrBuild(hostProvider, configuration);
    }

    /// <summary>
    /// Minimal constructor for services that only need <see cref="GetOrCreateClient"/>
    /// and do not use the household cache. <see cref="HouseholdCache"/> will be null
    /// when constructed this way; call sites must not access it.
    /// </summary>
    protected ColoradoCbmsServiceBase(HybridCache? cache, ILogger logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Formats an <see cref="UpdateStudentDetailsRequest"/> as a log-safe summary string for
    /// pre-PATCH diagnostic logging. String fields are reduced to character counts (e.g. <c>4c</c>)
    /// rather than their values, so guardian name, email, and address data never appear in log output.
    /// Enum-like fields (<c>reqNewCard</c>, <c>ntfnOptInSw</c>, <c>ntfnSrc</c>, <c>optOut</c>) are
    /// logged verbatim since they carry no PII.
    /// </summary>
    protected static string FormatPatchBodyForLog(UpdateStudentDetailsRequest b)
    {
        var sb = new StringBuilder();
        sb.Append($"sebtChldId={Len(b.SebtChldId)}");
        sb.Append($", sebtAppId={Len(b.SebtAppId)}");
        sb.Append($", addr={FormatAddressForLog(b.Addr)}");
        sb.Append($", reqNewCard={b.ReqNewCard ?? "null"}");
        sb.Append($", gurdEmailAddr={Len(b.GurdEmailAddr)}");
        sb.Append($", gurdFstNm={Len(b.GurdFstNm)}");
        sb.Append($", gurdLstNm={Len(b.GurdLstNm)}");
        sb.Append($", ntfnOptInSw={b.NtfnOptInSw ?? "null"}");
        sb.Append($", ntfnSrc={b.NtfnSrc ?? "null"}");
        sb.Append($", optOut={b.OptOut ?? "null"}");
        
        return sb.ToString();
    }

    private static string FormatAddressForLog(Address? a)
    {
        if (a is null) 
        {
            return "null";
        }

        var sb = new StringBuilder("present(");
        sb.Append($"addrLn1={Len(a.AddrLn1)}");
        sb.Append($", addrLn2={Len(a.AddrLn2)}");
        sb.Append($", cty={Len(a.Cty)}");
        sb.Append($", staCd={a.StaCd ?? "null"}");
        sb.Append($", zip={Len(a.Zip)}");
        sb.Append($", zip4={a.Zip4 is not null}");
        sb.Append(')');
        
        return sb.ToString();
    }

    private static string Len(string? s) => s is null ? "null" : $"{s.Length}c";

    protected CbmsSebtApiClient GetOrCreateClient(CbmsConnectionOptions options)
    {
        lock (_clientCacheLock)
        {
            if (_cachedOptions == options && _cachedClient != null)
                return _cachedClient;

            HttpMessageHandler? handler = null;
            var clientId = options.ClientId;
            var clientSecret = options.ClientSecret;

            if (options.UseMockResponses)
            {
                if (_cache == null)
                {
                    throw new InvalidOperationException(
                        "HybridCache must be registered in DI when Cbms:UseMockResponses is enabled. " +
                        "Ensure services.AddHybridCache() is called during service registration.");
                }

                clientId = "mock-client-id";
                clientSecret = "mock-client-secret";
                var dataStore = new MockCbmsDataStore(_cache);
                handler = new MockCbmsHttpHandler(dataStore);
            }

            _cachedClient = CbmsSebtApiClientFactory.Create(
                clientId, clientSecret,
                options.ApiBaseUrl, options.TokenEndpointUrl,
                handler, _logger);
            _cachedOptions = options;
            return _cachedClient;
        }
    }
}
