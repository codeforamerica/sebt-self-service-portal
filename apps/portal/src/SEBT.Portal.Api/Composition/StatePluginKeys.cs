namespace SEBT.Portal.Api.Composition;

/// <summary>
/// Keyed-DI name for the MEF connector implementation of each plugin interface, so the
/// feature-gated wrappers can resolve the plugin without looping on themselves.
/// </summary>
internal static class StatePluginKeys
{
    public const string Connector = "state-plugin";
}
