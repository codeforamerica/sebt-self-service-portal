# Phone-Indexed Mock CBMS Data Store — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the single-response mock CBMS handler with a phone-indexed, cache-backed data store that returns per-household responses and supports PATCH mutations.

**Architecture:** A `MockCbmsDataStore` backed by `IHybridCache` seeds from embedded JSON files indexed by a phone manifest. `MockCbmsHttpHandler` delegates to the data store for phone lookups and PATCH operations. The main repo's plugin system is updated to use DI-based instantiation instead of MEF composition, enabling `HybridCache` injection into plugins.

**Tech Stack:** .NET 10, `Microsoft.Extensions.Caching.Hybrid`, `System.Text.Json`, xUnit

**Spec:** `docs/superpowers/specs/2026-04-06-phone-indexed-mock-cbms-data-store-design.md`

---

## File Map

### Main repo (sebt-self-service-portal)

| Action | File | Responsibility |
|--------|------|----------------|
| Modify | `src/SEBT.Portal.Api/SEBT.Portal.Api.csproj` | Add `Microsoft.Extensions.Caching.Hybrid` package |
| Modify | `src/SEBT.Portal.Api/Composition/ServiceCollectionPluginExtensions.cs` | Replace MEF composition with type scanning + DI factory registration; add `HybridCache` |
| Modify | `src/SEBT.Portal.Api/Composition/ContainerConfigurationExtensions.cs` | Refactor to return loaded assemblies instead of `ContainerConfiguration` |
| Modify | `src/SEBT.Portal.Api/Program.cs` | No change expected (calls `AddPlugins` already) |
| Create | `test/SEBT.Portal.Tests/Composition/PluginAssemblyScannerTests.cs` | Tests for assembly scanning and DI factory registration |
| Modify | `test/SEBT.Portal.Tests/SEBT.Portal.Tests.csproj` | Add `Microsoft.Extensions.Caching.Hybrid` if needed for tests |

### CO connector repo (sebt-self-service-portal-co-connector)

| Action | File | Responsibility |
|--------|------|----------------|
| Create | `src/SEBT.Portal.StatePlugins.CO.CbmsApi/Mocks/MockCbmsDataStore.cs` | Cache-backed data store: seed, phone lookup, PATCH mutation |
| Create | `src/SEBT.Portal.StatePlugins.CO.CbmsApi/TestData/CbmsMocks/mock-manifest.json` | Phone → filename index |
| Create | `src/SEBT.Portal.StatePlugins.CO.CbmsApi/TestData/CbmsMocks/household-*.json` | Per-household response files (provided by user) |
| Modify | `src/SEBT.Portal.StatePlugins.CO.CbmsApi/Mocks/MockCbmsHttpHandler.cs` | Delegate to `MockCbmsDataStore`; add PATCH route |
| Modify | `src/SEBT.Portal.StatePlugins.CO/ColoradoSummerEbtCaseService.cs` | Accept `HybridCache?`; create data store in mock mode |
| Modify | `src/SEBT.Portal.StatePlugins.CO/Cbms/CbmsOptionsHelper.cs` | Remove `Return404ForGetAccountDetails` option |
| Modify | `src/SEBT.Portal.StatePlugins.CO/SEBT.Portal.StatePlugins.CO.csproj` | Add `Microsoft.Extensions.Caching.Hybrid` package |
| Modify | `src/SEBT.Portal.StatePlugins.CO.CbmsApi/SEBT.Portal.StatePlugins.CO.CbmsApi.csproj` | Ensure new JSON files are `EmbeddedResource` (wildcard already covers `*.json`) |
| Create | `src/SEBT.Portal.StatePlugins.CO.Tests/CbmsApi/MockCbmsDataStoreTests.cs` | Unit tests for data store |
| Modify | `src/SEBT.Portal.StatePlugins.CO.Tests/ColoradoSummerEbtCaseServiceTests.cs` | Update tests for new mock behavior |
| Modify | `src/SEBT.Portal.StatePlugins.CO.Tests/SEBT.Portal.StatePlugins.CO.Tests.csproj` | Add `Microsoft.Extensions.Caching.Hybrid` for test `HybridCache` |

---

## Part A: Plugin DI Bridge (main repo)

> **Working directory:** `sebt-self-service-portal` (main portal repo)
>
> Create a feature branch in this repo before starting.

### Task 1: Refactor `ContainerConfigurationExtensions` to Return Loaded Assemblies

**Files:**
- Modify: `src/SEBT.Portal.Api/Composition/ContainerConfigurationExtensions.cs`

- [ ] **Step 1: Read the current file**

Read `src/SEBT.Portal.Api/Composition/ContainerConfigurationExtensions.cs` in full. Understand the assembly loading logic — duplicate detection, host assembly filtering, `AssemblyLoadContext.Default.Resolving` handler.

- [ ] **Step 2: Rename class and refactor return type**

Rename `ContainerConfigurationExtensions` to `PluginAssemblyLoader`. Change the method signature from:

```csharp
public static ContainerConfiguration WithAssembliesInPath(
    this ContainerConfiguration containerConfiguration,
    string[] paths,
    AttributedModelProvider conventions,
    SearchOption searchOption = SearchOption.TopDirectoryOnly)
```

to:

```csharp
public static List<Assembly> LoadAssembliesFromPaths(
    string[] paths,
    SearchOption searchOption = SearchOption.TopDirectoryOnly)
```

Remove the `containerConfiguration` and `conventions` parameters. Remove `containerConfiguration.WithAssemblies(assemblies, conventions)` at the end of the inner loop; instead, accumulate all loaded assemblies into a flat `List<Assembly>` and return it.

Keep all the existing logic intact: `DefaultResolving` handler, `hostAssemblySimpleNames` filtering, `loadedNames` dedup, `FileLoadException`/`BadImageFormatException` catch.

Add a block comment at the top of the class:

