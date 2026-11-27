using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;

namespace SEBT.Portal.Tests.Integration.StartupValidation;

/// <summary>
/// Guards the isolation <see cref="StartupValidationTestBase"/> provides: a developer's local
/// appsettings overlays and user secrets must not reach the host, or these tests pass in CI and
/// fail on any machine set up from the example files.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class StartupValidationHostIsolationTests : StartupValidationTestBase
{
    [Fact]
    public void Host_ReadsNoJsonConfigurationBeyondTrackedAppSettings()
    {
        using var factory = CreateFactory();
        factory.CreateClient();

        var configuration = (IConfigurationRoot)factory.Services.GetRequiredService<IConfiguration>();
        var jsonFiles = configuration.Providers
            .OfType<JsonConfigurationProvider>()
            .Select(provider => provider.Source.Path);

        Assert.Equal(["appsettings.json"], jsonFiles);
    }
}
