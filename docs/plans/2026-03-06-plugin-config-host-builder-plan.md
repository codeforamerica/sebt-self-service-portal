# Deferred Plugin Loading — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Eliminate environment variable usage in test factories by deferring plugin loading to DI resolution time, where `IConfiguration` includes WAF's `ConfigureAppConfiguration` overrides.

**Architecture:** Replace eager MEF assembly loading during service registration with a `PluginLoader` singleton that loads lazily on first resolution. Register factory delegates for each plugin interface. Tests inject config via standard `ConfigureAppConfiguration` — zero env vars.

**Tech Stack:** ASP.NET Core DI factory delegates, System.Composition (MEF), `WebApplicationFactory`, `IConfigurationBuilder.AddInMemoryCollection`, xUnit

**Design doc:** `docs/plans/2026-03-06-plugin-config-host-builder.md`

---

### Task 1: Revert IHostBuilder extension, spike deferred loading

Revert the Task 1 commit (`f703121`) that moved `AddPlugins` to an `IHostBuilder` extension. That approach was based on incorrect timing assumptions — `ConfigureHostBuilder.ConfigureServices` runs callbacks immediately, not during `Build()`. Then create a minimal spike to verify that a factory delegate can read `IConfiguration` values set via WAF's `ConfigureAppConfiguration`.

**Files:**
- Revert: `src/SEBT.Portal.Api/Composition/ServiceCollectionPluginExtensions.cs`
- Revert: `src/SEBT.Portal.Api/Program.cs:71`
- Create (temporary): `test/SEBT.Portal.Tests/Integration/ConfigurationTimingTests.cs`

**Step 1: Revert commit f703121**

Run: `git revert f703121 --no-edit`

This restores `AddPlugins` as an `IServiceCollection` extension with `IConfiguration` parameter, and restores `builder.Services.AddPlugins(builder.Configuration)` in Program.cs.

**Step 2: Verify revert is clean**

Run: `dotnet test test/SEBT.Portal.Tests/SEBT.Portal.Tests.csproj`

Expected: all tests pass (438 passed, 3 skipped, 0 failed)

**Step 3: Write the spike test**

Create `test/SEBT.Portal.Tests/Integration/ConfigurationTimingTests.cs`:

```csharp
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SEBT.Portal.Tests.Integration;

/// <summary>
/// Spike test to verify that IConfiguration from DI at resolution time
/// includes values added via WAF's ConfigureAppConfiguration.
/// This validates the core assumption of the deferred plugin loading design.
/// </summary>
public class ConfigurationTimingTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;

    public ConfigurationTimingTests()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");

                builder.ConfigureAppConfiguration((_, config) =>
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["PluginAssemblyPaths:0"] = "plugins-test",
                        ["JwtSettings:SecretKey"] =
                            "integration-test-key-must-be-at-least-32-bytes-long",
                        ["TestSpike:Value"] = "from-configure-app-configuration",
                    }));

                builder.ConfigureServices(services =>
                {
                    // Register a singleton factory that reads IConfiguration
                    // at resolution time — this is the pattern PluginLoader will use
                    services.AddSingleton<SpikeService>(sp =>
                    {
                        var config = sp.GetRequiredService<IConfiguration>();
                        var value = config["TestSpike:Value"];
                        return new SpikeService(value ?? "NOT FOUND");
                    });
                });
            });
    }

    [Fact]
    public void FactoryDelegate_ReadsConfigurationFromConfigureAppConfiguration()
    {
        // Resolve the service — the factory delegate should read the config
        // value that was set via ConfigureAppConfiguration
        var service = _factory.Services.GetRequiredService<SpikeService>();

        Assert.Equal("from-configure-app-configuration", service.Value);
    }

    public void Dispose() => _factory.Dispose();

    private record SpikeService(string Value);
}
```

**Step 4: Run the spike test**

Run: `dotnet test test/SEBT.Portal.Tests/SEBT.Portal.Tests.csproj --filter "FullyQualifiedName~ConfigurationTimingTests"`

Expected: PASS — confirming factory delegates can read WAF's config overrides.

If the test FAILS: the deferred loading approach won't work, and we should stop here. Report back with the failure details.

**Step 5: Run full test suite to verify spike doesn't break anything**

Run: `dotnet test test/SEBT.Portal.Tests/SEBT.Portal.Tests.csproj`

