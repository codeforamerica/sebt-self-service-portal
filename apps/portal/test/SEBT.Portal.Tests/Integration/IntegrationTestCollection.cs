namespace SEBT.Portal.Tests.Integration;

/// <summary>
/// Serializes integration test classes that share process-global environment variables
/// (PluginAssemblyPaths, JwtSettings) via WebApplicationFactory. Without this, xUnit
/// runs test classes in parallel and the env vars set by one factory corrupt another.
/// </summary>
[CollectionDefinition("Integration")]
public class IntegrationTestCollection;