```csharp
// Plugin Assembly Loader
//
// Loads plugin assemblies from disk into AssemblyLoadContext.Default.
// This was originally part of the MEF (System.Composition) pipeline but MEF is
// no longer used for composition — plugins are now instantiated by the DI container
// via ActivatorUtilities. The [Export], [ExportMetadata], and [ImportingConstructor]
// attributes on plugin classes are inert but harmless.
//
// TODO: Remove the System.Composition dependency entirely once this approach is stable.
// The assembly loading logic here is standalone and does not depend on MEF.
```

- [ ] **Step 3: Build to verify compilation**

Run: `dotnet build src/SEBT.Portal.Api/SEBT.Portal.Api.csproj`
Expected: Build succeeds (callers will break, but we fix those in Task 2)

- [ ] **Step 4: Commit**

```
feat: refactor plugin assembly loader to return assemblies directly

Decouples assembly loading from MEF ContainerConfiguration. The loader
now returns a List<Assembly> instead of mutating a ContainerConfiguration,
preparing for DI-based plugin instantiation.
```

### Task 2: Replace MEF Composition with DI Factory Registration

**Files:**
- Modify: `src/SEBT.Portal.Api/SEBT.Portal.Api.csproj`
- Modify: `src/SEBT.Portal.Api/Composition/ServiceCollectionPluginExtensions.cs`

- [ ] **Step 1: Add HybridCache package to API project**

Add to `src/SEBT.Portal.Api/SEBT.Portal.Api.csproj` in the `<ItemGroup>` with other PackageReferences:

```xml
<PackageReference Include="Microsoft.Extensions.Caching.Hybrid" Version="9.6.0" />
```

- [ ] **Step 2: Read the current `ServiceCollectionPluginExtensions.cs`**

Read in full. Note the MEF convention builders, `CreateContainerConfiguration`, `container.GetExports<IStatePlugin>()`, health check wiring, and plugin interface validation.

- [ ] **Step 3: Rewrite `AddPlugins` to use assembly scanning + DI factories**

Replace the contents of `ServiceCollectionPluginExtensions.cs` with:

```csharp
// TODO: Remove System.Composition dependency — only referenced for assembly loading reuse
// and because [Export]/[ExportMetadata] attributes remain on plugin classes (inert).
using Serilog;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SEBT.Portal.StatesPlugins.Interfaces;

namespace SEBT.Portal.Api.Composition;

// Plugin Discovery and Registration
//
// Plugins are discovered by scanning assemblies loaded from plugins-{state}/ directories.
// Each plugin must implement exactly one service interface (e.g., ISummerEbtCaseService)
// in addition to IStatePlugin.
//
// Plugins are instantiated by the DI container via ActivatorUtilities — NOT by MEF.
// This means plugin constructors can receive any DI-registered service (IConfiguration,
// ILoggerFactory, HybridCache, etc.) as constructor parameters.
//
// The MEF attributes ([Export], [ExportMetadata], [ImportingConstructor]) on plugin
// classes are currently inert — they are not read or used by this code. They remain
// for now because the plugin assemblies have not been updated to remove them.
//
// Next step: extract assembly loading into a standalone helper and remove the
// System.Composition dependency entirely.
internal static class ServiceCollectionPluginExtensions
{
    public static IServiceCollection AddPlugins(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton<IStateAuthenticationService, Defaults.DefaultStateAuthenticationService>();
        services.TryAddSingleton<IStateHealthCheckService, Defaults.DefaultStateHealthCheckService>();
        services.TryAddSingleton<ISummerEbtCaseService, Defaults.DefaultSummerEbtCaseService>();
        services.TryAddSingleton<IEnrollmentCheckService, Defaults.DefaultEnrollmentCheckService>();
        services.TryAddSingleton<IAddressUpdateService, Defaults.DefaultAddressUpdateService>();

        services.AddHybridCache();

        var healthChecksBuilder = services.AddHealthChecks();

        var pluginAssemblyPaths = configuration
                                      .GetSection("PluginAssemblyPaths")
                                      .Get<string[]>()
                                  ?? throw new InvalidOperationException("PluginAssemblyPaths missing from configuration.");
        Log.Information("Loading plugins from: {PluginAssemblyPaths}", pluginAssemblyPaths);

        var loadedAssemblies = PluginAssemblyLoader.LoadAssembliesFromPaths(pluginAssemblyPaths);

        var pluginTypes = loadedAssemblies
            .SelectMany(a =>
            {
                try { return a.GetExportedTypes(); }
                catch (TypeLoadException ex)
                {
                    Log.Warning(ex, "Could not load types from assembly {Assembly}", a.FullName);
                    return [];
                }
            })
            .Where(t => typeof(IStatePlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
            .ToList();

        foreach (var pluginType in pluginTypes)
        {
            Log.Information("Discovered plugin type: {PluginType}", pluginType.FullName);

            var pluginInterfaces = pluginType.GetInterfaces()
                .Where(i => i != typeof(IStatePlugin))
                .ToList();

            switch (pluginInterfaces.Count)
            {
                case 0:
                    throw new InvalidOperationException(
                        $"Plugin '{pluginType.FullName}' does not implement any interface besides IStatePlugin. " +
                        "Each plugin must implement exactly one service interface in addition to IStatePlugin.");
                case > 1:
                    throw new InvalidOperationException(
                        $"Plugin '{pluginType.FullName}' implements multiple interfaces: " +
                        $"{string.Join(", ", pluginInterfaces.Select(i => i.FullName))}. " +
                        "Each plugin must implement exactly one service interface in addition to IStatePlugin.");
            }

            var pluginInterface = pluginInterfaces[0];

            if (typeof(IStateHealthCheckService).IsAssignableFrom(pluginType))
            {
                // Health check plugins are instantiated eagerly so we can call
                // ConfigureHealthChecks() during service registration (it needs
                // IHealthChecksBuilder, which wraps IServiceCollection).
                //
                // LIMITATION: This builds a temporary IServiceProvider from the current
                // IServiceCollection to resolve constructor dependencies. The temporary
                // provider has its own singleton scope, which works today because health
                // check plugins only depend on IConfiguration and ILoggerFactory — both
                // are already fully constructed at this point and are effectively shared.
                //
                // If a health check plugin ever needs a DI service with shared mutable
                // state (e.g., HybridCache backed by Redis), the temporary provider
                // would create a separate instance, breaking shared-state assumptions.
                // At that point, revisit this approach — e.g., defer health check
                // registration to a post-build step, or use a type-based registration
                // with a lazy resolve adapter.
                using var tempProvider = services.BuildServiceProvider();
                var instance = ActivatorUtilities.CreateInstance(tempProvider, pluginType);
                Log.Information("Constructed health check plugin: {PluginType}", pluginType.FullName);

                ((IStateHealthCheckService)instance).ConfigureHealthChecks(healthChecksBuilder);
                services.AddSingleton(pluginInterface, instance);
            }
            else
            {
                // All other plugins: register as a factory so DI creates them on first
                // resolve using the *real* service provider. This gives plugins access
                // to any DI-registered service via constructor injection.
                var capturedType = pluginType; // avoid closure over loop variable
                services.AddSingleton(pluginInterface, sp =>
                {
                    var logger = sp.GetRequiredService<ILoggerFactory>()
                        .CreateLogger("SEBT.Portal.Api.Composition");
                    logger.LogInformation("Constructing plugin: {PluginType}", capturedType.FullName);
                    return ActivatorUtilities.CreateInstance(sp, capturedType);
                });
            }
        }

        return services;
    }
}
```

