using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SEBT.Portal.Api.Composition;
using SEBT.Portal.StatesPlugins.Interfaces;

namespace SEBT.Portal.Tests.Composition;

public class PluginAssemblyScannerTests
{
    [Fact]
    public void AddPlugins_registers_default_services_when_no_plugin_assemblies_found()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PluginAssemblyPaths:0"] = "nonexistent-plugin-path"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddLogging();

        services.AddPlugins(configuration);

        var provider = services.BuildServiceProvider();

        // Should fall back to defaults when no plugins are discovered
        var caseService = provider.GetService<ISummerEbtCaseService>();
        Assert.NotNull(caseService);
        Assert.Contains("Default", caseService.GetType().Name);
    }
}
