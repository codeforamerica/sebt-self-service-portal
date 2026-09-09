using System.Globalization;
using System.Reflection;
using System.Text.Json;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;

namespace SEBT.Portal.StatePlugins.CO.Cbms;

/// <summary>
/// Resolves student identifiers from get-account-details rows. Kiota only binds JSON keys that match
/// the OpenAPI camelCase names (e.g. <c>sebtChldId</c>). If CBMS returns different casing or aliases,
/// values may land in <see cref="GetAccountStudentDetail.AdditionalData"/> instead of the typed properties.
/// </summary>
internal static class CbmsGetAccountStudentDetailIds
{
    public readonly record struct ResolvedIds(string? SebtChldId, string? SebtAppId);

    private static readonly string[] ChldIdKeyCandidates =
    [
        "sebtChldId", "SebtChldId", "SEBT_CHLD_ID", "sebtChildId", "SebtChildId", "SEBT_CHILD_ID",
        "sebt_chld_id", "sebt_child_id", "chldId", "childId", "ChldId", "ChildId", "studentId", "StudentId"
    ];

    private static readonly string[] AppIdKeyCandidates =
    [
        "sebtAppId", "SebtAppId", "SEBT_APP_ID", "sebt_app_id", "appId", "AppId", "SEBTAPPID"
    ];

    /// <summary>
    /// Returns the best-available child / application ids for building update-std-dtls payloads.
    /// </summary>
    public static ResolvedIds Resolve(GetAccountStudentDetail row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var chld = FirstNonEmpty(
            FormatIntId(row.SebtChldId),
            GetFromAdditionalCaseInsensitive(row, ChldIdKeyCandidates),
            FindByHeuristic(row, forChildId: true));

        var app = FirstNonEmpty(
            FormatIntId(row.SebtAppId),
            GetFromAdditionalCaseInsensitive(row, AppIdKeyCandidates),
            FindByHeuristic(row, forChildId: false));

        return new ResolvedIds(chld, app);
    }

    /// <summary>
    /// True when the row can be turned into an <see cref="UpdateStudentDetailsRequest"/> (needs at least one id).
    /// </summary>
    public static bool CanBuildUpdatePayload(ResolvedIds ids) =>
        !string.IsNullOrWhiteSpace(ids.SebtChldId) || !string.IsNullOrWhiteSpace(ids.SebtAppId);

    /// <summary>
    /// Non-PII hint for errors: property names Kiota left in <see cref="GetAccountStudentDetail.AdditionalData"/>.
    /// </summary>
    public static string FormatDiagnosticsHint(GetAccountStudentDetail row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.AdditionalData.Count > 0)
        {
            var keys = string.Join(", ", row.AdditionalData.Keys.Order(StringComparer.Ordinal));
            return $"Unmapped JSON properties on the first row: {keys}. Compare to OpenAPI field names (camelCase) or share this list with the API team.";
        }

        var populated = DescribeNonIdTypedFieldsPresent(row);
        if (populated.Count > 0)
        {
            return
                "All JSON keys matched the OpenAPI model, but the connector found no usable student/application identifiers on the row. " +
                DescribeCorrelationIdsAllEmpty(row) + " " +
                $"Other non-empty fields on the row: {string.Join(", ", populated)}.";
        }

        return "The enrollment row deserialized as an empty object (no ids and no other populated fields).";
    }

    private static string DescribeCorrelationIdsAllEmpty(GetAccountStudentDetail row)
    {
        var empty = new List<string>();
        if (row.SebtChldId is null)
            empty.Add(nameof(GetAccountStudentDetail.SebtChldId));
        if (row.SebtAppId is null)
            empty.Add(nameof(GetAccountStudentDetail.SebtAppId));
        if (row.SebtChldCwin is null)
            empty.Add(nameof(GetAccountStudentDetail.SebtChldCwin));
        if (string.IsNullOrWhiteSpace(row.CbmsCsId))
            empty.Add(nameof(GetAccountStudentDetail.CbmsCsId));

        return
            $"These correlation fields were empty: {string.Join(", ", empty)}. " +
            "Ask CBMS to populate at least one (typically sebtChldId or sebtAppId per OpenAPI) for accounts that should use update-std-dtls.";
    }

    /// <summary>Scalar typed properties with values, excluding the two ids (names only, for support).</summary>
    private static List<string> DescribeNonIdTypedFieldsPresent(GetAccountStudentDetail row)
    {
        var names = new List<string>();
        foreach (var prop in typeof(GetAccountStudentDetail).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.Name is nameof(GetAccountStudentDetail.AdditionalData))
                continue;
            if (prop.Name is nameof(GetAccountStudentDetail.SebtChldId) or nameof(GetAccountStudentDetail.SebtAppId))
                continue;

            var val = prop.GetValue(row);
            switch (val)
            {
                case string s when !string.IsNullOrWhiteSpace(s):
                    names.Add(prop.Name);
                    break;
                case int:
                case long:
                    names.Add(prop.Name);
                    break;
                case double d when d != 0:
                    names.Add(prop.Name);
                    break;
            }
        }

        names.Sort(StringComparer.Ordinal);
        return names;
    }

    private static string? GetFromAdditionalCaseInsensitive(GetAccountStudentDetail row, IEnumerable<string> candidates)
    {
        foreach (var candidate in candidates)
        {
            foreach (var kv in row.AdditionalData)
            {
                if (string.Equals(kv.Key, candidate, StringComparison.OrdinalIgnoreCase))
                {
                    var s = CoerceToString(kv.Value);
                    if (!string.IsNullOrWhiteSpace(s))
                        return s;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Last resort: keys like <c>SEBTCHLDID</c>, snake_case, or vendor-specific names.
    /// </summary>
    private static string? FindByHeuristic(GetAccountStudentDetail row, bool forChildId)
    {
        foreach (var kv in row.AdditionalData)
        {
            var k = kv.Key.ToLowerInvariant();
            if (forChildId)
            {
                var looksLikeChildId =
                    (k.Contains("sebt") && k.Contains("chld") && k.Contains("id"))
                    || (k.Contains("sebt") && k.Contains("child") && k.Contains("id"))
                    || k is "chldid" or "childid";
                if (!looksLikeChildId)
                    continue;
            }
            else
            {
                // Tight: "approval…" contains "app" but must not match; require sebt+app+id or bare appid.
                var looksLikeAppId =
                    (k.Contains("sebt") && k.Contains("app") && k.Contains("id"))
                    || k is "appid" or "applicationid";
                if (!looksLikeAppId)
                    continue;
            }

            var s = CoerceToString(kv.Value);
            if (!string.IsNullOrWhiteSpace(s))
                return s;
        }

        return null;
    }

    private static string? CoerceToString(object? v)
    {
        switch (v)
        {
            case null:
                return null;
            case string s:
                return string.IsNullOrWhiteSpace(s) ? null : s;
            case JsonElement je when je.ValueKind == JsonValueKind.String:
                return je.GetString();
            case JsonElement je when je.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False:
                return je.ToString();
        }

        var t = v.GetType();
        if (t.Name.StartsWith("Untyped", StringComparison.Ordinal))
        {
            var valueProp = t.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
            if (valueProp?.GetValue(v) is { } inner)
                return CoerceToString(inner);
        }

        var s2 = v.ToString();
        return string.IsNullOrWhiteSpace(s2) || string.Equals(s2, t.FullName, StringComparison.Ordinal)
            ? null
            : s2;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v;
        }

        return null;
    }

    private static string? FormatIntId(int? value) =>
        value?.ToString(CultureInfo.InvariantCulture);
}
