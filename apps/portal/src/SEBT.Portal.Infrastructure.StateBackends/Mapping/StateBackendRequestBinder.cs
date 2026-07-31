using System.Text.Json.Nodes;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;

namespace SEBT.Portal.Infrastructure.StateBackends.Mapping;

/// <summary>
/// Builds an outgoing request body from a <see cref="RequestBinding"/>: a map input resolving to
/// nothing fails loud, a mapOptional input is omitted. <c>isProofed</c> passes straight through —
/// never an authorization decision here.
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

        if (binding.MapOptional is { } mapOptional)
        {
            foreach ((string inputName, string targetPath) in mapOptional)
            {
                if (TryResolveInput(inputName, request) is { } value)
                {
                    JsonPathWriter.Write(body, targetPath, value);
                }
            }
        }

        return body;
    }

    /// <summary>
    /// Write-path binding: constants plus the routing fields decoded from the opaque caseId; an
    /// unmatched map input fails loud.
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
    /// Batch write-path binding (address update): the scalar binding plus the <c>shared</c> and
    /// <c>collect</c> shapes over the decoded caseIds. A <c>shared</c> field that disagrees across
    /// cases, or a shared/collect field missing from any caseId, fails loud.
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

    // One household-level field across all decoded caseIds; fails loud on disagreement.
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

    // A per-case field gathered into an ordered array, one element per decoded caseId.
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

    // Required entries fail loud on an unresolved input; optional entries are omitted instead.
    private static JsonNode ResolveInput(string inputName, HouseholdLookupRequest request) =>
        TryResolveInput(inputName, request)
            ?? throw new InvalidOperationException(
                $"Request map input '{inputName}' resolved to no value.");

    // Closed set: household-identity signals by IdentitySignal.Type plus fixed caller-context names.
    private static JsonNode? TryResolveInput(string inputName, HouseholdLookupRequest request)
    {
        if (string.Equals(inputName, "isProofed", StringComparison.Ordinal))
        {
            // Straight pass-through of the caller's proofing status — no authorization decision here.
            return JsonValue.Create(request.IsProofed);
        }

        if (string.Equals(inputName, "portalUuid", StringComparison.Ordinal))
        {
            return request.PortalUuid is { } portalUuid ? JsonValue.Create(portalUuid) : null;
        }

        foreach (IdentitySignal signal in request.Signals)
        {
            if (string.Equals(signal.Type, inputName, StringComparison.Ordinal))
            {
                return JsonValue.Create(signal.Value);
            }
        }

        return null;
    }
}
