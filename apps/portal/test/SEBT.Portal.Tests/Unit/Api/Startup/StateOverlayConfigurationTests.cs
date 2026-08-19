using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using SEBT.Portal.Api.Startup;

namespace SEBT.Portal.Tests.Unit.Api.Startup;

/// <summary>
/// Verifies where the state overlay (appsettings.{state}.json) lands in the
/// configuration provider chain built by WebApplication.CreateBuilder.
/// Expected precedence (later wins): base appsettings files, state overlay,
/// environment variables, command-line args, then AppConfig Agent (appended
/// after the overlay in Program.cs).
///
/// These tests use the real WebApplication.CreateBuilder chain rather than a
/// hand-assembled ConfigurationBuilder, because the bug being guarded against
/// is precisely the overlay's position relative to the providers CreateBuilder
/// registers on its own.
/// </summary>
public class StateOverlayConfigurationTests : IDisposable
{
    private const string StateCode = "zz";

    private readonly string _contentRoot =
        Directory.CreateTempSubdirectory("state-overlay-config-tests-").FullName;
    private readonly List<string> _environmentVariablesToClear = [];

    [Fact]
    public void AddStateOverlay_StateFileOverridesBaseAppSettings()
    {
        WriteJsonFile("appsettings.json", "StateOverlayTest:BaseKey", "from-base");
        WriteStateOverlayFile("StateOverlayTest:BaseKey", "from-state-overlay");

        var builder = CreateBuilder();
        builder.Configuration.AddStateOverlay(StateCode);

        Assert.Equal("from-state-overlay", builder.Configuration["StateOverlayTest:BaseKey"]);
    }

    [Fact]
    public void AddStateOverlay_StateFileOverridesEnvironmentSpecificAppSettings()
    {
        WriteJsonFile("appsettings.Staging.json", "StateOverlayTest:EnvJsonKey", "from-environment-json");
        WriteStateOverlayFile("StateOverlayTest:EnvJsonKey", "from-state-overlay");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = _contentRoot,
            EnvironmentName = "Staging",
        });
        builder.Configuration.AddStateOverlay(StateCode);

        Assert.Equal("from-state-overlay", builder.Configuration["StateOverlayTest:EnvJsonKey"]);
    }

    [Fact]
    public void AddStateOverlay_EnvironmentVariableOverridesStateFile()
    {
        WriteStateOverlayFile("StateOverlayTest:EnvKey", "from-state-overlay");
        SetEnvironmentVariable("StateOverlayTest__EnvKey", "from-env");

        var builder = CreateBuilder();
        builder.Configuration.AddStateOverlay(StateCode);

        Assert.Equal("from-env", builder.Configuration["StateOverlayTest:EnvKey"]);
    }

    [Fact]
    public void AddStateOverlay_CommandLineArgsOverrideEnvironmentVariablesAndStateFile()
    {
        WriteStateOverlayFile("StateOverlayTest:ArgsKey", "from-state-overlay");
        SetEnvironmentVariable("StateOverlayTest__ArgsKey", "from-env");

        var builder = CreateBuilder("--StateOverlayTest:ArgsKey=from-args");
        builder.Configuration.AddStateOverlay(StateCode);

        Assert.Equal("from-args", builder.Configuration["StateOverlayTest:ArgsKey"]);
    }

    [Fact]
    public void AddStateOverlay_SourcesAddedAfterOverlayOverrideEnvironmentVariables()
    {
        // Simulates the AppConfig Agent providers, which Program.cs appends
        // after the state overlay and must stay the highest priority.
        WriteStateOverlayFile("StateOverlayTest:AppConfigKey", "from-state-overlay");
        SetEnvironmentVariable("StateOverlayTest__AppConfigKey", "from-env");

        var builder = CreateBuilder();
        builder.Configuration.AddStateOverlay(StateCode);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["StateOverlayTest:AppConfigKey"] = "from-app-config",
        });

        Assert.Equal("from-app-config", builder.Configuration["StateOverlayTest:AppConfigKey"]);
    }

    [Fact]
    public void AddStateOverlay_MissingStateFileLeavesBaseConfiguration()
    {
        WriteJsonFile("appsettings.json", "StateOverlayTest:MissingKey", "from-base");

        var builder = CreateBuilder();
        builder.Configuration.AddStateOverlay("nofile");

        Assert.Equal("from-base", builder.Configuration["StateOverlayTest:MissingKey"]);
    }

    [Fact]
    public void AddStateOverlay_ThrowsWhenNoEnvironmentVariableSourceExists()
    {
        // A chain without the app-level environment variable source has no safe
        // insertion point: appending would silently restore the old precedence
        // where state JSON overrides env vars. Fail fast instead.
        var configuration = new ConfigurationManager();

        var ex = Assert.Throws<InvalidOperationException>(
            () => configuration.AddStateOverlay(StateCode));

        Assert.Contains("environment variable", ex.Message);
    }

    [Fact]
    public void AddStateOverlay_NormalizesStateCodeToLowercase()
    {
        WriteStateOverlayFile("StateOverlayTest:CaseKey", "from-state-overlay");

        var builder = CreateBuilder();
        builder.Configuration.AddStateOverlay(StateCode.ToUpperInvariant());

        Assert.Equal("from-state-overlay", builder.Configuration["StateOverlayTest:CaseKey"]);
    }

    private WebApplicationBuilder CreateBuilder(params string[] args)
    {
        return WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = _contentRoot,
            Args = args,
        });
    }

    private void WriteStateOverlayFile(string key, string value)
    {
        WriteJsonFile($"appsettings.{StateCode}.json", key, value);
    }

    private void WriteJsonFile(string fileName, string key, string value)
    {
        File.WriteAllText(
            Path.Combine(_contentRoot, fileName),
            $$"""{ "{{key}}": "{{value}}" }""");
    }

    private void SetEnvironmentVariable(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value);
        _environmentVariablesToClear.Add(name);
    }

    public void Dispose()
    {
        foreach (var name in _environmentVariablesToClear)
        {
            Environment.SetEnvironmentVariable(name, null);
        }
        Directory.Delete(_contentRoot, recursive: true);
    }
}