Expected: all tests pass (439 passed, 3 skipped, 0 failed — one new test)

**Step 6: Commit**

```
DC-172: Revert IHostBuilder extension, add config timing spike test

Revert f703121 — ConfigureHostBuilder.ConfigureServices runs callbacks
immediately in the minimal hosting model, not during Build(). Add a
spike test proving factory delegates CAN read ConfigureAppConfiguration
values at resolution time, validating the deferred loading approach.
```

---

### Task 2: Create PluginLoader with deferred MEF loading

Extract the MEF assembly loading logic from `AddPlugins` into a new `PluginLoader` class that loads lazily on first access. This is the core production change.

**Files:**
- Create: `src/SEBT.Portal.Api/Composition/PluginLoader.cs`

**Step 1: Create the PluginLoader class**

Create `src/SEBT.Portal.Api/Composition/PluginLoader.cs`:

```csharp
using System.Composition.Convention;
using System.Composition.Hosting;
using System.Runtime.Loader;
using SEBT.Portal.StatesPlugins.Interfaces;
using Serilog;

namespace SEBT.Portal.Api.Composition;

/// <summary>
/// Lazily loads MEF plugin assemblies and discovers exports on first access.
/// Takes IConfiguration via constructor injection from DI — at resolution time,
/// this includes all registered config sources (including WAF's
/// ConfigureAppConfiguration overrides in tests).
/// </summary>
internal sealed class PluginLoader
{
    private readonly Lazy<IReadOnlyDictionary<Type, object>> _exports;

    public PluginLoader(IConfiguration configuration)
    {
        _exports = new Lazy<IReadOnlyDictionary<Type, object>>(
            () => LoadExports(configuration));
    }

    /// <summary>
    /// Returns the plugin export for the given interface type, or null if no
    /// plugin provides it.
    /// </summary>
    public T? GetExport<T>() where T : class
    {
        _exports.Value.TryGetValue(typeof(T), out var export);
        return export as T;
    }

    private static IReadOnlyDictionary<Type, object> LoadExports(
        IConfiguration configuration)
    {
        var pluginAssemblyPaths = configuration
                                      .GetSection("PluginAssemblyPaths")
                                      .Get<string[]>()
                                  ?? throw new InvalidOperationException(
                                      "PluginAssemblyPaths missing from configuration.");

        Log.Information("Loading plugins from: {PluginAssemblyPaths}", pluginAssemblyPaths);

        var conventions = new ConventionBuilder();

        conventions
            .ForTypesDerivedFrom<IStateMetadataService>()
            .Export<IStateMetadataService>()
            .Shared();

        conventions
            .ForTypesDerivedFrom<IStateAuthenticationService>()
            .Export<IStateAuthenticationService>()
            .Shared();

        conventions
            .ForTypesDerivedFrom<ISummerEbtCaseService>()
            .Export<ISummerEbtCaseService>()
            .Shared();

        conventions
            .ForTypesDerivedFrom<IEnrollmentCheckService>()
            .Export<IEnrollmentCheckService>()
            .Shared();

        using var container = new ContainerConfiguration()
            .WithExport(configuration)
            .WithAssembliesInPath(pluginAssemblyPaths, conventions)
            .CreateContainer();

        var plugins = container.GetExports<IStatePlugin>();
        var exports = new Dictionary<Type, object>();

        foreach (var plugin in plugins)
        {
            Log.Information("Loaded plugin: {PluginType}", plugin.GetType().FullName);

            var pluginInterfaces = plugin.GetType().GetInterfaces()
                .Where(i => i != typeof(IStatePlugin))
                .ToList();

            switch (pluginInterfaces.Count)
            {
                case 0:
                    throw new InvalidOperationException(
                        $"Plugin '{plugin.GetType().FullName}' does not implement any interface besides IStatePlugin. " +
                        "Each plugin must implement exactly one service interface in addition to IStatePlugin.");
                case > 1:
                    throw new InvalidOperationException(
                        $"Plugin '{plugin.GetType().FullName}' implements multiple interfaces: " +
                        $"{string.Join(", ", pluginInterfaces.Select(i => i.FullName))}. " +
                        "Each plugin must implement exactly one service interface in addition to IStatePlugin.");
                default:
                    exports[pluginInterfaces[0]] = plugin;
                    break;
            }
        }

        return exports;
    }
}
```

**Step 2: Verify it compiles**

