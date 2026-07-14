using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Integration;

/// <summary>
/// Proves the app refuses to start when an outage window is malformed. Before ValidateOnStart, a
/// mistyped date or target was logged and skipped at request time, so a broken schedule could be
/// deployed and would quietly disable the outage page it was meant to schedule.
/// <para>
/// That the well-formed and empty cases still boot is covered by every other integration test in
/// this project: the base appsettings.json has no OutageSchedule section, so they all start the
/// host against the validated defaults.
/// </para>
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class OutageScheduleStartupValidationTests : IDisposable
{
    // Program.cs adds appsettings.{state}.json after the environment-variable provider, so a state
    // file wins over anything set here. A high window index sidesteps that: a state file's windows
    // live at 0..n and cannot overwrite this one. Assertions match on the offending value rather
    // than its position in the bound list, which shifts when a state file contributes windows too.
    //
    // TimeZoneId has no such escape hatch — a state file that sets it would silently replace the
    // value under test — so the timezone rule is covered by OutageScheduleSettingsValidatorTests.
    private const string BadWindow = "9";

    private static readonly string[] EnvVarKeys =
    [
        "PluginAssemblyPaths__0",
        "PluginAssemblyPaths__1",
        "JwtSettings__SecretKey",
        "STATE",
        "Oidc__DiscoveryEndpoint",
        "Oidc__ClientId",
        "Oidc__CallbackRedirectUri",
        "Oidc__CompleteLoginSigningKey",
        "ConnectionStrings__Redis",
        "MinimumIal__ApplicationCases",
        "MinimumIal__CoLoadedStreamlineCases",
        "MinimumIal__NonCoLoadedStreamlineCases",
        $"OutageSchedule__Windows__{BadWindow}__Start",
        $"OutageSchedule__Windows__{BadWindow}__End",
        $"OutageSchedule__Windows__{BadWindow}__Target"
    ];

    public OutageScheduleStartupValidationTests()
    {
        // Everything the host needs to reach options validation, including a valid JwtSettings
        // secret so OutageSchedule is the only section that can fail. A second failing section
        // would surface as an AggregateException instead of an OptionsValidationException.
        Environment.SetEnvironmentVariable("PluginAssemblyPaths__0", "plugins-none");
        Environment.SetEnvironmentVariable("PluginAssemblyPaths__1", "plugins-none");
        Environment.SetEnvironmentVariable("STATE", "co");
        Environment.SetEnvironmentVariable("JwtSettings__SecretKey",
            "integration-test-jwt-secret-key-at-least-32-chars!");
        Environment.SetEnvironmentVariable("Oidc__DiscoveryEndpoint", "https://auth.example.com/.well-known/openid-configuration");
        Environment.SetEnvironmentVariable("Oidc__ClientId", "test-client");
        Environment.SetEnvironmentVariable("Oidc__CallbackRedirectUri", "http://localhost:3000/callback");
        Environment.SetEnvironmentVariable("Oidc__CompleteLoginSigningKey",
            "integration-test-secret-key-at-least-32-chars!");
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", "");
        Environment.SetEnvironmentVariable("MinimumIal__ApplicationCases", "IAL1");
        Environment.SetEnvironmentVariable("MinimumIal__CoLoadedStreamlineCases", "IAL1");
        Environment.SetEnvironmentVariable("MinimumIal__NonCoLoadedStreamlineCases", "IAL1plus");
    }

    [Fact]
    public void Startup_WithUnparseableWindowDates_ThrowsOptionsValidationException()
    {
        SetWindow(start: "2026-13-45T99:00", end: "not-a-date", target: "Both");

        using var factory = CreateFactory();
        var ex = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("OutageSchedule", ex.Message);
        Assert.Contains("2026-13-45T99:00", ex.Message);
        Assert.Contains("not-a-date", ex.Message);
    }

    [Fact]
    public void Startup_WithUnrecognizedWindowTarget_ThrowsOptionsValidationException()
    {
        SetWindow(start: "2026-06-15T22:00:00", end: "2026-06-16T06:00:00", target: "Prtal");

        using var factory = CreateFactory();
        var ex = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("Target", ex.Message);
        Assert.Contains("Prtal", ex.Message);
    }

    [Fact]
    public void Startup_WithEndBeforeStart_ThrowsOptionsValidationException()
    {
        SetWindow(start: "2026-06-16T06:00:00", end: "2026-06-15T22:00:00", target: "Both");

        using var factory = CreateFactory();
        var ex = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("must be after", ex.Message);
    }

    private static void SetWindow(string start, string end, string target)
    {
        Environment.SetEnvironmentVariable($"OutageSchedule__Windows__{BadWindow}__Start", start);
        Environment.SetEnvironmentVariable($"OutageSchedule__Windows__{BadWindow}__End", end);
        Environment.SetEnvironmentVariable($"OutageSchedule__Windows__{BadWindow}__Target", target);
    }

    // ValidateOnStart triggers during host startup — CreateClient() surfaces the failure.
    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    ReplaceWithMock<IDatabaseMigrator>(services);
                    ReplaceWithMock<IDatabaseSeeder>(services);
                });
            });
    }

    private static void ReplaceWithMock<TService>(IServiceCollection services) where TService : class
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(TService));
        if (descriptor != null)
        {
            services.Remove(descriptor);
        }

        services.AddScoped(_ => Substitute.For<TService>());
    }

    public void Dispose()
    {
        foreach (var key in EnvVarKeys)
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }
}
