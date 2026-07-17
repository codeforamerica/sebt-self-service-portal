using System.Composition;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Kiota.Abstractions;
using SEBT.Portal.StatePlugins.CO.Cbms;
using SEBT.Portal.StatePlugins.CO.CbmsApi;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

namespace SEBT.Portal.StatePlugins.CO;

/// <summary>
/// Colorado address updates via CBMS <c>update-std-dtls</c> (student mailing address).
/// Resolves the household with get-account-details using <see cref="PhoneNormalizer"/> on
/// <see cref="AddressUpdateRequest.HouseholdIdentifierValue"/>; uses the same normalization as <see cref="ColoradoSummerEbtCaseService"/> phone lookup.
/// When multiple children are on the account, CBMS receives one PATCH whose body is an array of update payloads
/// (same portal address mapped per student row from get-account-details).
/// Successful updates require CBMS <c>respCd</c> <c>200</c> (OpenAPI example) or <c>00</c> (observed UAT); other codes are failure.
/// After a successful PATCH the cached household response is updated in-memory and written through to the cache
/// so that subsequent reads reflect the change without a round-trip to CBMS.
/// </summary>
[Export(typeof(IAddressUpdateService))]
[Export(typeof(IStatePlugin))]
[ExportMetadata("StateCode", "CO")]
public class ColoradoAddressUpdateService : ColoradoCbmsServiceBase, IAddressUpdateService
{
    private readonly IConfiguration? _configuration;
    private readonly ILogger _logger;

    // Only non-null when constructed via the internal test ctor. Allows tests to inject
    // a fake HttpMessageHandler for the PATCH path while the read path is served by
    // the PluginCache substitute set via PluginCache.OverrideForTesting.
    private readonly HttpMessageHandler? _testHttpMessageHandler;

    [ImportingConstructor]
    public ColoradoAddressUpdateService(
        [Import] IServiceProvider hostProvider,
        [Import(AllowDefault = true)] IConfiguration? configuration = null,
        [Import(AllowDefault = true)] ILoggerFactory? loggerFactory = null,
        HybridCache? cache = null)
        : base(
            hostProvider,
            configuration ?? throw new InvalidOperationException("IConfiguration required."),
            cache,
            loggerFactory?.CreateLogger<ColoradoAddressUpdateService>() ?? NullLogger<ColoradoAddressUpdateService>.Instance)
    {
        _configuration = configuration;
        _logger = loggerFactory?.CreateLogger<ColoradoAddressUpdateService>() ?? NullLogger<ColoradoAddressUpdateService>.Instance;
    }

    internal ColoradoAddressUpdateService(
        IServiceProvider hostProvider,
        IConfiguration? configuration,
        HttpMessageHandler? testHttpMessageHandler,
        HybridCache? cache = null,
        ILogger? logger = null)
        : base(
            hostProvider,
            configuration ?? throw new InvalidOperationException("IConfiguration required."),
            cache,
            logger ?? NullLogger<ColoradoAddressUpdateService>.Instance)
    {
        _configuration = configuration;
        _testHttpMessageHandler = testHttpMessageHandler;
        _logger = logger ?? NullLogger<ColoradoAddressUpdateService>.Instance;
    }

    /// <inheritdoc />
    public async Task<AddressUpdateResult> UpdateAddressAsync(
        AddressUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Address is null)
        {
            return AddressUpdateResult.PolicyRejected("INVALID_ADDRESS", "Address is required.");
        }

        if (request.HouseholdIdentifierValue is null)
        {
            return AddressUpdateResult.PolicyRejected(
                "INVALID_IDENTIFIER",
                "Colorado CBMS requires a valid US phone number in HouseholdIdentifierValue.");
        }

        if (!CbmsAddressUpdateMapper.TryValidatePortalAddress(request.Address, out var validationError))
        {
            return AddressUpdateResult.PolicyRejected("INVALID_ADDRESS", validationError ?? "Invalid address.");
        }

        var phone10 = PhoneNormalizer.Normalize(request.HouseholdIdentifierValue);
        if (string.IsNullOrEmpty(phone10))
        {
            return AddressUpdateResult.PolicyRejected(
                "INVALID_IDENTIFIER",
                "Colorado CBMS requires a valid US phone number in HouseholdIdentifierValue, same normalization as household lookup.");
        }

