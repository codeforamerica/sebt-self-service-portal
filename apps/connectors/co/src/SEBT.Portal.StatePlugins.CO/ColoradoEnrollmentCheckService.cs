using System.Composition;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using SEBT.Portal.StatePlugins.CO.Cbms;
using SEBT.Portal.StatePlugins.CO.CbmsApi;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;
using CbmsCheckEnrollmentRequest = SEBT.Portal.StatePlugins.CO.CbmsApi.Models.CheckEnrollmentRequest;

namespace SEBT.Portal.StatePlugins.CO;

/// <summary>
/// Colorado implementation of the enrollment check service.
/// Calls the CBMS API's check-enrollment endpoint to verify child eligibility
/// for Summer EBT benefits.
/// </summary>
[Export(typeof(IStatePlugin))]
[ExportMetadata("StateCode", "CO")]
public class ColoradoEnrollmentCheckService : ColoradoCbmsServiceBase, IEnrollmentCheckService
{
    private const string BaseUrlConfigKey = "COConnector:CbmsApiBaseUrl";
    private const string ApiKeyConfigKey = "COConnector:CbmsApiKey";

    /// <summary>
    /// Default strict greater-than match-confidence threshold: a row's <c>mtchCnfd</c> must
    /// exceed this value to be eligible for the eligibility-gated Match status. Below or equal
    /// -> NonMatch. Overridable per environment via <see cref="MatchConfidenceThresholdConfigKey"/>
    /// (see <see cref="ResolveMatchConfidenceThreshold"/>); editable at runtime via AWS AppConfig.
    /// </summary>
    private const double DefaultMatchConfidenceThreshold = 90.0;

    /// <summary>Inclusive bounds for a valid threshold — CBMS <c>mtchCnfd</c> is a 0-100 percentage.</summary>
    private const double MinMatchConfidenceThreshold = 0.0;
    private const double MaxMatchConfidenceThreshold = 100.0;

    /// <summary>
    /// Configuration key for the match-confidence threshold. Falls back to
    /// <see cref="DefaultMatchConfidenceThreshold"/> when unset; a present value must be a number
    /// within [<see cref="MinMatchConfidenceThreshold"/>, <see cref="MaxMatchConfidenceThreshold"/>]
    /// or <see cref="ResolveMatchConfidenceThreshold"/> throws.
    /// </summary>
    private const string MatchConfidenceThresholdConfigKey = "Cbms:MatchConfidenceThreshold";

    private readonly IConfiguration? _configuration;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes the service. The host should supply <paramref name="configuration"/> so the
    /// CBMS API base URL and API key are read from configuration (or set via env vars).
    /// </summary>
    /// <param name="configuration">Optional. The application configuration; when null, only env vars are checked.</param>
    /// <param name="loggerFactory">Optional. Logger factory for diagnostics.</param>
    [ImportingConstructor]
    public ColoradoEnrollmentCheckService(
        [Import(AllowDefault = true)] IConfiguration? configuration = null,
        [Import(AllowDefault = true)] ILoggerFactory? loggerFactory = null)
    : base(null, loggerFactory?.CreateLogger<ColoradoEnrollmentCheckService>() ?? NullLogger<ColoradoEnrollmentCheckService>.Instance)
    {
        _configuration = configuration;
        _logger = loggerFactory?.CreateLogger<ColoradoEnrollmentCheckService>() ?? NullLogger<ColoradoEnrollmentCheckService>.Instance;
    }