Run: `dotnet build src/SEBT.Portal.Api/SEBT.Portal.Api.csproj`

Expected: `Build succeeded.`

**Step 3: Commit**

```
DC-172: Add PluginLoader with deferred MEF assembly loading

Extract MEF loading logic into a singleton that loads lazily on first
DI resolution. Takes IConfiguration via constructor injection, so it
reads the fully-assembled config including WAF test overrides.
```

---

### Task 3: Rewrite AddPlugins to use PluginLoader factory delegates

Replace the eager loading in `AddPlugins` with factory delegate registrations that defer to `PluginLoader`. Remove the `IConfiguration` parameter — config is read at resolution time by `PluginLoader`. Update Program.cs call site.

**Files:**
- Modify: `src/SEBT.Portal.Api/Composition/ServiceCollectionPluginExtensions.cs`
- Modify: `src/SEBT.Portal.Api/Program.cs:71`

**Step 1: Rewrite ServiceCollectionPluginExtensions.cs**

Replace the entire file contents with:

```csharp
using SEBT.Portal.StatesPlugins.Interfaces;

namespace SEBT.Portal.Api.Composition;

/// <summary>
/// Registers MEF plugins via deferred factory delegates. Plugin assemblies
/// are loaded lazily by <see cref="PluginLoader"/> on first DI resolution,
/// when IConfiguration is fully assembled (including test overrides).
/// </summary>
internal static class ServiceCollectionPluginExtensions
{
    public static IServiceCollection AddPlugins(this IServiceCollection services)
    {
        services.AddSingleton<PluginLoader>();

        services.AddSingleton<IStateAuthenticationService>(sp =>
            sp.GetRequiredService<PluginLoader>()
                .GetExport<IStateAuthenticationService>()
            ?? new Defaults.DefaultIStateAuthenticationService());

        services.AddSingleton<IEnrollmentCheckService>(sp =>
            sp.GetRequiredService<PluginLoader>()
                .GetExport<IEnrollmentCheckService>()
            ?? new Defaults.DefaultEnrollmentCheckService());

        return services;
    }
}
```

Notes:
- `IStateMetadataService` is only in MEF conventions, never resolved from DI — no factory needed.
- `ISummerEbtCaseService` has no default. Tests that need it mock it via `ConfigureServices`. Plugin integration tests get it from the real plugin via `PluginLoader`. It is NOT registered here — tests and plugins provide it.
- Removed `using System.Composition.Convention`, `using System.Composition.Hosting`, `using Serilog`, `using Microsoft.Extensions.DependencyInjection.Extensions` — no longer needed.
- Removed `CreateContainerConfiguration` private method — moved to `PluginLoader`.

**Step 2: Update Program.cs call site**

In `src/SEBT.Portal.Api/Program.cs`, change line 71 from:

```csharp
builder.Services.AddPlugins(builder.Configuration);
```

to:

```csharp
builder.Services.AddPlugins();
```

**Step 3: Build to verify compilation**

Run: `dotnet build src/SEBT.Portal.Api/SEBT.Portal.Api.csproj`

Expected: `Build succeeded.`

**Step 4: Run full test suite**

Run: `dotnet test test/SEBT.Portal.Tests/SEBT.Portal.Tests.csproj`

Expected: all tests pass. The existing test factories still use env vars, and `PluginLoader` reads `IConfiguration` at resolution time. The env var values are in `IConfiguration` via the default env var provider, so `PluginLoader` sees them.

**Step 5: Commit**

```
DC-172: Rewrite AddPlugins to use deferred PluginLoader factory delegates

Replace eager MEF loading with factory delegates that defer to
PluginLoader. AddPlugins no longer takes IConfiguration — config is
read at resolution time when it's fully assembled.
```

---

### Task 4: Refactor PortalWebApplicationFactory to use ConfigureAppConfiguration

Replace the env var constructor/dispose lifecycle with `ConfigureAppConfiguration` and an in-memory collection.

**Files:**
- Modify: `test/SEBT.Portal.Tests/Integration/PortalWebApplicationFactory.cs`

**Step 1: Replace the factory implementation**

Replace the entire file contents with:

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.StatesPlugins.Interfaces;

namespace SEBT.Portal.Tests.Integration;

