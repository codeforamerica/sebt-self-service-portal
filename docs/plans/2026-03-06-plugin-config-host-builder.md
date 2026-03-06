# Move Plugin Loading to Host Builder Pipeline

## Problem

`AddPlugins` runs inline in Program.cs (line 71), reading `builder.Configuration`
directly. `WebApplicationFactory`'s `ConfigureWebHost` fires *after* Program.cs
top-level statements execute, so tests cannot use the standard
`ConfigureAppConfiguration` hook to override plugin paths or connection strings.

Instead, test factories set process-global environment variables in their
constructors and restore them in `Dispose`. This approach is fragile:

- **Global state mutation** — env vars are process-global. Even with
  save/restore and `[Collection]` serialization, one missed restore or parallel
  execution bug causes cross-test contamination.
- **Scaling concern** — every new config key (connection strings, API keys,
  feature flags) requires another `SetEnvVar` call and its restore counterpart.
- **Pattern deviation** — standard .NET integration tests use
  `ConfigureAppConfiguration` or `ConfigureServices` to override config.
  Our tests cannot follow that pattern, making them unfamiliar to .NET
  developers joining the project.

## Solution

Change `AddPlugins` from an `IServiceCollection` extension method (called inline
in Program.cs) to an `IHostBuilder` extension method that registers a
`ConfigureServices` callback. The callback executes during `Build()`, *after*
WAF's `ConfigureAppConfiguration` has already modified the configuration.

### Host builder pipeline order during `Build()`

1. `ConfigureAppConfiguration` callbacks — WAF test overrides go here
2. `ConfigureServices` callbacks — `AddPlugins` logic runs here, reads the
   fully-assembled configuration

This means tests use the standard in-memory collection pattern with zero
environment variables and zero save/restore lifecycle.

## Production code changes

### `ServiceCollectionPluginExtensions.cs`

The public API changes from extending `IServiceCollection` to extending
`IHostBuilder`. This encapsulates the callback timing so callers cannot
accidentally call plugin loading at the wrong point in the pipeline.

```csharp
// New public API
public static IHostBuilder AddPlugins(this IHostBuilder hostBuilder)
{
    hostBuilder.ConfigureServices((context, services) =>
        services.RegisterPlugins(context.Configuration));
    return hostBuilder;
}

// Existing logic, renamed to private
private static void RegisterPlugins(
    this IServiceCollection services, IConfiguration configuration)
{
    // All existing AddPlugins code unchanged:
    // TryAddSingleton defaults, MEF assembly loading,
    // plugin discovery, service registration
}
```

### `Program.cs`

One-line call site change:

```csharp
// Before:
builder.Services.AddPlugins(builder.Configuration);

// After:
builder.Host.AddPlugins();
```

No other production files change.

## Test factory changes

### `PortalWebApplicationFactory`

All env var machinery (`_originalEnvVars`, `SetEnvVar`, constructor env var
calls, `Dispose` restore logic) is deleted. Configuration moves entirely to
`ConfigureAppConfiguration`:

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
        // Replace DB, migrator, seeder, mock plugin stubs — unchanged
    });
}
```

No constructor. No `Dispose` override.

### `PluginIntegrationWebApplicationFactory`

Same pattern. Constructor stores parameters; `ConfigureAppConfiguration`
applies them:

```csharp
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
            foreach (var (key, value) in _configOverrides)
                overrides[key] = value;

        config.AddInMemoryCollection(overrides);
    });

    builder.ConfigureServices(services =>
    {
        // Replace DB, migrator, seeder,
        // TryAddSingleton ISummerEbtCaseService — unchanged
    });
}
```

No `Dispose` override. No env var save/restore.

Config keys use `:` separator (standard .NET hierarchical config) instead of
`__` (env var convention).

## Test class changes

### Config key format

Test classes that pass `configOverrides` switch from `__` to `:` separator:

```csharp
// Before:
["DCConnector__ConnectionString"] = dcDatabase.ConnectionString

// After:
["DCConnector:ConnectionString"] = dcDatabase.ConnectionString
```

### Remove `[Collection("PluginIntegration")]`

The collection existed solely to serialize tests that mutated process-global
env vars. With no global state mutation, each WAF creates its own isolated
test server and DI container. Tests are safe to run in parallel.

Remove `[Collection("PluginIntegration")]` from:
- `DcEnrollmentCheckIntegrationTests`
- `CoEnrollmentCheckIntegrationTests`
- `DefaultEnrollmentCheckIntegrationTests`

Delete `PluginIntegrationCollection.cs` entirely.

## Files summary

| File | Action |
|------|--------|
| `src/.../Composition/ServiceCollectionPluginExtensions.cs` | Modify |
| `src/.../Program.cs` | Modify (one line) |
| `test/.../Integration/PortalWebApplicationFactory.cs` | Modify |
| `test/.../PluginIntegration/PluginIntegrationWebApplicationFactory.cs` | Modify |
| `test/.../PluginIntegration/DcEnrollmentCheckIntegrationTests.cs` | Modify |
| `test/.../PluginIntegration/CoEnrollmentCheckIntegrationTests.cs` | Modify |
| `test/.../PluginIntegration/DefaultEnrollmentCheckIntegrationTests.cs` | Modify |
| `test/.../PluginIntegration/PluginIntegrationCollection.cs` | Delete |

## Risks and verification

**Primary risk:** The design assumes `ConfigureAppConfiguration` callbacks fire
before `ConfigureServices` callbacks during `Build()`. This is the documented
.NET host builder pipeline order, but should be verified with a spike test:
add a temporary log in `RegisterPlugins` that prints the value of
`PluginAssemblyPaths:0` and confirm it reflects the test factory's override.

**Unchanged:** `PluginPathResolver`, `DcSourceDatabaseFixture`, all plugin
connector repos, `HasPluginDlls` skip logic.
