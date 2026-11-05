namespace SEBT.Portal.StatePlugins.CO.Tests;

/// <summary>
/// Forces sequential execution for any test class that manipulates the <see cref="PluginCache"/>
/// static singleton. Tests in this collection run one at a time, preventing the race condition
/// where one class's IDisposable.Dispose (which calls ResetForTesting) races with another
/// class's OverrideForTesting call. Parallelization is disabled so the collection also runs
/// isolated from other collections (e.g. CbmsSandbox) whose Dispose resets the cache.
/// </summary>
[CollectionDefinition("PluginCache", DisableParallelization = true)]
public class PluginCacheCollection;
