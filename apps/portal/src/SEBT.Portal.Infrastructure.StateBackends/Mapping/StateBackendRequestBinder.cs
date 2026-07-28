using System.Text.Json.Nodes;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;

namespace SEBT.Portal.Infrastructure.StateBackends.Mapping;

/// <summary>
/// Builds an outgoing request body from a capped request-binding map (DC-568 spike), pulling
/// values from the lookup request's <see cref="IdentitySignal"/> list. The binding vocabulary
/// is closed to three kinds — see <see cref="RequestBinding"/>. This is the signal→JSON layer;
/// the config records themselves stay transport-free in Core.
/// </summary>
internal static class StateBackendRequestBinder
{
    public static JsonObject BuildBody(
        IReadOnlyDictionary<string, RequestBinding> bindings,
        IReadOnlyList<IdentitySignal> signals)
    {
        var body = new JsonObject();

        foreach ((string field, RequestBinding binding) in bindings)
        {
            body[field] = ResolveBinding(binding, signals);
        }

        return body;
    }

    private static JsonNode? ResolveBinding(
        RequestBinding binding, IReadOnlyList<IdentitySignal> signals)
    {
        ValidateExactlyOne(binding);

        if (binding.From is not null)
        {
            string? value = FindSignalValue(binding.From, signals);
            return value is null ? null : JsonValue.Create(value);
        }

        if (binding.Compose is not null)
        {
            var composed = new JsonObject();
            foreach ((string key, RequestBinding sub) in binding.Compose)
            {
                composed[key] = ResolveBinding(sub, signals);
            }

            return composed;
        }

        // Const — the remaining branch guaranteed by ValidateExactlyOne.
        return JsonValue.Create(binding.Const);
    }

    private static string? FindSignalValue(string signalType, IReadOnlyList<IdentitySignal> signals)
    {
        foreach (IdentitySignal signal in signals)
        {
            if (string.Equals(signal.Type, signalType, StringComparison.Ordinal))
            {
                return signal.Value;
            }
        }

        return null;
    }

    // Fail-loud: a binding maps to exactly one of the three closed kinds.
    private static void ValidateExactlyOne(RequestBinding binding)
    {
        int set = (binding.From is not null ? 1 : 0)
            + (binding.Const is not null ? 1 : 0)
            + (binding.Compose is not null ? 1 : 0);

        if (set != 1)
        {
            throw new InvalidOperationException(
                "A request binding must set exactly one of 'from', 'const', or 'compose'.");
        }
    }
}
