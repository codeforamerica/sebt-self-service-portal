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
/// Colorado card-replacement via CBMS <c>update-std-dtls</c> with <c>reqNewCard = "Y"</c>.
/// Resolves the household via <see cref="ICbmsHouseholdCache"/> using <see cref="PhoneNormalizer"/> on
/// <see cref="CardReplacementRequest.HouseholdIdentifierValue"/>, filters enrollment rows to
/// those whose <c>sebtChldCwin</c> matches the requested <see cref="CardReplacementRequest.CaseRefs"/>,
/// then sends a single PATCH with one array element per matched student. The portal's
/// <c>SummerEBTCaseID</c> maps to CBMS <c>sebtChldCwin</c> (cross-year); the PATCH body uses the
/// per-year <c>sebtChldId</c> / <c>sebtAppId</c>.
/// Successful updates require CBMS <c>respCd</c> <c>200</c> (spec) or <c>00</c> (observed UAT).
/// Card-replacement cooldown is tracked in the portal database — no write-through to the cache.
/// </summary>
[Export(typeof(IStatePlugin))]
[ExportMetadata("StateCode", "CO")]
public class ColoradoCardReplacementService : ColoradoCbmsServiceBase, ICardReplacementService
{
    private readonly IConfiguration? _configuration;
    private readonly ILogger _logger;

    // Only non-null when constructed via the internal test ctor. Allows tests to inject
    // a fake HttpMessageHandler for the PATCH path while the read path is served by
    // the PluginCache substitute set via PluginCache.OverrideForTesting.
    private readonly HttpMessageHandler? _testHttpMessageHandler;

    [ImportingConstructor]
    public ColoradoCardReplacementService(
        [Import] IServiceProvider hostProvider,
        [Import(AllowDefault = true)] IConfiguration? configuration = null,
        [Import(AllowDefault = true)] ILoggerFactory? loggerFactory = null,
        HybridCache? cache = null)
        : base(
            hostProvider,
            configuration ?? throw new InvalidOperationException("IConfiguration required."),
            cache,
            loggerFactory?.CreateLogger<ColoradoCardReplacementService>() ?? NullLogger<ColoradoCardReplacementService>.Instance)
    {
        _configuration = configuration;
        _logger = loggerFactory?.CreateLogger<ColoradoCardReplacementService>() ?? NullLogger<ColoradoCardReplacementService>.Instance;
    }

    internal ColoradoCardReplacementService(
        IServiceProvider hostProvider,
        IConfiguration? configuration,
        HttpMessageHandler? testHttpMessageHandler,
        HybridCache? cache = null,
        ILogger? logger = null)
        : base(
            hostProvider,
            configuration ?? throw new InvalidOperationException("IConfiguration required."),
            cache,
            logger ?? NullLogger<ColoradoCardReplacementService>.Instance)
    {
        _configuration = configuration;
        _testHttpMessageHandler = testHttpMessageHandler;
        _logger = logger ?? NullLogger<ColoradoCardReplacementService>.Instance;
    }

    /// <inheritdoc />
    public async Task<CardReplacementResult> RequestCardReplacementAsync(
        CardReplacementRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.HouseholdIdentifierValue is null)
        {
            return CardReplacementResult.PolicyRejected(
                "INVALID_IDENTIFIER",
                "Colorado CBMS requires a valid US phone number in HouseholdIdentifierValue.");
        }

        if (request.CaseRefs is null || request.CaseRefs.Count == 0)
        {
            return CardReplacementResult.PolicyRejected(
                "INVALID_CASE_IDS",
                "At least one case id is required.");
        }

        var phone10 = PhoneNormalizer.Normalize(request.HouseholdIdentifierValue);
        if (string.IsNullOrEmpty(phone10))
        {
            return CardReplacementResult.PolicyRejected(
                "INVALID_IDENTIFIER",
                "Colorado CBMS requires a valid US phone number in HouseholdIdentifierValue, same normalization as household lookup.");
        }

        var options = CbmsOptionsHelper.GetCbmsOptions(_configuration);
        if (string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            return CardReplacementResult.BackendError(
                "NOT_CONFIGURED",
                "CBMS credentials are not configured. Set Cbms:ClientId and Cbms:ClientSecret (or Cbms__* env vars).");
        }

