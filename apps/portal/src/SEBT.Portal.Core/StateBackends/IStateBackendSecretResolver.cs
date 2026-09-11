namespace SEBT.Portal.Core.StateBackends;

/// <summary>
/// Resolves a config-key reference (e.g. <c>StateBackend:Auth:ApiKey</c>) to a secret value. The
/// reference is a key, never the value — implementations pull the secret from environment,
/// <c>/run/secrets</c>, or a vault, keeping secrets out of config records.
/// </summary>
public interface IStateBackendSecretResolver
{
    string Resolve(string reference);
}