- [ ] **Step 4: Remove unused usings and verify build**

The old file imported `System.Composition.Convention`, `System.Composition.Hosting`, `Serilog.Extensions.Logging`. Remove imports that are no longer needed. Ensure `using Microsoft.Extensions.Caching.Hybrid;` is present if needed (it may not be — `AddHybridCache()` is an extension method on `IServiceCollection` from `Microsoft.Extensions.Caching.Hybrid`).

Run: `dotnet build src/SEBT.Portal.Api/SEBT.Portal.Api.csproj`
Expected: Build succeeds.

- [ ] **Step 5: Commit**

```
feat: replace MEF plugin composition with DI-based instantiation

Plugins are now created by ActivatorUtilities instead of MEF, allowing
plugin constructors to receive any DI-registered service. Assembly
loading is unchanged. HybridCache is registered before plugin loading.
Health check plugins remain eagerly instantiated with a documented
limitation on shared-state services.
```

### Task 3: Plugin DI Bridge Tests

**Files:**
- Create: `test/SEBT.Portal.Tests/Composition/PluginAssemblyScannerTests.cs`
- Modify: `test/SEBT.Portal.Tests/SEBT.Portal.Tests.csproj` (if needed)

- [ ] **Step 1: Write test for assembly scanning discovering plugin types**

Create `test/SEBT.Portal.Tests/Composition/PluginAssemblyScannerTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run test to verify it passes**

Run: `dotnet test test/SEBT.Portal.Tests/SEBT.Portal.Tests.csproj --filter "FullyQualifiedName~PluginAssemblyScannerTests"`
Expected: PASS. With no plugin assemblies at the path, defaults are registered.

Note: `ServiceCollectionPluginExtensions` is `internal`. The test project references the API project — if the test can't see `AddPlugins`, add `[InternalsVisibleTo("SEBT.Portal.Tests")]` to `ServiceCollectionPluginExtensions.cs` or to the API `.csproj` via:

```xml
<ItemGroup>
    <InternalsVisibleTo Include="SEBT.Portal.Tests" />
</ItemGroup>
```

- [ ] **Step 3: Commit**

```
test: add plugin assembly scanner tests for DI bridge
```

### Task 4: Run Full Main Repo Test Suite

- [ ] **Step 1: Run all backend tests**

Run: `dotnet test` (from repo root, or `pnpm api:test`)
Expected: All tests pass. The existing tests should work because the default plugin registrations are unchanged and no real plugin assemblies are loaded in test.

- [ ] **Step 2: Fix any failures**

If tests fail, investigate. Likely causes:
- `AddPlugins` internals visibility — add `InternalsVisibleTo` if needed
- Missing `Microsoft.Extensions.Caching.Hybrid` package in test project — add it
- Any test that directly called `CreateContainerConfiguration` — update to use new API

- [ ] **Step 3: Commit fixes if any**

---

## Part B: Mock Data Store (CO connector repo)

> **Working directory:** `sebt-self-service-portal-co-connector` (this repo)
>
> Branch `feature/realistic-cbms-mock-responses` is already created.
>
> **Prerequisite:** User provides household JSON files and places them in `src/SEBT.Portal.StatePlugins.CO.CbmsApi/TestData/CbmsMocks/`.

### Task 5: Add Mock Household JSON Files and Generate Manifest

**Files:**
- Create: `src/SEBT.Portal.StatePlugins.CO.CbmsApi/TestData/CbmsMocks/mock-manifest.json`
- Create: `src/SEBT.Portal.StatePlugins.CO.CbmsApi/TestData/CbmsMocks/household-*.json` (user-provided)

- [ ] **Step 1: Confirm user has placed household JSON files**

Check that household JSON files exist in `src/SEBT.Portal.StatePlugins.CO.CbmsApi/TestData/CbmsMocks/`. Each file should be a complete `GetAccountDetailsResponse` with `stdntEnrollDtls` array, `respCd`, `respMsg`.

- [ ] **Step 2: Generate manifest from household files**

Read each household JSON file. Extract the `gurdPhnNm` field from the first entry in `stdntEnrollDtls`. Normalize to 10 digits (strip non-digits, remove leading `1` if 11+ digits). Write `mock-manifest.json`:

```json
{
    "households": {
        "<10-digit-phone>": "<filename>.json",
        ...
    }
}
```

- [ ] **Step 3: Update existing `get-account-details.json` phone to be valid 10-digit**

The existing `get-account-details.json` has `"gurdPhnNm": "555-0123"` (7 digits). Update it to a valid 10-digit phone like `"gurdPhnNm": "3035550123"` and include it in the manifest — OR remove it if it's superseded by the new household files. Decide based on what the user provided.

- [ ] **Step 4: Verify embedded resource wildcard covers new files**

Check `src/SEBT.Portal.StatePlugins.CO.CbmsApi/SEBT.Portal.StatePlugins.CO.CbmsApi.csproj` — it already has:

```xml
<EmbeddedResource Include="TestData\CbmsMocks\*.json" />
```

This covers all new JSON files. No csproj change needed.

- [ ] **Step 5: Build to verify resources compile**

Run: `dotnet build src/SEBT.Portal.StatePlugins.CO.CbmsApi/SEBT.Portal.StatePlugins.CO.CbmsApi.csproj`
Expected: Build succeeds.

- [ ] **Step 6: Commit**

```
feat: add mock household JSON files and phone manifest