        if (string.IsNullOrWhiteSpace(options.ApiBaseUrl) || string.IsNullOrWhiteSpace(options.TokenEndpointUrl))
        {
            return CardReplacementResult.BackendError(
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
                "{Dependency} CardReplacement: get-account-details (cache) failed with StatusCode={StatusCode}",
                "CBMS", ex.ResponseStatusCode);
            return MapErrorResponse(ex);
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex,
                "{Dependency} CardReplacement: get-account-details (cache) failed with HTTP {StatusCode}",
                "CBMS", ex.ResponseStatusCode);
            return BackendErrorFromApiException(ex);
        }

        if (accountResponse is null)
        {
            return CardReplacementResult.PolicyRejected(
                "HOUSEHOLD_NOT_FOUND",
                "CBMS get-account-details returned no enrollment rows for the household identifier.");
        }

        var students = accountResponse.StdntEnrollDtls ?? [];
        if (students.Count == 0)
        {
            return CardReplacementResult.PolicyRejected(
                "HOUSEHOLD_NOT_FOUND",
                "CBMS get-account-details returned no enrollment rows for the household identifier.");
        }

        // Pre-filter DD rows. They must never be acted on regardless of how the request resolves to them.
        var actionableStudents = students
            .Where(s => !CbmsCaseFilters.IsDeniedDuplicate(s))
            .ToList();

        // Resolve each requested CaseRef to at most one row.
        // Prefer the (appId, childId) pair when both are present on the CaseRef (application-based
        // active cases — uniquely keyed). Fall back to cwin only when the CaseRef carries no app/child
        // ids (auto-eligible active cases — DD does not apply to DIRC/CDE rows, so cwin is unambiguous
        // in that branch after the DD pre-filter).
        var matched = new List<(GetAccountStudentDetail Row, CbmsGetAccountStudentDetailIds.ResolvedIds Ids)>();
        foreach (var caseRef in request.CaseRefs)
        {
            var row = actionableStudents.FirstOrDefault(s => MatchesCaseRef(s, caseRef));
            if (row is null) continue;

            var ids = CbmsGetAccountStudentDetailIds.Resolve(row);
            if (CbmsGetAccountStudentDetailIds.CanBuildUpdatePayload(ids))
                matched.Add((row, ids));
        }

        // FirstOrDefault per CaseRef guarantees matched.Count <= request.CaseRefs.Count, so a strict
        // less-than check. This is a defense in depth protection against PATCHing an empty array to
        // CBMS but in theory this path should not get hit. Message intentionally generic.
        if (matched.Count < request.CaseRefs.Count)
        {
            return CardReplacementResult.PolicyRejected(
                "CASES_NOT_FOUND",
                $"Requested {request.CaseRefs.Count} case(s), but only {matched.Count} matched usable CBMS enrollment row(s). " +
                "Portal case list may be stale; ask the user to refresh and retry.");
        }

        var updateBodies = matched
            .Select(x => CbmsCardReplacementMapper.ToCardReplacementBody(
                x.Row,
                x.Ids.SebtChldId,
                x.Ids.SebtAppId))
            .ToList();

        var client = GetOrCreateClient(options);

        try
        {
            _logger.LogInformation(
                "CBMS CardReplacement: requesting new card for {StudentCount} student(s) (PATCH /sebt/update-std-dtls)",
                updateBodies.Count);
            for (var i = 0; i < updateBodies.Count; i++)
            {
                _logger.LogInformation("CBMS CardReplacement: body[{Index}] {Fields}", i, FormatPatchBodyForLog(updateBodies[i]));
            }
            var patchSw = System.Diagnostics.Stopwatch.StartNew();
            var updateResponse = await client.Sebt.UpdateStdDtls.PatchAsync(updateBodies, cancellationToken: cancellationToken);
            patchSw.Stop();
            _logger.LogInformation(
                "CBMS CardReplacement: update-std-dtls completed in {ElapsedMs}ms, respCd={RespCd}",
                patchSw.ElapsedMilliseconds, updateResponse?.RespCd ?? "(null)");

            if (updateResponse != null && ColoradoAddressUpdateService.IsCbmsUpdateSuccessCode(updateResponse.RespCd))
            {
                // No write-through: card-replacement cooldown is tracked in the portal database.
                return CardReplacementResult.Success();
            }

            var msg = updateResponse?.RespMsg ?? "CBMS returned an unexpected card-replacement response.";
            return CardReplacementResult.BackendError("CBMS_UPDATE_FAILED", msg);
        }
        catch (ErrorResponse ex)
        {
            _logger.LogError(ex,
                "{Dependency} CardReplacement: update-std-dtls failed with StatusCode={StatusCode}",
                "CBMS", ex.ResponseStatusCode);
            return MapErrorResponse(ex);
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex,
                "{Dependency} CardReplacement: update-std-dtls failed with HTTP {StatusCode}",
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

    /// <summary>
    /// Matches a CBMS enrollment row to a portal-supplied <see cref="CaseRef"/>.
    /// Prefers the unique <c>(sebtAppId, sebtChldId)</c> pair when both are present on the CaseRef
    /// (application-based active cases). Falls back to <c>sebtChldCwin</c> matching when the CaseRef
    /// carries no app/child ids (auto-eligible active cases — the portal model exposes only the cwin
    /// for these, since <c>CbmsResponseMapper</c> populates <c>ApplicationId</c>/<c>ApplicationStudentId</c>
    /// only when <c>eligSrc ∈ {CBMS, PK}</c>).
    /// </summary>
    private static bool MatchesCaseRef(GetAccountStudentDetail row, CaseRef caseRef)
    {
        if (!string.IsNullOrEmpty(caseRef.ApplicationId)
            && !string.IsNullOrEmpty(caseRef.ApplicationStudentId))
        {
            return string.Equals(
                    row.SebtAppId?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    caseRef.ApplicationId, StringComparison.Ordinal)
                && string.Equals(
                    row.SebtChldId?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    caseRef.ApplicationStudentId, StringComparison.Ordinal);
        }
        return string.Equals(
            row.SebtChldCwin?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            caseRef.SummerEbtCaseId, StringComparison.Ordinal);
    }

    /// <summary>Kiota uses the HTTP reason phrase (e.g. "Bad Request") as <see cref="ApiException.Message"/> — include status for clarity.</summary>
    private static CardReplacementResult BackendErrorFromApiException(ApiException ex)
    {
        var status = ex.ResponseStatusCode;
        var msg = string.IsNullOrWhiteSpace(ex.Message) ? "(no error message)" : ex.Message.Trim();
        var detail = status is >= 100 and < 600 ? $"HTTP {(int)status}: {msg}" : msg;
        return CardReplacementResult.BackendError("CBMS_ERROR", detail);
    }

    private static CardReplacementResult MapErrorResponse(ErrorResponse ex)
    {
        var message = FormatErrorResponse(ex);
        if (ex.ResponseStatusCode == 404)
            return CardReplacementResult.PolicyRejected("CBMS_404", message);
        return CardReplacementResult.BackendError($"CBMS_{ex.ResponseStatusCode}", message);
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
