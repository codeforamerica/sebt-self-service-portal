using System.Text.Json.Nodes;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;

namespace SEBT.Portal.Infrastructure.StateBackends.Mapping;

/// <summary>
/// Builds an outgoing lookup request body from a domain-centered <see cref="RequestBinding"/>
/// (DC-568 spike). Two sources feed the body:
///   * <c>constants</c> — fixed literals written at dotted target paths.
///   * <c>map</c> — OUR named input (a household-identity signal or a caller-context value)
///     written at a dotted target path.
///
/// Input resolution is a CLOSED set — known signal types plus a fixed set of context names. No
/// arbitrary lookups, expressions, or transforms. A map input that resolves to nothing fails loud.
/// The binder passes <see cref="HouseholdLookupRequest.IsProofed"/> straight through; it never
/// computes an authorization decision. This is the input→JSON layer; the config records stay
/// transport-free in Core.
/// </summary>
internal static class StateBackendRequestBinder
{
    public static JsonObject BuildBody(RequestBinding binding, HouseholdLookupRequest request)
    {
        var body = new JsonObject();

        if (binding.Constants is { } constants)
        {
            foreach ((string targetPath, object value) in constants)
            {
                JsonPathWriter.Write(body, targetPath, JsonValue.Create(value));
            }
        }

        if (binding.Map is { } map)
        {
            foreach ((string inputName, string targetPath) in map)
            {
                JsonNode value = ResolveInput(inputName, request);
                JsonPathWriter.Write(body, targetPath, value);
            }
        }

        return body;
    }

    /// <summary>
    /// Write-path binding: builds the outgoing body from constants plus a CLOSED set of named
    /// inputs — the routing fields decoded from the incoming opaque caseId. Same domain-centered
    /// constants + map vocabulary as the read path; the map's LHS names refer to decoded routing
    /// fields. A map input with no matching routing field fails loud (never silently drops).
    /// </summary>
    public static JsonObject BuildBody(RequestBinding binding, IReadOnlyDictionary<string, string> inputs)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(inputs);

        var body = new JsonObject();

        if (binding.Constants is { } constants)
        {
            foreach ((string targetPath, object value) in constants)
            {
                JsonPathWriter.Write(body, targetPath, JsonValue.Create(value));
            }
        }

        if (binding.Map is { } map)
        {
            foreach ((string inputName, string targetPath) in map)
            {
                if (!inputs.TryGetValue(inputName, out string? value))
                {
                    throw new InvalidOperationException(
                        $"Request map input '{inputName}' resolved to no value.");
                }

                JsonPathWriter.Write(body, targetPath, JsonValue.Create(value));
            }
        }

        return body;
    }

    /// <summary>
    /// Batch write-path binding (address update). Builds the body from constants + scalar map
    /// (address scalars, resolved from <paramref name="scalarInputs"/>) PLUS the two batch shapes
    /// that fan out over the decoded caseIds:
    ///   * <c>shared</c> — a household-level routing field resolved ONCE across every case; FAILS
    ///     LOUD if the cases disagree on the value.
    ///   * <c>collect</c> — a per-case routing field gathered into an ARRAY, one element per case.
    ///
    /// HARD CAP: shared + collect are the ONLY batch shapes — no per-case conditionals, filtering,
    /// or transforms. A shared/collect field missing from any decoded caseId fails loud.
    /// </summary>
    public static JsonObject BuildAddressBody(
        RequestBinding binding,
        IReadOnlyList<IReadOnlyDictionary<string, string>> decodedCaseIds,
        IReadOnlyDictionary<string, string> scalarInputs)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(decodedCaseIds);
        ArgumentNullException.ThrowIfNull(scalarInputs);

        if (decodedCaseIds.Count == 0)
        {
            throw new InvalidOperationException("Address update requires at least one caseId.");
        }

        // Constants + scalar address fields reuse the existing scalar binding.
        JsonObject body = BuildBody(binding, scalarInputs);

        if (binding.Shared is { } shared)
        {
            foreach ((string fieldName, string targetPath) in shared)
            {
                JsonPathWriter.Write(body, targetPath, JsonValue.Create(ResolveShared(fieldName, decodedCaseIds)));
            }
        }

        if (binding.Collect is { } collect)
        {
            foreach ((string fieldName, string targetPath) in collect)
            {
                JsonPathWriter.Write(body, targetPath, CollectArray(fieldName, decodedCaseIds));
            }
        }

        return body;
    }

    // Resolve one household-level field across all decoded caseIds; fail loud on disagreement.
    private static string ResolveShared(
        string fieldName, IReadOnlyList<IReadOnlyDictionary<string, string>> decodedCaseIds)
    {
        string? resolved = null;

        foreach (IReadOnlyDictionary<string, string> caseFields in decodedCaseIds)
        {
            if (!caseFields.TryGetValue(fieldName, out string? value))
            {
                throw new InvalidOperationException(
                    $"Shared address field '{fieldName}' is missing from a decoded caseId.");
            }

            if (resolved is null)
            {
                resolved = value;
            }
            else if (!string.Equals(resolved, value, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Shared address field '{fieldName}' disagrees across caseIds — cannot resolve a single value.");
            }
        }

        return resolved!;
    }

    // Gather a per-case field into an ordered array, one element per decoded caseId.
    private static JsonArray CollectArray(
        string fieldName, IReadOnlyList<IReadOnlyDictionary<string, string>> decodedCaseIds)
    {
        var array = new JsonArray();

        foreach (IReadOnlyDictionary<string, string> caseFields in decodedCaseIds)
        {
            if (!caseFields.TryGetValue(fieldName, out string? value))
            {
                throw new InvalidOperationException(
                    $"Collected address field '{fieldName}' is missing from a decoded caseId.");
            }

            array.Add(JsonValue.Create(value));
        }

        return array;
    }

    // Closed resolution set: household-identity signals addressable by IdentitySignal.Type, plus
    // a small fixed set of caller-context names. Fail loud when an input resolves to nothing.
    private static JsonNode ResolveInput(string inputName, HouseholdLookupRequest request)
    {
        // Caller context — facts about the authenticated user, not household search keys.
        if (string.Equals(inputName, "isProofed", StringComparison.Ordinal))
        {
            // Straight pass-through of the caller's proofing status. No threshold logic here.
            return JsonValue.Create(request.IsProofed);
        }

        if (string.Equals(inputName, "portalUuid", StringComparison.Ordinal))
        {
            if (request.PortalUuid is { } portalUuid)
            {
                return JsonValue.Create(portalUuid);
            }

            throw new InvalidOperationException(
                "Request map input 'portalUuid' resolved to no value.");
        }

        // Household-identity signal, addressed by its IdentitySignal.Type.
        foreach (IdentitySignal signal in request.Signals)
        {
            if (string.Equals(signal.Type, inputName, StringComparison.Ordinal))
            {
                return JsonValue.Create(signal.Value);
            }
        }

        throw new InvalidOperationException(
            $"Request map input '{inputName}' resolved to no value.");
    }
}
