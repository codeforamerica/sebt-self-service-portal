namespace SEBT.Portal.Core.StateBackends;

/// <summary>
/// Resolves a config-key reference (e.g. <c>StateBackend:Auth:ApiKey</c>) to a secret value.
/// The reference is a key, never the value — implementations pull the actual secret from
/// environment variables, <c>/run/secrets</c>, or a vault. Keeps secrets out of config records.
/// </summary>
public interface IStateBackendSecretResolver
{
    /// <summary>
    /// Resolves the given config-key reference to its secret value.
    /// </summary>
    /// <param name="reference">The config-key reference to resolve.</param>
    /// <returns>The resolved secret value.</returns>
    string Resolve(string reference);
}