/// <summary>
/// Custom WebApplicationFactory for integration tests.
/// Replaces SQL Server with InMemory EF provider and mocks
/// database migration/seeding so tests don't need a real database.
/// Plugin directories are configured but empty, so no state plugins load.
/// </summary>
public class PortalWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PluginAssemblyPaths:0"] = "plugins-test",
                ["JwtSettings:SecretKey"] =
                    "integration-test-key-must-be-at-least-32-bytes-long",
            }));

        builder.ConfigureServices(services =>
        {
            // Remove the real SQL Server DbContext registration
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<PortalDbContext>));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            // Add InMemory EF provider instead
            services.AddDbContext<PortalDbContext>(options =>
                options.UseInMemoryDatabase("IntegrationTests"));

            // Replace database migrator and seeder with no-ops
            var migratorDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IDatabaseMigrator));
            if (migratorDescriptor != null)
            {
                services.Remove(migratorDescriptor);
            }
            services.AddScoped(_ => Substitute.For<IDatabaseMigrator>());

            var seederDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IDatabaseSeeder));
            if (seederDescriptor != null)
            {
                services.Remove(seederDescriptor);
            }
            services.AddScoped(_ => Substitute.For<IDatabaseSeeder>());

            // Override plugin factory delegates with mocks.
            // These AddSingleton calls come after AddPlugins' factory registrations
            // (which ran during Program.cs), so they win — last registration wins in DI.
            services.AddSingleton(Substitute.For<ISummerEbtCaseService>());
            services.AddSingleton(Substitute.For<IEnrollmentCheckService>());
        });
    }
}
```

**Step 2: Run integration tests**

Run: `dotnet test test/SEBT.Portal.Tests/SEBT.Portal.Tests.csproj --filter "FullyQualifiedName~Integration.EnrollmentCheck"`

Expected: all `EnrollmentCheckEndpointTests` pass

**Step 3: Run full test suite**

Run: `dotnet test test/SEBT.Portal.Tests/SEBT.Portal.Tests.csproj`

Expected: all tests pass

**Step 4: Commit**

```
DC-172: Refactor PortalWebApplicationFactory to use ConfigureAppConfiguration

Replace env var constructor/dispose lifecycle with standard
ConfigureAppConfiguration and in-memory collection. No more
process-global state mutation.
```

---

### Task 5: Refactor PluginIntegrationWebApplicationFactory to use ConfigureAppConfiguration

Same pattern as Task 4. Store constructor parameters and apply them via `ConfigureAppConfiguration` instead of env vars.

**Files:**
- Modify: `test/SEBT.Portal.Tests/Integration/PluginIntegration/PluginIntegrationWebApplicationFactory.cs`

**Step 1: Replace the factory implementation**

Replace the entire file contents with:

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.StatesPlugins.Interfaces;

namespace SEBT.Portal.Tests.Integration.PluginIntegration;

/// <summary>
/// WebApplicationFactory for plugin integration tests.
/// Loads real MEF plugins from specified directories instead of registering mocks.
/// Uses InMemory EF and mock migrator/seeder (same as PortalWebApplicationFactory)
/// but does NOT register mock plugin stubs — real plugins or default fallbacks
/// provide the implementations via PluginLoader factory delegates.
/// </summary>
/// <remarks>
/// Plugin paths and connection strings are injected via ConfigureAppConfiguration.
/// PluginLoader reads IConfiguration at DI resolution time, so it sees these
/// overrides without needing process-global environment variables.
/// </remarks>
public class PluginIntegrationWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string? _pluginDir;
    private readonly Dictionary<string, string>? _configOverrides;

    public PluginIntegrationWebApplicationFactory(
        string? pluginDir = null,
        Dictionary<string, string>? configOverrides = null)
    {
        _pluginDir = pluginDir;
        _configOverrides = configOverrides;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["PluginAssemblyPaths:0"] = _pluginDir != null
                    ? PluginPathResolver.Resolve(_pluginDir)
                    : "plugins-none",
                ["JwtSettings:SecretKey"] =
                    "integration-test-key-must-be-at-least-32-bytes-long",
            };

            if (_configOverrides != null)
            {
                foreach (var (key, value) in _configOverrides)
                {
                    overrides[key] = value;
                }
            }

            config.AddInMemoryCollection(overrides);
        });

        builder.ConfigureServices(services =>
        {
            // Replace SQL Server with InMemory EF
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<PortalDbContext>));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            services.AddDbContext<PortalDbContext>(options =>
                options.UseInMemoryDatabase($"PluginIntegrationTests-{Guid.NewGuid()}"));

            // Replace database migrator and seeder with no-ops
            var migratorDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IDatabaseMigrator));
            if (migratorDescriptor != null)
            {
                services.Remove(migratorDescriptor);
            }

            services.AddScoped(_ => Substitute.For<IDatabaseMigrator>());

            var seederDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IDatabaseSeeder));
            if (seederDescriptor != null)
            {
                services.Remove(seederDescriptor);
            }

            services.AddScoped(_ => Substitute.For<IDatabaseSeeder>());

            // ISummerEbtCaseService has no default fallback in AddPlugins, and
            // HouseholdRepository depends on it. Register a mock so DI validation
            // passes. TryAddSingleton is a no-op if a real plugin already registered it.
            services.TryAddSingleton(Substitute.For<ISummerEbtCaseService>());
        });
    }
}
```

