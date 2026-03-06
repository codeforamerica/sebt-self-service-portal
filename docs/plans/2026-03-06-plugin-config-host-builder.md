# Deferred Plugin Loading for Testable Configuration

## Problem

`AddPlugins` runs inline in Program.cs, eagerly reading `builder.Configuration`
and loading assemblies via MEF during service registration. In ASP.NET Core's
minimal hosting model, `ConfigureHostBuilder.ConfigureServices` executes
callbacks immediately (not during `Build()`), so `WebApplicationFactory`'s
`ConfigureAppConfiguration` hook fires too late to influence plugin config.

Test factories work around this by setting process-global environment variables
in their constructors and restoring them in `Dispose`. This approach is fragile:

- **Global state mutation** — env vars are process-global. Even with
  save/restore and `[Collection]` serialization, one missed restore or parallel
  execution bug causes cross-test contamination.
- **Scaling concern** — every new config key (connection strings, API keys,
  feature flags) requires another `SetEnvVar` call and its restore counterpart.
- **Pattern deviation** — standard .NET integration tests use
  `ConfigureAppConfiguration` or `ConfigureServices` to override config.
  Our tests cannot follow that pattern, making them unfamiliar to .NET
  developers joining the project.

### Why `IHostBuilder.ConfigureServices` doesn't help

The initial approach was to move `AddPlugins` from inline execution to a
`builder.Host.ConfigureServices` callback, expecting it to run during `Build()`
after WAF's `ConfigureAppConfiguration`. However, in the minimal hosting model,
`builder.Host` returns a `ConfigureHostBuilder` that runs callbacks immediately
("Run these immediately so that they are observable by the imperative code" —
ASP.NET Core source). This means the timing is identical to inline execution.

## Solution: Deferred plugin loading via factory delegates

Instead of loading assemblies eagerly during service registration, register
factory delegates that load assemblies lazily at DI resolution time. At that
point, `IConfiguration` from the service provider is fully assembled and
includes WAF's `ConfigureAppConfiguration` additions.

### Current flow (eager)

```
AddPlugins called → reads IConfiguration → loads assemblies →
creates MEF exports → registers instances in DI
```

### Proposed flow (deferred)

```
AddPlugins called → registers PluginLoader + factory delegates in DI
                    (no config reading, no assembly loading)

First service resolution → factory calls PluginLoader.GetExport<T>()
                         → PluginLoader reads IConfiguration (now complete)
                         → loads assemblies, creates MEF container
                         → returns export (or default fallback)
```

## Production code changes

### New class: `PluginLoader`

A singleton that lazily loads assemblies and discovers MEF exports on first
access. Takes `IConfiguration` via constructor injection from DI — at
resolution time, this includes all registered config sources.

```csharp
internal sealed class PluginLoader
{
    private readonly Lazy<IReadOnlyDictionary<Type, object>> _exports;

    public PluginLoader(IConfiguration configuration)
    {
        _exports = new Lazy<IReadOnlyDictionary<Type, object>>(
            () => LoadExports(configuration));
    }

    public T? GetExport<T>() where T : class
    {
        _exports.Value.TryGetValue(typeof(T), out var export);
        return export as T;
    }

    private static IReadOnlyDictionary<Type, object> LoadExports(
        IConfiguration configuration)
    {
        // Existing MEF logic from current RegisterPlugins:
        // read PluginAssemblyPaths, build conventions, load assemblies,
        // discover exports, map each to its service interface
    }
}
```

### Modified `AddPlugins`

Changes from `IHostBuilder` extension back to `IServiceCollection` extension.
No longer takes `IConfiguration` — config is read at resolution time by
`PluginLoader`. Registers factory delegates for each known plugin interface.