Adds per-household CBMS response files indexed by a phone manifest.
These will be loaded by MockCbmsDataStore to return phone-specific
mock responses.
```

### Task 6: Implement `MockCbmsDataStore` — Seeding and Phone Lookup (TDD)

**Files:**
- Create: `src/SEBT.Portal.StatePlugins.CO.CbmsApi/Mocks/MockCbmsDataStore.cs`
- Create: `src/SEBT.Portal.StatePlugins.CO.Tests/CbmsApi/MockCbmsDataStoreTests.cs`
- Modify: `src/SEBT.Portal.StatePlugins.CO.Tests/SEBT.Portal.StatePlugins.CO.Tests.csproj`

- [ ] **Step 1: Add HybridCache package to test project**

Add to `src/SEBT.Portal.StatePlugins.CO.Tests/SEBT.Portal.StatePlugins.CO.Tests.csproj`:

```xml
<PackageReference Include="Microsoft.Extensions.Caching.Hybrid" Version="9.6.0" />
```

- [ ] **Step 2: Write failing tests for seeding and phone lookup**

Create `src/SEBT.Portal.StatePlugins.CO.Tests/CbmsApi/MockCbmsDataStoreTests.cs`:

```csharp
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Mocks;

namespace SEBT.Portal.StatePlugins.CO.Tests.CbmsApi;

