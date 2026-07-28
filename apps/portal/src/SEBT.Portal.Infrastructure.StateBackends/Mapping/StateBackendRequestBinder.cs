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
                WriteAtPath(body, targetPath, JsonValue.Create(value));
            }
        }

        if (binding.Map is { } map)
        {
            foreach ((string inputName, string targetPath) in map)
            {
                JsonNode value = ResolveInput(inputName, request);
                WriteAtPath(body, targetPath, value);
            }
        }

        return body;
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

    // Writes a value at a dotted target path, building intermediate nested objects as needed.
    private static void WriteAtPath(JsonObject root, string dottedPath, JsonNode? value)
    {
        string[] segments = dottedPath.Split('.');
        JsonObject current = root;

        for (int i = 0; i < segments.Length - 1; i++)
        {
            string segment = segments[i];
            if (current[segment] is not JsonObject child)
            {
                child = new JsonObject();
                current[segment] = child;
            }

            current = child;
        }

        current[segments[^1]] = value;
    }
}
