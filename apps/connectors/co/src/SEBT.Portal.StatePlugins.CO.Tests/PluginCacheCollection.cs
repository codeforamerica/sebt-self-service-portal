namespace SEBT.Portal.StatePlugins.CO.Tests;

/// <summary>
/// Forces sequential execution for any test class that manipulates the <see cref="PluginCache"/>
/// static singleton. Tests in this collection run one at a time, preventing the race condition
/// where one class's IDisposable.Dispose (which calls ResetForTesting) races with another
/// class's OverrideForTesting call.
/// </summary>
[CollectionDefinition("PluginCache")]
public class PluginCacheCollection;
