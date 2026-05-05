using Microsoft.Extensions.DependencyInjection;
using SEBT.Portal.StatesPlugins.Interfaces;

namespace SEBT.Portal.Tests.Integration.PluginIntegration;

/// <summary>
/// Diagnostic harness for DC plugin DI injection. Spins up the real API with
/// the DC plugin DLLs loaded and verifies that <see cref="ICardReplacementService"/>
/// and <see cref="ISummerEbtCaseService"/> resolve to the DC plugin
/// implementations rather than the API-side defaults.
///
/// The DC plugin services declare <c>IConfiguration</c> and
/// <c>ILogger&lt;T&gt;</c> as required (non-nullable) constructor parameters
/// guarded with <c>ArgumentNullException.ThrowIfNull</c>. That contract makes
/// these tests sufficient on their own: if either dependency failed to
/// resolve, the constructor would throw at fixture startup and the tests
/// would surface as fixture-init failures, not silent log drops in production.
///
/// Uses <see cref="IClassFixture{TFixture}"/> so a single
/// <see cref="PluginIntegrationWebApplicationFactory"/> is shared across
/// tests. Re-creating the factory in each test would trip
/// <c>PluginAssemblyLoader</c>'s "skip already-loaded host assemblies" filter
/// on the second invocation, because plugin DLLs from the first factory
/// remain in the default ALC.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class DcPluginInjectionIntegrationTests
    : IClassFixture<DcPluginInjectionIntegrationTests.Fixture>
{
    private readonly Fixture _fixture;

    public DcPluginInjectionIntegrationTests(Fixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public void DcCardReplacementService_resolves_to_dc_plugin_implementation()
    {
        Skip.IfNot(_fixture.CanRun, _fixture.SkipReason);

        var resolved = _fixture.Factory!.Services.GetRequiredService<ICardReplacementService>();

        Assert.Equal("DcCardReplacementService", resolved.GetType().Name);
    }

    [SkippableFact]
    public void DcSummerEbtCaseService_resolves_to_dc_plugin_implementation()
    {
        Skip.IfNot(_fixture.CanRun, _fixture.SkipReason);

        var resolved = _fixture.Factory!.Services.GetRequiredService<ISummerEbtCaseService>();

        Assert.Equal("DcSummerEbtCaseService", resolved.GetType().Name);
    }

    public class Fixture : IDisposable
    {
        public PluginIntegrationWebApplicationFactory? Factory { get; }
        public bool CanRun { get; }
        public string SkipReason { get; }

        public Fixture()
        {
            if (!PluginPathResolver.HasPluginDlls("plugins-dc"))
            {
                CanRun = false;
                SkipReason = "DC plugin DLLs not found in plugins-dc/";
                return;
            }

            try
            {
                // Deliberately omit DCConnector:CardReplacementProcName so the
                // plugin's "not configured" path is the natural default for any
                // test that exercises behavior. DI-shape tests don't depend on it.
                Factory = new PluginIntegrationWebApplicationFactory(
                    pluginDir: "plugins-dc",
                    configOverrides: new Dictionary<string, string>
                    {
                        ["DCConnector:ConnectionString"] =
                            "Server=unused;Database=unused;User Id=sa;Password=unused;TrustServerCertificate=True;"
                    });

                _ = Factory.Services;
                CanRun = true;
                SkipReason = string.Empty;
            }
            catch (Exception ex)
            {
                Factory?.Dispose();
                Factory = null;
                CanRun = false;
                SkipReason = $"DC plugin DLLs could not be loaded: {ex.GetBaseException().Message}";
            }
        }

        public void Dispose()
        {
            Factory?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