**Step 2: Run plugin integration tests**

Run: `dotnet test test/SEBT.Portal.Tests/SEBT.Portal.Tests.csproj --filter "FullyQualifiedName~PluginIntegration"`

Expected: DefaultEnrollmentCheckIntegrationTests passes; DC and CO tests skip

**Step 3: Commit**

```
DC-172: Refactor PluginIntegrationWebApplicationFactory to use ConfigureAppConfiguration

Replace env var constructor/dispose lifecycle with standard
ConfigureAppConfiguration. Config overrides are now scoped to each
factory instance instead of mutating process-global state.
```

---

### Task 6: Update test classes, remove Collection, clean up spike

Update config key format in DC and CO test constructors. Remove `[Collection("PluginIntegration")]` from all plugin integration test classes. Delete the collection definition file. Remove the spike test from Task 1.

**Files:**
- Modify: `test/SEBT.Portal.Tests/Integration/PluginIntegration/DcEnrollmentCheckIntegrationTests.cs`
- Modify: `test/SEBT.Portal.Tests/Integration/PluginIntegration/CoEnrollmentCheckIntegrationTests.cs`
- Modify: `test/SEBT.Portal.Tests/Integration/PluginIntegration/DefaultEnrollmentCheckIntegrationTests.cs`
- Delete: `test/SEBT.Portal.Tests/Integration/PluginIntegration/PluginIntegrationCollection.cs`
- Delete: `test/SEBT.Portal.Tests/Integration/ConfigurationTimingTests.cs`

**Step 1: Update DcEnrollmentCheckIntegrationTests**

Remove the `[Collection("PluginIntegration")]` attribute.

Change the config override key from:

```csharp
["DCConnector__ConnectionString"] = dcDatabase.ConnectionString
```

to:

```csharp
["DCConnector:ConnectionString"] = dcDatabase.ConnectionString
```

**Step 2: Update CoEnrollmentCheckIntegrationTests**

Remove the `[Collection("PluginIntegration")]` attribute.

Change the config override keys from:

```csharp
["COConnector__CbmsApiBaseUrl"] = apiBaseUrl,
["COConnector__CbmsApiKey"] = apiKey
```

to:

```csharp
["COConnector:CbmsApiBaseUrl"] = apiBaseUrl,
["COConnector:CbmsApiKey"] = apiKey
```

Note: the `Environment.GetEnvironmentVariable(...)` reads on lines 28-29 stay unchanged — they check whether credentials are available for skip logic, not config injection.

**Step 3: Update DefaultEnrollmentCheckIntegrationTests**

Remove the `[Collection("PluginIntegration")]` attribute. No other changes.

**Step 4: Delete PluginIntegrationCollection.cs**

Delete `test/SEBT.Portal.Tests/Integration/PluginIntegration/PluginIntegrationCollection.cs`.

**Step 5: Delete ConfigurationTimingTests.cs**

Delete `test/SEBT.Portal.Tests/Integration/ConfigurationTimingTests.cs` (the spike test from Task 1 — it served its purpose).

**Step 6: Run full test suite**

Run: `dotnet test test/SEBT.Portal.Tests/SEBT.Portal.Tests.csproj`

Expected: all tests pass (438 passed, 3 skipped, 0 failed — back to original count after spike removal)

**Step 7: Commit**

```
DC-172: Remove Collection serialization, update config keys, clean up spike

Tests no longer mutate process-global state, so [Collection] serialization
is unnecessary. Update config override keys from env var format (__) to
standard .NET hierarchical format (:). Remove the config timing spike test.
```