    /// <summary>
    /// Resolves the match-confidence threshold from configuration
    /// (<see cref="MatchConfidenceThresholdConfigKey"/>). Returns
    /// <see cref="DefaultMatchConfidenceThreshold"/> when the value is unset. A present value must be
    /// a number within [<see cref="MinMatchConfidenceThreshold"/>, <see cref="MaxMatchConfidenceThreshold"/>]
    /// (the CBMS <c>mtchCnfd</c> domain); a non-numeric or out-of-range value is a configuration error
    /// and throws, so a typo (e.g. a negative or oversized number) fails loudly instead of silently
    /// skewing eligibility matching. Parsed with the invariant culture so it is environment-independent.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The configured value is non-numeric or outside [0, 100].
    /// </exception>
    internal static double ResolveMatchConfidenceThreshold(IConfiguration? configuration)
    {
        var raw = configuration?[MatchConfidenceThresholdConfigKey];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return DefaultMatchConfidenceThreshold;
        }

        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var threshold)
            || threshold < MinMatchConfidenceThreshold
            || threshold > MaxMatchConfidenceThreshold)
        {
            throw new InvalidOperationException(
                $"Invalid {MatchConfidenceThresholdConfigKey} value '{raw}'. Expected a number between " +
                $"{MinMatchConfidenceThreshold} and {MaxMatchConfidenceThreshold}.");
        }

        return threshold;
    }

    /// <inheritdoc />
    public async Task<EnrollmentCheckResult> CheckEnrollmentAsync(
        EnrollmentCheckRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Children.Count == 0)
        {
            return new EnrollmentCheckResult
            {
                Results = new List<ChildCheckResult>()
            };
        }

        var options = CbmsOptionsHelper.GetCbmsOptions(_configuration);
        if (!options.IsConfigured)
        {
            throw new InvalidOperationException(
                "CBMS API is not configured. Set Cbms:ClientId and Cbms:ClientSecret in appsettings " +
                "or Cbms__ClientId/Cbms__ClientSecret environment variables.");
        }

        if (options.UseMockResponses)
        {
            return BuildMockResult(request.Children);
        }

        // Validate the configured threshold up front so a misconfiguration fails before the CBMS call.
        var matchConfidenceThreshold = ResolveMatchConfidenceThreshold(_configuration);

        var client = GetOrCreateClient(options);

        // Build the CBMS request rows tagged with a 1-based StdReqInd. CBMS echoes
        // the indicator back on each response row so we can correlate by index
        // rather than name+DOB equality (which fails on fuzzy matches like typos).
        var cbmsRequests = BuildCbmsRequests(request.Children);
        var indexToChild = BuildIndexToChildMap(request.Children);

        _logger.LogInformation(
            "CBMS EnrollmentCheck: starting request for {ChildCount} child(ren), {RowCount} CBMS row(s) " +
            "after date-transposition expansion (POST /sebt/check-enrollment)",
            request.Children.Count, cbmsRequests.Count);
        var sw = Stopwatch.StartNew();
        CheckEnrollmentResponse? cbmsResponse;
        try
        {
            cbmsResponse = await client.Sebt.CheckEnrollment.PostAsync(cbmsRequests, cancellationToken: cancellationToken);
        }
        catch (ErrorResponse ex)
        {
            sw.Stop();
            var details = ex.ErrorDetails?
                .Select(d => $"{d.Code ?? "(no code)"}: {d.Message ?? "(no message)"}")
                .ToArray() ?? [];
            _logger.LogError(ex,
                "{Dependency} EnrollmentCheck: check-enrollment failed in {ElapsedMs}ms with StatusCode={StatusCode}, " +
                "ApiName={ApiName}, CorrelationId={CorrelationId}, Timestamp={Timestamp}, ErrorDetails={ErrorDetails}",
                "CBMS", sw.ElapsedMilliseconds, ex.ResponseStatusCode, ex.ApiName, ex.CorrelationId, ex.Timestamp, details);
            throw;
        }
        catch (ApiException ex)
        {
            sw.Stop();
            _logger.LogError(ex,
                "{Dependency} EnrollmentCheck: check-enrollment failed in {ElapsedMs}ms with HTTP {StatusCode}",
                "CBMS", sw.ElapsedMilliseconds, ex.ResponseStatusCode);
            throw;
        }
        sw.Stop();
        _logger.LogInformation(
            "CBMS EnrollmentCheck: completed in {ElapsedMs}ms, returned {DetailCount} student detail(s)",
            sw.ElapsedMilliseconds, cbmsResponse?.StdntDtls?.Count ?? 0);

        var results = CorrelateResults(request.Children, cbmsResponse, indexToChild, _logger, matchConfidenceThreshold);

        return new EnrollmentCheckResult
        {
            Results = results,
            ResponseMessage = cbmsResponse?.RespMsg
        };
    }

    /// <summary>
    /// Builds the CBMS check-enrollment request list from the portal request children.
    /// Each row is tagged with a 1-based <c>StdReqInd</c> string ("1", "2", ...) so
    /// CBMS can echo it on the response and let us correlate without name equality.
    /// </summary>
    internal static List<CbmsCheckEnrollmentRequest> BuildCbmsRequests(IList<ChildCheckRequest> children)
    {
        var requests = new List<CbmsCheckEnrollmentRequest>(children.Count);
        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            var stdReqInd = (i + 1).ToString(CultureInfo.InvariantCulture);

            requests.Add(BuildRow(child, child.DateOfBirth, stdReqInd));

            // When the DOB's month and day are transposable (the swap yields a different
            // valid date), submit a second row under the SAME StdReqInd with month/day
            // swapped. This catches guardians who mis-enter a transposable date (e.g.
            // 04/08 vs 08/04). CBMS echoes the indicator back per submitted row, and
            // CorrelateResults keeps the highest-confidence match for the child, so the
            // correct candidate wins without the portal ever seeing the extra row.
            if (TryTransposeMonthAndDay(child.DateOfBirth) is { } transposedDob)
            {
                requests.Add(BuildRow(child, transposedDob, stdReqInd));
            }
        }

        return requests;
    }

    /// <summary>
    /// Builds a single CBMS check-enrollment row for a child against a specific DOB.
    /// Name and school code pass through unchanged; only the DOB and the shared
    /// <c>StdReqInd</c> vary across the rows emitted for one child.
    /// </summary>
    private static CbmsCheckEnrollmentRequest BuildRow(ChildCheckRequest child, DateOnly dob, string stdReqInd) =>
        new()
        {
            StdFirstName = child.FirstName,
            StdLastName = child.LastName,
            StdDob = dob.ToString("yyyy-MM-dd"),
            StdSchlCd = child.SchoolCode,
            StdReqInd = stdReqInd
        };

    /// <summary>
    /// Returns the month/day-swapped DOB when the swap yields a <em>different</em> valid
    /// calendar date, otherwise <c>null</c>. The swap is only valid when the day can also
    /// serve as a month (1-12); the original month (always 1-12) is in turn always a valid
    /// day in any month, so no day-range check is needed. When month equals day the swap is
    /// a no-op and we return <c>null</c> so no duplicate row is emitted.
    /// </summary>
    internal static DateOnly? TryTransposeMonthAndDay(DateOnly dob)
    {
        if (dob.Day > 12)
        {
            return null;
        }

        var transposed = new DateOnly(dob.Year, dob.Day, dob.Month);
        return transposed == dob ? null : transposed;
    }

    /// <summary>
    /// Builds a 1-based-index -> request-child map mirroring <see cref="BuildCbmsRequests"/>.
    /// Used by <see cref="CorrelateResults"/> to look up the original request child for
    /// each response row's <c>StdReqInd</c>.
    /// </summary>
    private static Dictionary<string, ChildCheckRequest> BuildIndexToChildMap(IList<ChildCheckRequest> children)
    {
        var map = new Dictionary<string, ChildCheckRequest>(StringComparer.Ordinal);
        for (var i = 0; i < children.Count; i++)
        {
            map[(i + 1).ToString(CultureInfo.InvariantCulture)] = children[i];
        }
        return map;
    }

    /// <summary>
    /// Correlates CBMS response student details to the original request children by the
    /// echoed <c>StdReqInd</c>. For each child:
    ///   - zero rows -> <see cref="EnrollmentStatus.NonMatch"/> with a "no matching record" message;
    ///   - 1+ rows -> highest <c>MtchCnfd</c> wins; status flows through <see cref="MapEnrollmentStatus"/>
    ///     when above <paramref name="matchConfidenceThreshold"/>, otherwise NonMatch.
    /// <c>MatchConfidence</c> is always populated from the winning row, even on the sub-threshold
    /// NonMatch path, so logs and UI can surface the score we computed.
    /// </summary>
    /// <param name="matchConfidenceThreshold">
    /// Strict greater-than threshold a row's <c>MtchCnfd</c> must exceed to be eligible for Match;
    /// defaults to <see cref="DefaultMatchConfidenceThreshold"/>. Callers resolve the configured
    /// value via <see cref="ResolveMatchConfidenceThreshold"/>.
    /// </param>
    internal static IList<ChildCheckResult> CorrelateResults(
        IList<ChildCheckRequest> requestChildren,
        CheckEnrollmentResponse? cbmsResponse,
        IReadOnlyDictionary<string, ChildCheckRequest> indexToChild,
        ILogger? logger = null,
        double matchConfidenceThreshold = DefaultMatchConfidenceThreshold)
    {
        var details = cbmsResponse?.StdntDtls ?? new List<CheckEnrollmentStudentDetail>();

        var orphanCount = 0;
        var keyedRows = new List<CheckEnrollmentStudentDetail>(details.Count);
        foreach (var row in details)
        {
            if (string.IsNullOrEmpty(row.StdReqInd))
            {
                orphanCount++;
                continue;
            }
            keyedRows.Add(row);
        }

        if (orphanCount > 0)
        {
            (logger ?? NullLogger.Instance).LogWarning(
                "CBMS EnrollmentCheck: response contained {OrphanCount} row(s) with no StdReqInd; dropped from correlation",
                orphanCount);
        }

        var rowsByReqInd = keyedRows
            .GroupBy(r => r.StdReqInd!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var results = new List<ChildCheckResult>(requestChildren.Count);
        for (var i = 0; i < requestChildren.Count; i++)
        {
            var key = (i + 1).ToString(CultureInfo.InvariantCulture);
            var child = indexToChild.TryGetValue(key, out var mapped) ? mapped : requestChildren[i];

            if (!rowsByReqInd.TryGetValue(key, out var rowsForChild) || rowsForChild.Count == 0)
            {
                results.Add(new ChildCheckResult
                {
                    CheckId = child.CheckId,
                    FirstName = child.FirstName,
                    LastName = child.LastName,
                    DateOfBirth = child.DateOfBirth,
                    Status = EnrollmentStatus.NonMatch,
                    StatusMessage = "No matching record found in CBMS response"
                });
                continue;
            }

            var best = rowsForChild
                .OrderByDescending(r => r.MtchCnfd ?? double.NegativeInfinity)
                .First();

            var sebtEligSts = best.AdditionalData.TryGetValue("sebtEligSts", out var value)
                ? value?.ToString()
                : null;

            var aboveThreshold = (best.MtchCnfd ?? double.NegativeInfinity) > matchConfidenceThreshold;
            var status = aboveThreshold
                ? MapEnrollmentStatus(sebtEligSts)
                : EnrollmentStatus.NonMatch;

            results.Add(new ChildCheckResult
            {
                CheckId = child.CheckId,
                FirstName = best.StdFstNm ?? child.FirstName,
                LastName = best.StdLstNm ?? child.LastName,
                DateOfBirth = TryParseDob(best.StdDob) ?? child.DateOfBirth,
                Status = status,
                MatchConfidence = best.MtchCnfd,
                StatusMessage = sebtEligSts
            });
        }

        return results;
    }

    /// <summary>
    /// Maps CBMS sebtEligSts values (Y/N) to the standard <see cref="EnrollmentStatus"/> enum.
    /// </summary>
    internal static EnrollmentStatus MapEnrollmentStatus(string? sebtEligSts)
    {
        return sebtEligSts?.ToUpperInvariant() switch
        {
            "Y" => EnrollmentStatus.Match,
            _ => EnrollmentStatus.NonMatch
        };
    }

    private static DateOnly? TryParseDob(string? stdDob) =>
        DateOnly.TryParseExact(stdDob, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;

    /// <summary>
    /// Returns a hardcoded result set when <c>Cbms:UseMockResponses</c> is enabled.
    /// Children with the last name "Wibert" (case-insensitive) are returned as Match
    /// with 95% confidence; all others are NonMatch.
    /// Test persona: first name "Dimple", last name "Wibert".
    /// </summary>
    private static EnrollmentCheckResult BuildMockResult(IList<ChildCheckRequest> children)
    {
        var results = children.Select(child =>
        {
            var isMatch = child.LastName.Equals("Wibert", StringComparison.OrdinalIgnoreCase);
            return new ChildCheckResult
            {
                CheckId = child.CheckId,
                FirstName = child.FirstName,
                LastName = child.LastName,
                DateOfBirth = child.DateOfBirth,
                Status = isMatch ? EnrollmentStatus.Match : EnrollmentStatus.NonMatch,
                MatchConfidence = isMatch ? 95.0 : null
            };
        }).ToList();

        return new EnrollmentCheckResult { Results = results };
    }
}