public class MockCbmsDataStoreTests
{
    private static HybridCache CreateInMemoryHybridCache()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<HybridCache>();
    }

    [Fact]
    public async Task GetResponseForPhone_returns_household_json_for_known_phone()
    {
        var cache = CreateInMemoryHybridCache();
        var store = new MockCbmsDataStore(cache);

        // Use a phone number that exists in mock-manifest.json
        // (will be updated once real household files are in place)
        var result = await store.GetResponseForPhoneAsync("7198004382");

        Assert.NotNull(result);
        Assert.Contains("stdntEnrollDtls", result);
        Assert.Contains("respCd", result);
    }

    [Fact]
    public async Task GetResponseForPhone_returns_empty_success_for_unknown_phone()
    {
        var cache = CreateInMemoryHybridCache();
        var store = new MockCbmsDataStore(cache);

        var result = await store.GetResponseForPhoneAsync("9999999999");

        Assert.NotNull(result);
        Assert.Contains("\"stdntEnrollDtls\":[]", result);
        Assert.Contains("\"respCd\":\"00\"", result);
    }

    [Fact]
    public async Task GetResponseForPhone_seeds_cache_on_first_access()
    {
        var cache = CreateInMemoryHybridCache();
        var store = new MockCbmsDataStore(cache);

        // First call triggers seeding
        var result1 = await store.GetResponseForPhoneAsync("7198004382");
        // Second call reads from cache (should return same data)
        var result2 = await store.GetResponseForPhoneAsync("7198004382");

        Assert.Equal(result1, result2);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test src/SEBT.Portal.StatePlugins.CO.Tests/SEBT.Portal.StatePlugins.CO.Tests.csproj --filter "FullyQualifiedName~MockCbmsDataStoreTests"`
Expected: FAIL — `MockCbmsDataStore` does not exist yet.

- [ ] **Step 4: Add HybridCache package to the CO plugin project**

Add to `src/SEBT.Portal.StatePlugins.CO/SEBT.Portal.StatePlugins.CO.csproj`:

```xml
<PackageReference Include="Microsoft.Extensions.Caching.Hybrid" Version="9.6.0" />
```

- [ ] **Step 5: Implement `MockCbmsDataStore` — seeding and read**

Create `src/SEBT.Portal.StatePlugins.CO.CbmsApi/Mocks/MockCbmsDataStore.cs`:

```csharp
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Caching.Hybrid;

namespace SEBT.Portal.StatePlugins.CO.CbmsApi.Mocks;

/// <summary>
/// Cache-backed mock data store for CBMS API responses. Seeds from embedded
/// JSON files on first access, indexed by phone number via a manifest file.
/// Supports read (phone lookup) and write (PATCH mutation) operations.
/// Thread safety is provided by HybridCache.
/// </summary>
public sealed class MockCbmsDataStore
{
    private const string CacheKeyPrefix = "cbms-mock:";
    private const string ManifestResourceName = "SEBT.Portal.StatePlugins.CO.CbmsApi.TestData.CbmsMocks.mock-manifest.json";
    private const string ResourcePrefix = "SEBT.Portal.StatePlugins.CO.CbmsApi.TestData.CbmsMocks.";

    private static readonly string EmptySuccessResponse =
        JsonSerializer.Serialize(new { stdntEnrollDtls = Array.Empty<object>(), respCd = "00", respMsg = "Success" });

    private readonly HybridCache _cache;
    private readonly SemaphoreSlim _seedLock = new(1, 1);
    private volatile bool _seeded;

    // Phone numbers from the manifest, populated during seeding.
    // Used by ApplyPatchAsync to search across all households.
    private IReadOnlyList<string> _knownPhones = [];

    public MockCbmsDataStore(HybridCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        _cache = cache;
    }

    public async Task<string> GetResponseForPhoneAsync(string normalizedPhone, CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken).ConfigureAwait(false);

        var cacheKey = CacheKeyPrefix + normalizedPhone;
        var result = await _cache.GetOrCreateAsync(
            cacheKey,
            cancellationToken: cancellationToken,
            factory: (ct) => ValueTask.FromResult<string?>(null)).ConfigureAwait(false);

        return result ?? EmptySuccessResponse;
    }

    private async Task EnsureSeededAsync(CancellationToken cancellationToken)
    {
        if (_seeded) return;

        await _seedLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_seeded) return;

            var manifest = LoadManifest();
            var phones = new List<string>();

            foreach (var (phone, fileName) in manifest)
            {
                var json = LoadEmbeddedJson(fileName);
                var cacheKey = CacheKeyPrefix + phone;
                await _cache.SetAsync(cacheKey, json, cancellationToken: cancellationToken).ConfigureAwait(false);
                phones.Add(phone);
            }

            _knownPhones = phones;
            _seeded = true;
        }
        finally
        {
            _seedLock.Release();
        }
    }

    private static Dictionary<string, string> LoadManifest()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ManifestResourceName)
            ?? throw new InvalidOperationException($"Mock manifest not found: {ManifestResourceName}");
        using var doc = JsonDocument.Parse(stream);
        var households = doc.RootElement.GetProperty("households");

        var result = new Dictionary<string, string>();
        foreach (var prop in households.EnumerateObject())
        {
            result[prop.Name] = prop.Value.GetString()
                ?? throw new InvalidOperationException($"Null filename for phone {prop.Name} in manifest");
        }
        return result;
    }

    private static string LoadEmbeddedJson(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = ResourcePrefix + fileName;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Mock household JSON not found: {resourceName}. Ensure TestData/CbmsMocks/{fileName} is an EmbeddedResource.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test src/SEBT.Portal.StatePlugins.CO.Tests/SEBT.Portal.StatePlugins.CO.Tests.csproj --filter "FullyQualifiedName~MockCbmsDataStoreTests"`
Expected: All 3 tests PASS.

- [ ] **Step 7: Commit**

```
feat: add MockCbmsDataStore with HybridCache-backed phone lookup

Seeds from embedded JSON files via manifest on first access. Returns
correct household for known phones, empty success for unknown phones.
```

### Task 7: Implement `MockCbmsDataStore.ApplyPatchAsync` (TDD)

**Files:**
- Modify: `src/SEBT.Portal.StatePlugins.CO.CbmsApi/Mocks/MockCbmsDataStore.cs`
- Modify: `src/SEBT.Portal.StatePlugins.CO.Tests/CbmsApi/MockCbmsDataStoreTests.cs`

- [ ] **Step 1: Write failing tests for PATCH mutations**

Add to `MockCbmsDataStoreTests.cs`:

```csharp
[Fact]
public async Task ApplyPatchAsync_updates_address_on_matching_student()
{
    var cache = CreateInMemoryHybridCache();
    var store = new MockCbmsDataStore(cache);

    // Seed by reading first
    var before = await store.GetResponseForPhoneAsync("7198004382");
    var beforeDoc = JsonDocument.Parse(before);
    var firstStudent = beforeDoc.RootElement.GetProperty("stdntEnrollDtls")[0];
    var sebtChldId = firstStudent.GetProperty("sebtChldId").GetInt32().ToString();

    var patchBody = JsonSerializer.Serialize(new
    {
        sebtChldId,
        addr = new
        {
            addrLn1 = "999 New Street",
            addrLn2 = "Unit 7",
            cty = "Boulder",
            staCd = "CO",
            zip = "80301",
            zip4 = "1111"
        }
    });

    var result = await store.ApplyPatchAsync(patchBody);

    Assert.Contains("\"respCd\":\"00\"", result);

    // Verify mutation persisted
    var after = await store.GetResponseForPhoneAsync("7198004382");
    Assert.Contains("999 New Street", after);
    Assert.Contains("Boulder", after);
}

[Fact]
public async Task ApplyPatchAsync_updates_guardian_fields_on_matching_student()
{
    var cache = CreateInMemoryHybridCache();
    var store = new MockCbmsDataStore(cache);

    var before = await store.GetResponseForPhoneAsync("7198004382");
    var beforeDoc = JsonDocument.Parse(before);
    var firstStudent = beforeDoc.RootElement.GetProperty("stdntEnrollDtls")[0];
    var sebtChldId = firstStudent.GetProperty("sebtChldId").GetInt32().ToString();

    var patchBody = JsonSerializer.Serialize(new
    {
        sebtChldId,
        gurdFstNm = "UpdatedFirst",
        gurdLstNm = "UpdatedLast",
        gurdEmailAddr = "updated@example.com"
    });

    var result = await store.ApplyPatchAsync(patchBody);

    Assert.Contains("\"respCd\":\"00\"", result);

    var after = await store.GetResponseForPhoneAsync("7198004382");
    Assert.Contains("UpdatedFirst", after);
    Assert.Contains("UpdatedLast", after);
    Assert.Contains("updated@example.com", after);
}

[Fact]
public async Task ApplyPatchAsync_returns_404_for_unknown_sebtChldId()
{
    var cache = CreateInMemoryHybridCache();
    var store = new MockCbmsDataStore(cache);

    // Seed
    await store.GetResponseForPhoneAsync("7198004382");

    var patchBody = JsonSerializer.Serialize(new
    {
        sebtChldId = "999999999",
        gurdFstNm = "Nobody"
    });

    var result = await store.ApplyPatchAsync(patchBody);

    Assert.Contains("404", result);
    Assert.Contains("Not Found", result);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test src/SEBT.Portal.StatePlugins.CO.Tests/SEBT.Portal.StatePlugins.CO.Tests.csproj --filter "FullyQualifiedName~MockCbmsDataStoreTests"`
Expected: New tests FAIL — `ApplyPatchAsync` does not exist yet.

- [ ] **Step 3: Implement `ApplyPatchAsync`**

Add to `MockCbmsDataStore`:

```csharp
private static readonly string NotFoundResponse = JsonSerializer.Serialize(new
{
    apiName = "cbms-sebt-eapi-impl",
    correlationId = "mock",
    timestamp = DateTimeOffset.UtcNow.ToString("o"),
    errorDetails = new[] { new { code = "404", message = "Not Found" } }
});

private static readonly string SuccessResponse =
    JsonSerializer.Serialize(new { respCd = "00", respMsg = "Success" });

public async Task<string> ApplyPatchAsync(string requestBodyJson, CancellationToken cancellationToken = default)
{
    await EnsureSeededAsync(cancellationToken).ConfigureAwait(false);

    using var patchDoc = JsonDocument.Parse(requestBodyJson);
    var root = patchDoc.RootElement;

    var sebtChldId = root.TryGetProperty("sebtChldId", out var chldIdEl)
        ? chldIdEl.GetString()
        : null;

    if (string.IsNullOrEmpty(sebtChldId))
        return NotFoundResponse;

    // Search all known households for the matching student
    foreach (var phone in _knownPhones)
    {
        var cacheKey = CacheKeyPrefix + phone;
        var json = await _cache.GetOrCreateAsync(
            cacheKey,
            cancellationToken: cancellationToken,
            factory: (ct) => ValueTask.FromResult<string?>(null)).ConfigureAwait(false);

        if (json == null) continue;

        using var householdDoc = JsonDocument.Parse(json);
        var students = householdDoc.RootElement.GetProperty("stdntEnrollDtls");

        for (var i = 0; i < students.GetArrayLength(); i++)
        {
            var student = students[i];
            var studentChldId = student.TryGetProperty("sebtChldId", out var idEl)
                ? idEl.ValueKind == JsonValueKind.Number ? idEl.GetInt32().ToString() : idEl.GetString()
                : null;

            if (studentChldId != sebtChldId) continue;

            // Found the matching student — apply mutations
            var mutated = ApplyMutations(json, i, root);
            await _cache.SetAsync(cacheKey, mutated, cancellationToken: cancellationToken).ConfigureAwait(false);
            return SuccessResponse;
        }
    }

    return NotFoundResponse;
}

private static string ApplyMutations(string householdJson, int studentIndex, JsonElement patch)
{
    using var doc = JsonDocument.Parse(householdJson);
    using var ms = new MemoryStream();
    using (var writer = new Utf8JsonWriter(ms))
    {
        writer.WriteStartObject();
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Name == "stdntEnrollDtls")
            {
                writer.WritePropertyName("stdntEnrollDtls");
                writer.WriteStartArray();
                var idx = 0;
                foreach (var student in prop.Value.EnumerateArray())
                {
                    if (idx == studentIndex)
                    {
                        WriteStudentWithMutations(writer, student, patch);
                    }
                    else
                    {
                        student.WriteTo(writer);
                    }
                    idx++;
                }
                writer.WriteEndArray();
            }
            else
            {
                prop.WriteTo(writer);
            }
        }
        writer.WriteEndObject();
    }

    return System.Text.Encoding.UTF8.GetString(ms.ToArray());
}