```csharp
public static IServiceCollection AddPlugins(this IServiceCollection services)
{
    services.AddSingleton<PluginLoader>();

    services.AddSingleton<IStateAuthenticationService>(sp =>
        sp.GetRequiredService<PluginLoader>()
            .GetExport<IStateAuthenticationService>()
        ?? new DefaultIStateAuthenticationService());

    services.AddSingleton<IEnrollmentCheckService>(sp =>
        sp.GetRequiredService<PluginLoader>()
            .GetExport<IEnrollmentCheckService>()
        ?? new DefaultEnrollmentCheckService());

    return services;
}
```

Notes:
- `IStateMetadataService` is only referenced in MEF conventions, never resolved
  from DI. No factory delegate needed.
- `ISummerEbtCaseService` has no default. Its factory delegate returns null or
  the plugin implementation. Tests that need it mock it via `ConfigureServices`.
- `TryAddSingleton` is no longer needed — the factory delegate itself handles
  the fallback logic.

### `Program.cs`

```csharp
// Before:
builder.Host.AddPlugins();

// After:
builder.Services.AddPlugins();
```

## Test factory changes

### `PortalWebApplicationFactory`

All env var machinery deleted. Config via `ConfigureAppConfiguration`, mock
plugin stubs via `ConfigureServices` (last registration wins over factory
delegates from `AddPlugins`):

```csharp
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
        // Replace DB, migrator, seeder (same as today)
        // Mock plugin stubs override factory delegates from AddPlugins
        services.AddSingleton(Substitute.For<ISummerEbtCaseService>());
        services.AddSingleton(Substitute.For<IEnrollmentCheckService>());
    });
}
```

No constructor. No `Dispose` override.

### `PluginIntegrationWebApplicationFactory`

Same pattern. Constructor stores parameters; `ConfigureAppConfiguration`
applies them. Real plugins load via factory delegates — no mock overrides
needed for plugin interfaces.

```csharp
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
            foreach (var (key, value) in _configOverrides)
                overrides[key] = value;

        config.AddInMemoryCollection(overrides);
    });

    builder.ConfigureServices(services =>
    {
        // Replace DB, migrator, seeder
        // ISummerEbtCaseService mock (TryAddSingleton — no-op if real plugin
        // registered it via factory delegate)
    });
}
```

No `Dispose` override. No env var save/restore.

## Test class changes

Config keys switch from `__` (env var convention) to `:` (standard .NET).
`[Collection("PluginIntegration")]` removed — no global state mutation means
parallel execution is safe. `PluginIntegrationCollection.cs` deleted.

## Files summary

| File | Action |
|------|--------|
| `src/.../Composition/PluginLoader.cs` | Create |
| `src/.../Composition/ServiceCollectionPluginExtensions.cs` | Rewrite |
| `src/.../Program.cs` | Modify |
| `test/.../Integration/PortalWebApplicationFactory.cs` | Rewrite |
| `test/.../PluginIntegration/PluginIntegrationWebApplicationFactory.cs` | Rewrite |
| `test/.../PluginIntegration/DcEnrollmentCheckIntegrationTests.cs` | Modify |
| `test/.../PluginIntegration/CoEnrollmentCheckIntegrationTests.cs` | Modify |
| `test/.../PluginIntegration/DefaultEnrollmentCheckIntegrationTests.cs` | Modify |
| `test/.../PluginIntegration/PluginIntegrationCollection.cs` | Delete |

## Risks and verification

**Startup validation:** Assembly load failures currently crash at startup. With
deferred loading, they surface on first service resolution. This is acceptable
since the app still fails before serving requests (DI resolves singletons
eagerly during the first request pipeline). If fail-fast at startup is required
later, an `IHostedService` can eagerly resolve `PluginLoader`.

**IConfiguration identity:** The design assumes `IConfiguration` from DI at
resolution time includes WAF's `ConfigureAppConfiguration` additions. This
should be verified with a spike test in Task 1.

**Task 1 revert:** The prior commit (f703121) moved `AddPlugins` to an
`IHostBuilder` extension based on incorrect timing assumptions about
`ConfigureHostBuilder`. It should be reverted as the first task.