        var options = CbmsOptionsHelper.GetCbmsOptions(_configuration);
        if (string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            return AddressUpdateResult.BackendError(
                "NOT_CONFIGURED",
                "CBMS credentials are not configured. Set Cbms:ClientId and Cbms:ClientSecret (or Cbms__* env vars).");
        }

        if (string.IsNullOrWhiteSpace(options.ApiBaseUrl) || string.IsNullOrWhiteSpace(options.TokenEndpointUrl))
        {
            return AddressUpdateResult.BackendError(
                "NOT_CONFIGURED",
                "CBMS endpoint URLs are not configured. Set Cbms:ApiBaseUrl and Cbms:TokenEndpointUrl (or Cbms__* env vars).");
        }

        GetAccountDetailsResponse? accountResponse;
        try
        {
            accountResponse = await HouseholdCache!.GetAsync(phone10, includeCardService: true, cancellationToken).ConfigureAwait(false);
        }
        catch (ErrorResponse ex)
        {
            _logger.LogError(ex,
                "{Dependency} AddressUpdate: get-account-details (cache) failed with StatusCode={StatusCode}",
                "CBMS", ex.ResponseStatusCode);
            return MapErrorResponse(ex);
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex,
                "{Dependency} AddressUpdate: get-account-details (cache) failed with HTTP {StatusCode}",
                "CBMS", ex.ResponseStatusCode);
            return BackendErrorFromApiException(ex);
        }

        if (accountResponse is null)
        {
            return AddressUpdateResult.PolicyRejected(
                "HOUSEHOLD_NOT_FOUND",
                "CBMS get-account-details returned no enrollment rows for the household identifier used for lookup. " +
                "Confirm this environment (UAT vs production) and that the guardian phone on the SEBT account matches lookup normalization, same as household by phone.");
        }

        var students = accountResponse.StdntEnrollDtls ?? [];
        var actionable = students
            .Select(row => (Row: row, Ids: CbmsGetAccountStudentDetailIds.Resolve(row)))
            .Where(x => CbmsGetAccountStudentDetailIds.CanBuildUpdatePayload(x.Ids))
            .ToList();

        if (actionable.Count == 0)
        {
            if (students.Count == 0)
            {
                return AddressUpdateResult.PolicyRejected(
                    "NO_ENROLLMENT_ROWS",
                    "CBMS get-account-details returned a non-null account response with an empty enrollment list " +
                    "(zero stdntEnrollDtls rows) for the household identifier used for lookup. The household resolved " +
                    "but carries no SEBT enrollment records to update.");
            }

            return AddressUpdateResult.PolicyRejected(
                "NO_ACTIONABLE_ENROLLMENT",
                $"CBMS returned {students.Count} enrollment row(s), but none had usable sebtChldId or sebtAppId after parsing. " +
                CbmsGetAccountStudentDetailIds.FormatDiagnosticsHint(students[0]));
        }

        var client = GetOrCreateClient(options);

        var updateBodies = actionable
            .Select(x => CbmsAddressUpdateMapper.ToUpdateStudentDetailsRequest(
                request.Address,
                x.Row,
                x.Ids.SebtChldId,
                x.Ids.SebtAppId))
            .ToList();

        try
        {
            _logger.LogInformation(
                "CBMS AddressUpdate: updating {StudentCount} student(s) (PATCH /sebt/update-std-dtls)",
                updateBodies.Count);
            for (var i = 0; i < updateBodies.Count; i++)
            {
                _logger.LogInformation("CBMS AddressUpdate: body[{Index}] {Fields}", i, FormatPatchBodyForLog(updateBodies[i]));
            }
            var updateResponse = await client.Sebt.UpdateStdDtls.PatchAsync(updateBodies, cancellationToken: cancellationToken);
            _logger.LogInformation(
                "CBMS AddressUpdate: update-std-dtls completed, respCd={RespCd}",
                updateResponse?.RespCd ?? "(null)");

            if (updateResponse != null && IsCbmsUpdateSuccessCode(updateResponse.RespCd))
            {
                // Write-through: update the cached response so subsequent reads reflect the change.
                foreach (var (row, _) in actionable)
                {
                    CbmsAddressUpdateMapper.ApplyAddressToRow(request.Address, row);
                }
                await HouseholdCache!.SetAsync(phone10, accountResponse, cancellationToken).ConfigureAwait(false);

                return AddressUpdateResult.Success();
            }

            var msg = updateResponse?.RespMsg ?? "CBMS returned an unexpected update response.";
            return AddressUpdateResult.BackendError("CBMS_UPDATE_FAILED", msg);
        }
        catch (ErrorResponse ex)
        {
            _logger.LogError(ex,
                "{Dependency} AddressUpdate: update-std-dtls failed with StatusCode={StatusCode}",
                "CBMS", ex.ResponseStatusCode);
            return MapErrorResponse(ex);
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex,
                "{Dependency} AddressUpdate: update-std-dtls failed with HTTP {StatusCode}",
                "CBMS", ex.ResponseStatusCode);
            return BackendErrorFromApiException(ex);
        }
    }

    /// <summary>
    /// Overrides the base <see cref="ColoradoCbmsServiceBase.GetOrCreateClient"/> when a test HTTP handler
    /// has been injected via the internal constructor. This preserves the test seam for the PATCH path
    /// while the read path is served by the <see cref="PluginCache"/> substitute.
    /// In production (where <see cref="_testHttpMessageHandler"/> is null) the base implementation is used.
    /// </summary>
    protected new CbmsSebtApiClient GetOrCreateClient(CbmsConnectionOptions options)
    {
        if (_testHttpMessageHandler is null)
            return base.GetOrCreateClient(options);

        // Test path: build a client with the injected handler each time (no caching needed in tests).
        return CbmsSebtApiClientFactory.Create(
            options.ClientId,
            options.ClientSecret,
            options.ApiBaseUrl,
            options.TokenEndpointUrl,
            _testHttpMessageHandler,
            _logger);
    }

    /// <summary>Kiota uses the HTTP reason phrase (e.g. "Bad Request") as <see cref="ApiException.Message"/> — include status for clarity.</summary>
    private static AddressUpdateResult BackendErrorFromApiException(ApiException ex)
    {
        var status = ex.ResponseStatusCode;
        var msg = string.IsNullOrWhiteSpace(ex.Message) ? "(no error message)" : ex.Message.Trim();
        var detail = status is >= 100 and < 600 ? $"HTTP {(int)status}: {msg}" : msg;
        return AddressUpdateResult.BackendError("CBMS_ERROR", detail);
    }

    /// <summary>CBMS examples use "200"; live UAT has returned "00" with respMsg Success.</summary>
    internal static bool IsCbmsUpdateSuccessCode(string? respCd) =>
        string.Equals(respCd, "200", StringComparison.Ordinal)
        || string.Equals(respCd, "00", StringComparison.Ordinal);

    private static AddressUpdateResult MapErrorResponse(ErrorResponse ex)
    {
        var message = FormatErrorResponse(ex);
        if (ex.ResponseStatusCode == 404)
            return AddressUpdateResult.PolicyRejected("CBMS_404", message);
        return AddressUpdateResult.BackendError($"CBMS_{ex.ResponseStatusCode}", message);
    }

    /// <summary>
    /// CBMS 4xx/5xx responses deserialize to <see cref="ErrorResponse"/>; when the body omits
    /// <c>errorDetails</c>, Kiota leaves <see cref="ApiException.Message"/> as the HTTP reason phrase only (e.g. "Bad Request").
    /// Prefix with the status code (and surface correlation id) so the portal does not return a bare phrase as ProblemDetails detail.
    /// </summary>
    private static string FormatErrorResponse(ErrorResponse ex)
    {
        var status = ex.ResponseStatusCode;
        var prefix = status is >= 100 and < 600
            ? $"HTTP {status}"
            : "CBMS error";

        var fromDetails = ex.ErrorDetails?
            .Select(d => d.Message)
            .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))
            ?.Trim();

        var body = !string.IsNullOrWhiteSpace(fromDetails)
            ? fromDetails
            : (string.IsNullOrWhiteSpace(ex.Message) ? "(no message)" : ex.Message.Trim());

        var msg = $"{prefix}: {body}";
        if (!string.IsNullOrWhiteSpace(ex.CorrelationId))
            msg += $" (correlationId: {ex.CorrelationId})";

        return msg;
    }
}