private static void WriteStudentWithMutations(Utf8JsonWriter writer, JsonElement student, JsonElement patch)
{
    // Fields that can be directly overwritten on the student
    var directFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "gurdFstNm", "gurdLstNm", "gurdEmailAddr"
    };

    // Address fields come from the nested "addr" object in the patch
    var addressFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "addrLn1", "addrLn2", "cty", "staCd", "zip", "zip4"
    };

    // Build a map of overrides
    var overrides = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
    foreach (var field in directFields)
    {
        if (patch.TryGetProperty(field, out var val))
            overrides[field] = val;
    }
    if (patch.TryGetProperty("addr", out var addrEl))
    {
        foreach (var field in addressFields)
        {
            if (addrEl.TryGetProperty(field, out var val))
                overrides[field] = val;
        }
    }

    writer.WriteStartObject();
    foreach (var prop in student.EnumerateObject())
    {
        if (overrides.TryGetValue(prop.Name, out var overrideVal))
        {
            writer.WritePropertyName(prop.Name);
            overrideVal.WriteTo(writer);
        }
        else
        {
            prop.WriteTo(writer);
        }
    }
    writer.WriteEndObject();
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test src/SEBT.Portal.StatePlugins.CO.Tests/SEBT.Portal.StatePlugins.CO.Tests.csproj --filter "FullyQualifiedName~MockCbmsDataStoreTests"`
Expected: All 6 tests PASS.

- [ ] **Step 5: Commit**

```
feat: add PATCH mutation support to MockCbmsDataStore

ApplyPatchAsync finds a student by sebtChldId across all cached
households, applies address and guardian field mutations, and writes
back to cache. Returns 404 for unknown students.
```

### Task 8: Update `MockCbmsHttpHandler` to Delegate to Data Store

**Files:**
- Modify: `src/SEBT.Portal.StatePlugins.CO.CbmsApi/Mocks/MockCbmsHttpHandler.cs`

- [ ] **Step 1: Read the current `MockCbmsHttpHandler.cs`**

Read in full. Note the static `LoadMockJson` calls, `_return404ForGetAccountDetails` flag, and the `SendAsync` routing logic.

- [ ] **Step 2: Rewrite to delegate to `MockCbmsDataStore`**

Replace the contents of `MockCbmsHttpHandler.cs`:

```csharp
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace SEBT.Portal.StatePlugins.CO.CbmsApi.Mocks;

/// <summary>
/// HttpMessageHandler that returns mock CBMS API responses.
/// Phone lookup (get-account-details) and PATCH (update-std-dtls) are delegated
/// to <see cref="MockCbmsDataStore"/> for phone-indexed, cache-backed responses.
/// Token, ping, and check-enrollment return static embedded JSON.
/// </summary>
public sealed class MockCbmsHttpHandler : HttpMessageHandler
{
    private const string TokenPath = "ext-uat-c-cbms-oauth-app/token";
    private const string ApiBase = "ext-uat-c-cbms-cfa-eapi/api";
    private const string GetAccountDetailsPath = "sebt/get-account-details";
    private const string UpdateStdDtlsPath = "sebt/update-std-dtls";

