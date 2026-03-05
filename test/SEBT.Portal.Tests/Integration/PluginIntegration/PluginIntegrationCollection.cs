namespace SEBT.Portal.Tests.Integration.PluginIntegration;

/// <summary>
/// Collection definition that serializes all plugin integration tests.
/// Tests in this collection run sequentially because they set process-global
/// environment variables that would conflict if run in parallel.
/// </summary>
[CollectionDefinition("PluginIntegration")]
public class PluginIntegrationCollection
{
    // This class has no code and is never instantiated. Its purpose is to apply
    // [CollectionDefinition] so xUnit serializes all classes tagged with
    // [Collection("PluginIntegration")].
}