    private readonly MockCbmsDataStore _dataStore;

    private static readonly string MockTokenResponse = LoadStaticMockJson("token.json");
    private static readonly string MockPingResponse = LoadStaticMockJson("ping.json");
    private static readonly string MockCheckEnrollmentResponse = LoadStaticMockJson("check-enrollment.json");

    public MockCbmsHttpHandler(MockCbmsDataStore dataStore)
    {
        ArgumentNullException.ThrowIfNull(dataStore);
        _dataStore = dataStore;
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) =>
        SendAsync(request, cancellationToken).GetAwaiter().GetResult();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri?.ToString() ?? "";
        var method = request.Method;

        if (url.Contains(TokenPath, StringComparison.OrdinalIgnoreCase) && method == HttpMethod.Post)
        {
            return JsonResponse(MockTokenResponse);
        }

        if (url.Contains($"{ApiBase}/ping", StringComparison.OrdinalIgnoreCase) && method == HttpMethod.Get)
        {
            return JsonResponse(MockPingResponse);
        }

        if (url.Contains($"{ApiBase}/sebt/check-enrollment", StringComparison.OrdinalIgnoreCase) && method == HttpMethod.Post)
        {
            return JsonResponse(MockCheckEnrollmentResponse);
        }

        if (url.Contains(GetAccountDetailsPath, StringComparison.OrdinalIgnoreCase) && method == HttpMethod.Post)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var phone = ExtractPhoneFromRequestBody(body);
            var response = await _dataStore.GetResponseForPhoneAsync(phone, cancellationToken).ConfigureAwait(false);
            return JsonResponse(response);
        }

        if (url.Contains(UpdateStdDtlsPath, StringComparison.OrdinalIgnoreCase) && method == HttpMethod.Patch)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var response = await _dataStore.ApplyPatchAsync(body, cancellationToken).ConfigureAwait(false);

            // Check if the response is a 404 error
            if (response.Contains("\"code\":\"404\""))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent(response, Encoding.UTF8, "application/json")
                };
            }

            return JsonResponse(response);
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent(
                $$"""{"error":"Mock handler: no response for {{method}} {{url}}"}""",
                Encoding.UTF8,
                "application/json")
        };
    }

    private static string ExtractPhoneFromRequestBody(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("phnNm", out var phoneEl)
            ? phoneEl.GetString() ?? ""
            : "";
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private static string LoadStaticMockJson(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"SEBT.Portal.StatePlugins.CO.CbmsApi.TestData.CbmsMocks.{fileName}";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new InvalidOperationException(
                $"Mock JSON resource not found: {resourceName}. Ensure TestData/CbmsMocks/{fileName} is set as EmbeddedResource.");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
```

- [ ] **Step 3: Build to verify compilation**

Run: `dotnet build src/SEBT.Portal.StatePlugins.CO.CbmsApi/SEBT.Portal.StatePlugins.CO.CbmsApi.csproj`
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```
feat: update MockCbmsHttpHandler to delegate to MockCbmsDataStore

Phone lookups and PATCH mutations are now handled by the cache-backed
data store. Static responses (token, ping, check-enrollment) unchanged.
The return404ForGetAccountDetails flag is removed — unknown phones
naturally return an empty success response.
```

### Task 9: Update `ColoradoSummerEbtCaseService` and Remove `Return404` Option

**Files:**
- Modify: `src/SEBT.Portal.StatePlugins.CO/ColoradoSummerEbtCaseService.cs`
- Modify: `src/SEBT.Portal.StatePlugins.CO/Cbms/CbmsOptionsHelper.cs`

- [ ] **Step 1: Add `HybridCache` constructor parameter**

Update `ColoradoSummerEbtCaseService.cs` constructor:

```csharp
using Microsoft.Extensions.Caching.Hybrid;
```

```csharp
private readonly HybridCache? _cache;

[ImportingConstructor]
public ColoradoSummerEbtCaseService(
    [Import] IConfiguration configuration,
    [Import] ILoggerFactory loggerFactory,
    HybridCache? cache = null)
{
    ArgumentNullException.ThrowIfNull(configuration);
    ArgumentNullException.ThrowIfNull(loggerFactory);

    _configuration = configuration;
    _logger = loggerFactory.CreateLogger<ColoradoSummerEbtCaseService>();
    _cache = cache;
}
```

- [ ] **Step 2: Update `GetOrCreateClient` to use data store in mock mode**

Replace the mock path in `GetOrCreateClient`:

```csharp
private CbmsSebtApiClient GetOrCreateClient(CbmsConnectionOptions options)
{
    lock (_clientCacheLock)
    {
        if (_cachedOptions == options && _cachedClient != null)
            return _cachedClient;

        HttpMessageHandler? handler = null;
        var clientId = options.ClientId;
        var clientSecret = options.ClientSecret;

        if (options.UseMockResponses)
        {
            if (_cache == null)
            {
                throw new InvalidOperationException(
                    "HybridCache must be registered in DI when Cbms:UseMockResponses is enabled. " +
                    "Ensure services.AddHybridCache() is called during service registration.");
            }

            clientId = "mock-client-id";
            clientSecret = "mock-client-secret";
            var dataStore = new MockCbmsDataStore(_cache);
            handler = new MockCbmsHttpHandler(dataStore);
        }

        _cachedClient = CbmsSebtApiClientFactory.Create(
            clientId,
            clientSecret,
            options.ApiBaseUrl,
            options.TokenEndpointUrl,
            handler);
        _cachedOptions = options;
        return _cachedClient;
    }
}
```

- [ ] **Step 3: Remove `Return404ForGetAccountDetails` from `CbmsOptionsHelper`**

Update `src/SEBT.Portal.StatePlugins.CO/Cbms/CbmsOptionsHelper.cs`:

Remove the `return404Raw`/`return404ForGetAccountDetails` lines and the parameter from the `CbmsConnectionOptions` record:

```csharp
internal static CbmsConnectionOptions GetCbmsOptions(IConfiguration? configuration)
{
    var clientId = configuration?["Cbms:ClientId"]
        ?? Environment.GetEnvironmentVariable("Cbms__ClientId")
        ?? string.Empty;
    var clientSecret = configuration?["Cbms:ClientSecret"]
        ?? Environment.GetEnvironmentVariable("Cbms__ClientSecret")
        ?? string.Empty;
    var apiBaseUrl = configuration?["Cbms:ApiBaseUrl"]
        ?? Environment.GetEnvironmentVariable("Cbms__ApiBaseUrl")
        ?? CbmsDefaults.SandboxApiBaseUrl;
    var tokenEndpointUrl = configuration?["Cbms:TokenEndpointUrl"]
        ?? Environment.GetEnvironmentVariable("Cbms__TokenEndpointUrl")
        ?? CbmsDefaults.SandboxTokenEndpointUrl;

    var useMockResponsesRaw = configuration?["Cbms:UseMockResponses"]
        ?? Environment.GetEnvironmentVariable("Cbms__UseMockResponses");
    var useMockResponses = useMockResponsesRaw?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

    return new CbmsConnectionOptions(clientId, clientSecret, apiBaseUrl, tokenEndpointUrl, useMockResponses);
}
```

```csharp
internal sealed record CbmsConnectionOptions(
    string ClientId,
    string ClientSecret,
    string ApiBaseUrl,
    string TokenEndpointUrl,
    bool UseMockResponses = false)
{
    public bool IsConfigured =>
        UseMockResponses || (!string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret));
}
```

- [ ] **Step 4: Build to verify compilation**

Run: `dotnet build src/SEBT.Portal.StatePlugins.CO/SEBT.Portal.StatePlugins.CO.csproj`
Expected: Build succeeds.

- [ ] **Step 5: Commit**

```
feat: wire HybridCache into ColoradoSummerEbtCaseService mock path

Mock mode now creates a MockCbmsDataStore with the injected HybridCache.
Throws InvalidOperationException if cache is null in mock mode.
Removes Return404ForGetAccountDetails — unknown phones naturally return
empty success via the data store.
```

### Task 10: Update Existing Tests

**Files:**
- Modify: `src/SEBT.Portal.StatePlugins.CO.Tests/ColoradoSummerEbtCaseServiceTests.cs`

- [ ] **Step 1: Read the current test file**

Read `ColoradoSummerEbtCaseServiceTests.cs` in full to understand which tests need updating.

- [ ] **Step 2: Update mock-mode tests to pass HybridCache**

The constructor now requires `HybridCache?`. Update `CreateCbmsConfiguration` tests that use `useMockResponses: true` to also create and pass a `HybridCache`.

Add helper:

```csharp
private static HybridCache CreateInMemoryHybridCache()
{
    var services = new ServiceCollection();
    services.AddHybridCache();
    var provider = services.BuildServiceProvider();
    return provider.GetRequiredService<HybridCache>();
}
```

Update the mock-mode test to use a phone from the manifest and pass cache:

```csharp
[Fact]
public async Task GetHouseholdByIdentifierAsync_with_Phone_returns_household_when_UseMockResponses_and_valid_phone()
{
    var cache = CreateInMemoryHybridCache();
    var service = new ColoradoSummerEbtCaseService(
        CreateCbmsConfiguration(useMockResponses: true), NullLoggerFactory.Instance, cache);
    var piiVisibility = new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true);

    // Use a phone number that exists in mock-manifest.json
    var result = await service.GetHouseholdByIdentifierAsync(
        HouseholdIdentifierType.Phone,
        "7198004382",
        piiVisibility,
        IdentityAssuranceLevel.None);

    Assert.NotNull(result);
    Assert.NotEmpty(result.SummerEbtCases);
}
```

- [ ] **Step 3: Remove or update the 404 test**

The `Return404ForGetAccountDetails` config option no longer exists. Replace the test with one that verifies unknown phones return null:

```csharp
[Fact]
public async Task GetHouseholdByIdentifierAsync_with_Phone_returns_null_for_unknown_phone_in_mock_mode()
{
    var cache = CreateInMemoryHybridCache();
    var service = new ColoradoSummerEbtCaseService(
        CreateCbmsConfiguration(useMockResponses: true), NullLoggerFactory.Instance, cache);
    var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

    var result = await service.GetHouseholdByIdentifierAsync(
        HouseholdIdentifierType.Phone,
        "9999999999",
        piiVisibility,
        IdentityAssuranceLevel.None);

    // Empty success response has no students → service returns null
    Assert.Null(result);
}
```

- [ ] **Step 4: Update non-mock tests**

Tests that don't use mock mode pass `null` for cache (the default). Verify they still work with the two-arg constructor by adding usings:

```csharp
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
```

Non-mock tests don't need changes since `cache` defaults to `null`.

- [ ] **Step 5: Run all CO connector tests**

Run: `dotnet test src/SEBT.Portal.StatePlugins.CO.Tests/SEBT.Portal.StatePlugins.CO.Tests.csproj`
Expected: All tests PASS.

- [ ] **Step 6: Commit**

```
test: update service tests for HybridCache and phone-indexed mocks

Mock-mode tests now pass HybridCache and use manifest phone numbers.
Return404ForGetAccountDetails test replaced with unknown-phone test.
```

### Task 11: Final Verification

- [ ] **Step 1: Run full CO connector test suite**

Run: `dotnet test` (from CO connector repo root)
Expected: All tests PASS.

- [ ] **Step 2: Build the plugin and verify it loads**

Run: `dotnet build src/SEBT.Portal.StatePlugins.CO/SEBT.Portal.StatePlugins.CO.csproj`

This should copy plugin DLLs to the main repo's `plugins-co/` directory (via the `CopyPlugins` target). Verify the main repo still starts:

Run: (from main repo) `dotnet build src/SEBT.Portal.Api/SEBT.Portal.Api.csproj`
Expected: Build succeeds.

- [ ] **Step 3: Commit any remaining fixes**
