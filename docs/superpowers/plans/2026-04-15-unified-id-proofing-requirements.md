# Unified IdProofingRequirements Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace two separate IAL configuration systems with a single unified `IdProofingRequirements` section using resource+action keys, eliminating a class of security misconfiguration.

**Architecture:** Unified config section parsed by `IConfigureOptions<T>`, validated by `IValidateOptions<T>` at startup and on config changes, consumed via two interfaces (`IIdProofingService` for authorization gates, `IPiiVisibilityService` for PII filtering) backed by a single singleton implementation with safe hot-reload via `IOptionsMonitor<T>`.

**Tech Stack:** .NET 10, ASP.NET Core Options pattern, xUnit, NSubstitute

**Spec:** `docs/superpowers/specs/2026-04-15-unified-id-proofing-requirements-design.md`

---

## File Map

### New files (Core layer — models and interfaces)

| File | Responsibility |
|------|---------------|
| `src/SEBT.Portal.Core/Models/Auth/ProtectedResource.cs` | Enum: Address, Email, Phone, Household, Card |
| `src/SEBT.Portal.Core/Models/Auth/ProtectedAction.cs` | Enum: View, Write |
| `src/SEBT.Portal.Core/Models/Auth/IdProofingDecision.cs` | Record struct returned by `Evaluate()` |
| `src/SEBT.Portal.Core/Models/Auth/IdProofingKeys.cs` | Static helper: enum → config key string mapping |
| `src/SEBT.Portal.Core/Services/IIdProofingService.cs` | Interface: `Evaluate(resource, action, ial, cases)` |
| `src/SEBT.Portal.Core/Services/IPiiVisibilityService.cs` | Interface: `GetVisibility(ial)` |

### New files (Infrastructure layer — settings, binding, validation, service)

| File | Responsibility |
|------|---------------|
| `src/SEBT.Portal.Core/AppSettings/IalRequirement.cs` | Polymorphic requirement: uniform or per-case-type |
| `src/SEBT.Portal.Core/AppSettings/IdProofingRequirementsSettings.cs` | _(replace existing)_ Unified settings class with `Dictionary<string, IalRequirement>` |
| `src/SEBT.Portal.Infrastructure/Configuration/ConfigureIdProofingRequirements.cs` | `IConfigureOptions<T>`: custom parser for polymorphic config |
| `src/SEBT.Portal.Infrastructure/Configuration/IdProofingRequirementsCoherenceValidator.cs` | `IValidateOptions<T>`: write >= view, step-up consistency |
| `src/SEBT.Portal.Infrastructure/Services/IdProofingService.cs` | Singleton: implements `IIdProofingService` + `IPiiVisibilityService` |

### New test files

| File | Responsibility |
|------|---------------|
| `test/SEBT.Portal.Tests/Unit/Models/IalRequirementTests.cs` | IalRequirement: uniform, per-case-type, highest-wins, defaults |
| `test/SEBT.Portal.Tests/Unit/Configuration/ConfigureIdProofingRequirementsTests.cs` | Config binding: simple, object, mixed, unknown key warning |
| `test/SEBT.Portal.Tests/Unit/Configuration/IdProofingRequirementsCoherenceValidatorTests.cs` | Validation: coherence, step-up consistency |
| `test/SEBT.Portal.Tests/Unit/Services/IdProofingServiceTests.cs` | Service: Evaluate, GetVisibility |

### Modified files

| File | Change |
|------|--------|
| `src/SEBT.Portal.Api/appsettings.json` | Add `address+write`, `household+view`, `card+write` keys |
| `src/SEBT.Portal.Infrastructure/Dependencies.cs` | Replace old DI registrations with new ones |
| `src/SEBT.Portal.UseCases/Household/GetHouseholdData/GetHouseholdDataQueryHandler.cs` | Use `IIdProofingService` + `IPiiVisibilityService` |
| `src/SEBT.Portal.UseCases/Household/UpdateAddress/UpdateAddressCommandHandler.cs` | Use `IIdProofingService` + `IPiiVisibilityService` |
| `src/SEBT.Portal.UseCases/Household/RequestCardReplacement/RequestCardReplacementCommandHandler.cs` | Use `IIdProofingService` |
| `test/SEBT.Portal.Tests/Unit/UseCases/Household/GetHouseholdDataQueryHandlerTests.cs` | Swap mocks |
| `test/SEBT.Portal.Tests/Unit/UseCases/Household/UpdateAddressCommandHandlerTests.cs` | Swap mocks |
| `test/SEBT.Portal.Tests/Unit/UseCases/Household/RequestCardReplacementCommandHandlerTests.cs` | Swap mocks |
| `test/SEBT.Portal.Tests/Unit/UseCases/DependenciesTests.cs` | Update stubbed services |
| `test/SEBT.Portal.Tests/Unit/Security/MinimumIalFailOpenTests.cs` | Adapt to new unified interface |

### Deleted files

| File | Reason |
|------|--------|
| `src/SEBT.Portal.Core/AppSettings/MinimumIalSettings.cs` | Replaced by unified `IdProofingRequirementsSettings` |
| `src/SEBT.Portal.Core/Services/IMinimumIalService.cs` | Replaced by `IIdProofingService` |
| `src/SEBT.Portal.Core/Services/IIdProofingRequirementsService.cs` | Replaced by `IPiiVisibilityService` |
| `src/SEBT.Portal.Infrastructure/Services/MinimumIalService.cs` | Replaced by `IdProofingService` |
| `src/SEBT.Portal.Infrastructure/Services/IdProofingRequirementsService.cs` | Replaced by `IdProofingService` |
| `src/SEBT.Portal.Infrastructure/Configuration/MinimumIalSettingsValidator.cs` | Replaced by coherence validator |
| `src/SEBT.Portal.Infrastructure/Configuration/IdProofingRequirementsSettingsValidator.cs` | Replaced by coherence validator |
| `test/SEBT.Portal.Tests/Unit/Services/MinimumIalServiceTests.cs` | Logic covered by `IalRequirementTests` + `IdProofingServiceTests` |
| `test/SEBT.Portal.Tests/Unit/Services/IdProofingRequirementsServiceTests.cs` | Logic covered by `IdProofingServiceTests` |
| `test/SEBT.Portal.Tests/Unit/Configuration/IdProofingRequirementsSettingsValidatorTests.cs` | Replaced by coherence validator tests |

---

## Task 1: Core Enums, Value Types, and Interfaces

These are pure types with no dependencies — the foundation everything else builds on.

**Files:**
- Create: `src/SEBT.Portal.Core/Models/Auth/ProtectedResource.cs`
- Create: `src/SEBT.Portal.Core/Models/Auth/ProtectedAction.cs`
- Create: `src/SEBT.Portal.Core/Models/Auth/IdProofingDecision.cs`
- Create: `src/SEBT.Portal.Core/Models/Auth/IdProofingKeys.cs`
- Create: `src/SEBT.Portal.Core/Services/IIdProofingService.cs`
- Create: `src/SEBT.Portal.Core/Services/IPiiVisibilityService.cs`

- [ ] **Step 1: Create ProtectedResource enum**

```csharp
// src/SEBT.Portal.Core/Models/Auth/ProtectedResource.cs
namespace SEBT.Portal.Core.Models.Auth;

/// <summary>
/// A resource protected by identity assurance requirements.
/// Each resource may have view and/or write requirements configured.
/// </summary>
public enum ProtectedResource
{
    Address,
    Email,
    Phone,
    Household,
    Card
}
```

- [ ] **Step 2: Create ProtectedAction enum**

```csharp
// src/SEBT.Portal.Core/Models/Auth/ProtectedAction.cs
namespace SEBT.Portal.Core.Models.Auth;

/// <summary>
/// An action on a protected resource that requires identity assurance.
/// </summary>
public enum ProtectedAction
{
    View,
    Write
}
```

- [ ] **Step 3: Create IdProofingDecision record struct**

```csharp
// src/SEBT.Portal.Core/Models/Auth/IdProofingDecision.cs
namespace SEBT.Portal.Core.Models.Auth;

/// <summary>
/// Result of an identity proofing evaluation: whether the user is allowed
/// and what level would be required if they are not.
/// </summary>
public readonly record struct IdProofingDecision(
    bool IsAllowed,
    UserIalLevel RequiredLevel);
```

- [ ] **Step 4: Create IdProofingKeys helper**

```csharp
// src/SEBT.Portal.Core/Models/Auth/IdProofingKeys.cs
namespace SEBT.Portal.Core.Models.Auth;

/// <summary>
/// Maps <see cref="ProtectedResource"/>+<see cref="ProtectedAction"/> enum pairs
/// to the config key strings used in IdProofingRequirements.
/// </summary>
public static class IdProofingKeys
{
    public const string AddressView = "address+view";
    public const string AddressWrite = "address+write";
    public const string EmailView = "email+view";
    public const string PhoneView = "phone+view";
    public const string HouseholdView = "household+view";
    public const string CardWrite = "card+write";

    /// <summary>
    /// Converts a resource+action enum pair to its config key string.
    /// </summary>
    public static string ToConfigKey(ProtectedResource resource, ProtectedAction action)
        => $"{resource.ToString().ToLowerInvariant()}+{action.ToString().ToLowerInvariant()}";

    /// <summary>
    /// Returns all valid config key strings, derived from the enum values.
    /// Used by the config binder to warn on unrecognized keys.
    /// </summary>
    public static IReadOnlySet<string> AllValidKeys { get; } = BuildAllValidKeys();

    private static HashSet<string> BuildAllValidKeys()
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var resource in Enum.GetValues<ProtectedResource>())
        foreach (var action in Enum.GetValues<ProtectedAction>())
            keys.Add(ToConfigKey(resource, action));
        return keys;
    }
}
```

- [ ] **Step 5: Create IIdProofingService interface**

```csharp
// src/SEBT.Portal.Core/Services/IIdProofingService.cs
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Core.Services;

/// <summary>
/// Authorization gate: evaluates whether a user meets the IAL requirement
/// for a resource+action, resolved against their household case types.
/// </summary>
public interface IIdProofingService
{
    /// <summary>
    /// Evaluates whether the user meets the IAL requirement for the
    /// requested resource+action, resolved against their case types.
    /// </summary>
    IdProofingDecision Evaluate(
        ProtectedResource resource,
        ProtectedAction action,
        UserIalLevel userIalLevel,
        IReadOnlyList<SummerEbtCase> cases);
}
```

- [ ] **Step 6: Create IPiiVisibilityService interface**

```csharp
// src/SEBT.Portal.Core/Services/IPiiVisibilityService.cs
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;

namespace SEBT.Portal.Core.Services;

/// <summary>
/// Determines which PII fields a user can see based on their IAL level
/// and the configured view requirements.
/// Used by the repository layer for query filtering.
/// </summary>
public interface IPiiVisibilityService
{
    PiiVisibility GetVisibility(UserIalLevel userIalLevel);
}
```

- [ ] **Step 7: Verify build**

Run: `dotnet build src/SEBT.Portal.Core/SEBT.Portal.Core.csproj`
Expected: Build succeeded, 0 errors

- [ ] **Step 8: Commit**

```
feat: add core types and interfaces for unified IdProofingRequirements
```

---

## Task 2: IalRequirement Value Type (TDD)

The polymorphic requirement type that handles both uniform and per-case-type configurations.

**Files:**
- Create: `src/SEBT.Portal.Core/AppSettings/IalRequirement.cs`
- Create: `test/SEBT.Portal.Tests/Unit/Models/IalRequirementTests.cs`

- [ ] **Step 1: Write failing tests for IalRequirement**

```csharp
// test/SEBT.Portal.Tests/Unit/Models/IalRequirementTests.cs
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Tests.Unit.Models;

public class IalRequirementTests
{
    private static SummerEbtCase ApplicationCase() =>
        new()
        {
            ChildFirstName = "Test",
            ChildLastName = "Child",
            IsStreamlineCertified = false,
            IsCoLoaded = false
        };

    private static SummerEbtCase CoLoadedStreamlineCase() =>
        new()
        {
            ChildFirstName = "Test",
            ChildLastName = "Child",
            IsStreamlineCertified = true,
            IsCoLoaded = true
        };

    private static SummerEbtCase NonCoLoadedStreamlineCase() =>
        new()
        {
            ChildFirstName = "Test",
            ChildLastName = "Child",
            IsStreamlineCertified = true,
            IsCoLoaded = false
        };

    // --- Uniform requirement ---

    [Theory]
    [InlineData(IalLevel.IAL1, UserIalLevel.IAL1)]
    [InlineData(IalLevel.IAL1plus, UserIalLevel.IAL1plus)]
    [InlineData(IalLevel.IAL2, UserIalLevel.IAL2)]
    public void Uniform_Resolve_ReturnsLevel_RegardlessOfCases(
        IalLevel level,
        UserIalLevel expected)
    {
        var req = IalRequirement.Uniform(level);
        var result = req.Resolve([ApplicationCase(), CoLoadedStreamlineCase()]);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Uniform_Resolve_ReturnsLevel_WhenNoCases()
    {
        var req = IalRequirement.Uniform(IalLevel.IAL1plus);
        var result = req.Resolve([]);
        Assert.Equal(UserIalLevel.IAL1plus, result);
    }

    [Fact]
    public void Uniform_AllLevels_ReturnsSingleLevel()
    {
        var req = IalRequirement.Uniform(IalLevel.IAL1plus);
        Assert.Equal([IalLevel.IAL1plus], req.AllLevels().ToList());
    }

    // --- Per-case-type requirement ---

    [Fact]
    public void PerCaseType_Resolve_ReturnsapplicationLevel()
    {
        var req = IalRequirement.PerCaseType(new Dictionary<string, IalLevel>
        {
            ["application"] = IalLevel.IAL1,
            ["coloadedStreamline"] = IalLevel.IAL1,
            ["streamline"] = IalLevel.IAL1plus
        });

        var result = req.Resolve([ApplicationCase()]);
        Assert.Equal(UserIalLevel.IAL1, result);
    }

    [Fact]
    public void PerCaseType_Resolve_HighestWins_WhenMixedCases()
    {
        var req = IalRequirement.PerCaseType(new Dictionary<string, IalLevel>
        {
            ["application"] = IalLevel.IAL1,
            ["coloadedStreamline"] = IalLevel.IAL1,
            ["streamline"] = IalLevel.IAL1plus
        });

        var result = req.Resolve([CoLoadedStreamlineCase(), NonCoLoadedStreamlineCase()]);
        Assert.Equal(UserIalLevel.IAL1plus, result);
    }

    [Fact]
    public void PerCaseType_Resolve_ReturnsIal1_WhenNoCases()
    {
        var req = IalRequirement.PerCaseType(new Dictionary<string, IalLevel>
        {
            ["application"] = IalLevel.IAL1plus,
            ["coloadedStreamline"] = IalLevel.IAL1plus,
            ["streamline"] = IalLevel.IAL1plus
        });

        var result = req.Resolve([]);
        Assert.Equal(UserIalLevel.IAL1, result);
    }

    [Fact]
    public void PerCaseType_AllLevels_ReturnsAllConfiguredLevels()
    {
        var req = IalRequirement.PerCaseType(new Dictionary<string, IalLevel>
        {
            ["application"] = IalLevel.IAL1,
            ["streamline"] = IalLevel.IAL1plus
        });

        var levels = req.AllLevels().OrderBy(l => l).ToList();
        Assert.Equal([IalLevel.IAL1, IalLevel.IAL1plus], levels);
    }

    // --- Default requirement ---

    [Fact]
    public void Default_Resolve_ReturnsIal1plus()
    {
        var req = IalRequirement.Default();
        var result = req.Resolve([ApplicationCase()]);
        Assert.Equal(UserIalLevel.IAL1plus, result);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~IalRequirementTests" --verbosity normal`
Expected: FAIL — `IalRequirement` does not exist yet

- [ ] **Step 3: Implement IalRequirement**

```csharp
// src/SEBT.Portal.Core/AppSettings/IalRequirement.cs
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// An IAL requirement that is either uniform (same level for all case types)
/// or per-case-type (different levels depending on how cases were loaded).
/// </summary>
public class IalRequirement
{
    private readonly IalLevel? _uniform;
    private readonly Dictionary<string, IalLevel>? _perCaseType;

    private IalRequirement(IalLevel? uniform, Dictionary<string, IalLevel>? perCaseType)
    {
        _uniform = uniform;
        _perCaseType = perCaseType;
    }

    /// <summary>Creates a requirement with the same level for all case types.</summary>
    public static IalRequirement Uniform(IalLevel level) => new(level, null);

    /// <summary>Creates a requirement with per-case-type levels. "Highest wins" on resolve.</summary>
    public static IalRequirement PerCaseType(Dictionary<string, IalLevel> levels) => new(null, levels);

    /// <summary>Creates a default requirement of IAL1plus (fail-safe).</summary>
    public static IalRequirement Default() => Uniform(IalLevel.IAL1plus);

    /// <summary>
    /// Resolves the required IAL level. For uniform requirements, returns the
    /// level directly. For per-case-type, applies "highest wins" across cases.
    /// Returns <see cref="UserIalLevel.IAL1"/> when no cases are provided.
    /// </summary>
    public UserIalLevel Resolve(IReadOnlyList<SummerEbtCase> cases)
    {
        if (_uniform.HasValue)
            return ToUserIalLevel(_uniform.Value);

        if (_perCaseType is null || cases.Count == 0)
            return UserIalLevel.IAL1;

        var highest = cases.Max(c => ClassifyCase(c, _perCaseType));
        return ToUserIalLevel(highest);
    }

    /// <summary>Returns all configured IAL levels (for validation comparisons).</summary>
    public IEnumerable<IalLevel> AllLevels()
    {
        if (_uniform.HasValue)
            return [_uniform.Value];
        return _perCaseType?.Values ?? [];
    }

    private static IalLevel ClassifyCase(SummerEbtCase c, Dictionary<string, IalLevel> levels)
    {
        string key;
        if (!c.IsStreamlineCertified)
            key = "application";
        else
            key = c.IsCoLoaded ? "coloadedStreamline" : "streamline";

        return levels.TryGetValue(key, out var level) ? level : IalLevel.IAL1plus;
    }

    private static UserIalLevel ToUserIalLevel(IalLevel level)
    {
        return level switch
        {
            IalLevel.IAL1 => UserIalLevel.IAL1,
            IalLevel.IAL1plus => UserIalLevel.IAL1plus,
            IalLevel.IAL2 => UserIalLevel.IAL2,
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown IalLevel value")
        };
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~IalRequirementTests" --verbosity normal`
Expected: All pass

- [ ] **Step 5: Commit**

```
feat: add IalRequirement value type with uniform and per-case-type resolution
```

---

## Task 3: Unified Settings Class and Config Binder (TDD)

Replace the old `IdProofingRequirementsSettings` with the unified version and add the custom `IConfigureOptions<T>` binder.

**Files:**
- Modify: `src/SEBT.Portal.Core/AppSettings/IdProofingRequirementsSettings.cs`
- Create: `src/SEBT.Portal.Infrastructure/Configuration/ConfigureIdProofingRequirements.cs`
- Create: `test/SEBT.Portal.Tests/Unit/Configuration/ConfigureIdProofingRequirementsTests.cs`

- [ ] **Step 1: Write failing tests for config binding**

```csharp
// test/SEBT.Portal.Tests/Unit/Configuration/ConfigureIdProofingRequirementsTests.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Infrastructure.Configuration;

namespace SEBT.Portal.Tests.Unit.Configuration;

public class ConfigureIdProofingRequirementsTests
{
    private static (ConfigureIdProofingRequirements binder, IdProofingRequirementsSettings settings)
        BindConfig(Dictionary<string, string?> configValues)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var logger = NullLogger<ConfigureIdProofingRequirements>.Instance;
        var binder = new ConfigureIdProofingRequirements(config, logger);
        var settings = new IdProofingRequirementsSettings();
        binder.Configure(settings);
        return (binder, settings);
    }

    [Fact]
    public void Configure_SimpleStringValue_CreatesUniformRequirement()
    {
        var (_, settings) = BindConfig(new Dictionary<string, string?>
        {
            ["IdProofingRequirements:address+view"] = "IAL1plus"
        });

        var req = settings.Get(ProtectedResource.Address, ProtectedAction.View);
        Assert.Equal(UserIalLevel.IAL1plus, req.Resolve([]));
    }

    [Fact]
    public void Configure_ObjectValue_CreatesPerCaseTypeRequirement()
    {
        var (_, settings) = BindConfig(new Dictionary<string, string?>
        {
            ["IdProofingRequirements:household+view:application"] = "IAL1",
            ["IdProofingRequirements:household+view:coloadedStreamline"] = "IAL1",
            ["IdProofingRequirements:household+view:streamline"] = "IAL1plus"
        });

        var req = settings.Get(ProtectedResource.Household, ProtectedAction.View);
        var nonCoLoaded = new SummerEbtCase
        {
            ChildFirstName = "Test",
            ChildLastName = "Child",
            IsStreamlineCertified = true,
            IsCoLoaded = false
        };
        Assert.Equal(UserIalLevel.IAL1plus, req.Resolve([nonCoLoaded]));
    }

    [Fact]
    public void Configure_MissingKey_ReturnsDefault_Ial1plus()
    {
        var (_, settings) = BindConfig(new Dictionary<string, string?>
        {
            ["IdProofingRequirements:address+view"] = "IAL1plus"
        });

        // card+write not in config — should get default (IAL1plus)
        var req = settings.Get(ProtectedResource.Card, ProtectedAction.Write);
        Assert.Equal(UserIalLevel.IAL1plus, req.Resolve([]));
    }

    [Fact]
    public void Configure_CaseInsensitiveEnumParsing()
    {
        var (_, settings) = BindConfig(new Dictionary<string, string?>
        {
            ["IdProofingRequirements:address+view"] = "ial1plus"
        });

        var req = settings.Get(ProtectedResource.Address, ProtectedAction.View);
        Assert.Equal(UserIalLevel.IAL1plus, req.Resolve([]));
    }

    [Fact]
    public void Configure_UnknownKey_LogsWarning()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IdProofingRequirements:typo+view"] = "IAL1plus"
            })
            .Build();

        var logger = Substitute.For<ILogger<ConfigureIdProofingRequirements>>();
        var binder = new ConfigureIdProofingRequirements(config, logger);
        var settings = new IdProofingRequirementsSettings();

        binder.Configure(settings);

        logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("typo+view")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~ConfigureIdProofingRequirementsTests" --verbosity normal`
Expected: FAIL — new classes do not exist yet

- [ ] **Step 3: Replace IdProofingRequirementsSettings**

Read the existing file first, then replace its contents entirely:

```csharp
// src/SEBT.Portal.Core/AppSettings/IdProofingRequirementsSettings.cs
using SEBT.Portal.Core.Models.Auth;

namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Unified settings for all identity proofing requirements, keyed by
/// resource+action (e.g. "address+view", "card+write").
/// See docs/config/ial/README.md for the configuration guide.
/// </summary>
public class IdProofingRequirementsSettings
{
    public static readonly string SectionName = "IdProofingRequirements";

    /// <summary>
    /// Map of config key (e.g. "address+view") to its IAL requirement.
    /// Populated by <c>ConfigureIdProofingRequirements</c>.
    /// </summary>
    public Dictionary<string, IalRequirement> Requirements { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the requirement for a config key. Returns <see cref="IalRequirement.Default"/>
    /// (IAL1plus) if the key is not configured — fail-safe by design.
    /// </summary>
    public IalRequirement Get(string key)
    {
        return Requirements.TryGetValue(key, out var req) ? req : IalRequirement.Default();
    }

    /// <summary>
    /// Gets the requirement for a resource+action enum pair.
    /// </summary>
    public IalRequirement Get(ProtectedResource resource, ProtectedAction action)
    {
        return Get(IdProofingKeys.ToConfigKey(resource, action));
    }
}
```

- [ ] **Step 4: Implement ConfigureIdProofingRequirements**

```csharp
// src/SEBT.Portal.Infrastructure/Configuration/ConfigureIdProofingRequirements.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;

namespace SEBT.Portal.Infrastructure.Configuration;

/// <summary>
/// Custom config binder for IdProofingRequirements. Handles the polymorphic
/// value format: each key can be a simple string ("IAL1plus") or an object
/// with per-case-type sub-requirements.
/// </summary>
public class ConfigureIdProofingRequirements(
    IConfiguration config,
    ILogger<ConfigureIdProofingRequirements> logger)
    : IConfigureOptions<IdProofingRequirementsSettings>
{
    public void Configure(IdProofingRequirementsSettings options)
    {
        var section = config.GetSection(IdProofingRequirementsSettings.SectionName);
        options.Requirements.Clear();

        foreach (var child in section.GetChildren())
        {
            if (!IdProofingKeys.AllValidKeys.Contains(child.Key))
            {
                logger.LogWarning(
                    "Unrecognized IdProofingRequirements key '{Key}'. " +
                    "Valid keys are resource+action combinations: {ValidKeys}",
                    child.Key,
                    string.Join(", ", IdProofingKeys.AllValidKeys));
            }

            if (child.Value is not null)
            {
                // Simple form: "address+view": "IAL1plus"
                var level = Enum.Parse<IalLevel>(child.Value, ignoreCase: true);
                options.Requirements[child.Key] = IalRequirement.Uniform(level);
            }
            else
            {
                // Object form: "household+view": { "application": "IAL1plus", ... }
                var perCase = new Dictionary<string, IalLevel>();
                foreach (var sub in child.GetChildren())
                {
                    perCase[sub.Key] = Enum.Parse<IalLevel>(sub.Value!, ignoreCase: true);
                }

                options.Requirements[child.Key] = IalRequirement.PerCaseType(perCase);
            }
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~ConfigureIdProofingRequirementsTests" --verbosity normal`
Expected: All pass

- [ ] **Step 6: Commit**

```
feat: add unified settings class and polymorphic config binder
```

---

## Task 4: Coherence Validator (TDD)

The startup and hot-reload validator that enforces write >= view and step-up consistency.

**Files:**
- Create: `src/SEBT.Portal.Infrastructure/Configuration/IdProofingRequirementsCoherenceValidator.cs`
- Create: `test/SEBT.Portal.Tests/Unit/Configuration/IdProofingRequirementsCoherenceValidatorTests.cs`

- [ ] **Step 1: Write failing tests for coherence validator**

```csharp
// test/SEBT.Portal.Tests/Unit/Configuration/IdProofingRequirementsCoherenceValidatorTests.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Infrastructure.Configuration;

namespace SEBT.Portal.Tests.Unit.Configuration;

public class IdProofingRequirementsCoherenceValidatorTests
{
    private static IdProofingRequirementsCoherenceValidator CreateValidator(
        bool stepUpConfigured = false)
    {
        var configValues = new Dictionary<string, string?>();
        if (stepUpConfigured)
        {
            configValues["Oidc:StepUp:DiscoveryEndpoint"] = "https://auth.example.com/.well-known/openid-configuration";
            configValues["Oidc:StepUp:ClientId"] = "test-client";
        }

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        return new IdProofingRequirementsCoherenceValidator(config);
    }

    private static IdProofingRequirementsSettings MakeSettings(
        Dictionary<string, IalRequirement> requirements)
    {
        var settings = new IdProofingRequirementsSettings();
        foreach (var kvp in requirements)
            settings.Requirements[kvp.Key] = kvp.Value;
        return settings;
    }

    [Fact]
    public void Validate_CoherentConfig_Succeeds()
    {
        var validator = CreateValidator();
        var settings = MakeSettings(new Dictionary<string, IalRequirement>
        {
            ["address+view"] = IalRequirement.Uniform(IalLevel.IAL1plus),
            ["address+write"] = IalRequirement.Uniform(IalLevel.IAL1plus),
            ["email+view"] = IalRequirement.Uniform(IalLevel.IAL1),
            ["household+view"] = IalRequirement.Uniform(IalLevel.IAL1plus),
            ["card+write"] = IalRequirement.Uniform(IalLevel.IAL1plus),
        });

        var result = validator.Validate(null, settings);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WriteBelowView_Fails()
    {
        var validator = CreateValidator();
        var settings = MakeSettings(new Dictionary<string, IalRequirement>
        {
            ["address+view"] = IalRequirement.Uniform(IalLevel.IAL1plus),
            ["address+write"] = IalRequirement.Uniform(IalLevel.IAL1),
        });

        var result = validator.Validate(null, settings);
        Assert.True(result.Failed);
        Assert.Contains("address", result.FailureMessage);
    }

    [Fact]
    public void Validate_PerCaseTypeWriteBelowView_Fails()
    {
        var validator = CreateValidator();
        var settings = MakeSettings(new Dictionary<string, IalRequirement>
        {
            ["address+view"] = IalRequirement.Uniform(IalLevel.IAL1plus),
            ["address+write"] = IalRequirement.PerCaseType(new Dictionary<string, IalLevel>
            {
                ["application"] = IalLevel.IAL1,
                ["streamline"] = IalLevel.IAL1plus,
            }),
        });

        var result = validator.Validate(null, settings);
        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_StepUpConfigured_AllWriteIal1_Fails()
    {
        var validator = CreateValidator(stepUpConfigured: true);
        var settings = MakeSettings(new Dictionary<string, IalRequirement>
        {
            ["address+view"] = IalRequirement.Uniform(IalLevel.IAL1),
            ["address+write"] = IalRequirement.Uniform(IalLevel.IAL1),
            ["card+write"] = IalRequirement.Uniform(IalLevel.IAL1),
        });

        var result = validator.Validate(null, settings);
        Assert.True(result.Failed);
        Assert.Contains("step-up", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_StepUpConfigured_OneWriteAboveIal1_Succeeds()
    {
        var validator = CreateValidator(stepUpConfigured: true);
        var settings = MakeSettings(new Dictionary<string, IalRequirement>
        {
            ["address+view"] = IalRequirement.Uniform(IalLevel.IAL1plus),
            ["address+write"] = IalRequirement.Uniform(IalLevel.IAL1plus),
            ["card+write"] = IalRequirement.Uniform(IalLevel.IAL1),
        });

        var result = validator.Validate(null, settings);
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_NoStepUpConfigured_AllWriteIal1_Succeeds()
    {
        var validator = CreateValidator(stepUpConfigured: false);
        var settings = MakeSettings(new Dictionary<string, IalRequirement>
        {
            ["address+view"] = IalRequirement.Uniform(IalLevel.IAL1),
            ["address+write"] = IalRequirement.Uniform(IalLevel.IAL1),
        });

        var result = validator.Validate(null, settings);
        Assert.True(result.Succeeded);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~IdProofingRequirementsCoherenceValidatorTests" --verbosity normal`
Expected: FAIL — validator class does not exist yet

- [ ] **Step 3: Implement the coherence validator**

```csharp
// src/SEBT.Portal.Infrastructure/Configuration/IdProofingRequirementsCoherenceValidator.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;

namespace SEBT.Portal.Infrastructure.Configuration;

/// <summary>
/// Validates IdProofingRequirements coherence at startup and on every config reload.
/// Enforces: write >= view for the same resource, and step-up consistency.
/// </summary>
public class IdProofingRequirementsCoherenceValidator(IConfiguration configuration)
    : IValidateOptions<IdProofingRequirementsSettings>
{
    public ValidateOptionsResult Validate(string? name, IdProofingRequirementsSettings options)
    {
        var failures = new List<string>();

        CheckWriteNotBelowView(options, failures);
        CheckStepUpConsistency(options, failures);

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(string.Join("; ", failures))
            : ValidateOptionsResult.Success;
    }

    /// <summary>
    /// For each resource that has both +view and +write requirements,
    /// every write level must be >= every view level.
    /// </summary>
    private static void CheckWriteNotBelowView(
        IdProofingRequirementsSettings options,
        List<string> failures)
    {
        // Group keys by resource name (everything before the '+')
        var byResource = options.Requirements.Keys
            .Where(k => k.Contains('+'))
            .GroupBy(k => k.Split('+')[0], StringComparer.OrdinalIgnoreCase);

        foreach (var group in byResource)
        {
            var resource = group.Key;
            var viewKey = $"{resource}+view";
            var writeKey = $"{resource}+write";

            var hasView = options.Requirements.TryGetValue(viewKey, out var viewReq);
            var hasWrite = options.Requirements.TryGetValue(writeKey, out var writeReq);

            if (!hasView || !hasWrite)
                continue;

            foreach (var writeLevel in writeReq!.AllLevels())
            foreach (var viewLevel in viewReq!.AllLevels())
            {
                if (writeLevel < viewLevel)
                {
                    failures.Add(
                        $"{writeKey} level {writeLevel} is below {viewKey} level {viewLevel}. " +
                        "Write operations must require at least the same IAL as view operations.");
                }
            }
        }
    }

    /// <summary>
    /// If OIDC step-up is configured, at least one +write requirement must be above IAL1.
    /// Otherwise the step-up infrastructure is configured but never triggered.
    /// </summary>
    private void CheckStepUpConsistency(
        IdProofingRequirementsSettings options,
        List<string> failures)
    {
        var stepUpDiscovery = configuration["Oidc:StepUp:DiscoveryEndpoint"];
        var stepUpClientId = configuration["Oidc:StepUp:ClientId"];

        var stepUpConfigured = !string.IsNullOrWhiteSpace(stepUpDiscovery)
                               || !string.IsNullOrWhiteSpace(stepUpClientId);

        if (!stepUpConfigured)
            return;

        var anyWriteAboveIal1 = options.Requirements
            .Where(kvp => kvp.Key.EndsWith("+write", StringComparison.OrdinalIgnoreCase))
            .SelectMany(kvp => kvp.Value.AllLevels())
            .Any(level => level > IalLevel.IAL1);

        if (!anyWriteAboveIal1)
        {
            failures.Add(
                "OIDC step-up is configured (Oidc:StepUp) but no write operation requires " +
                "above IAL1. Step-up authentication will never be triggered. " +
                "Set at least one +write requirement to IAL1plus or higher.");
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~IdProofingRequirementsCoherenceValidatorTests" --verbosity normal`
Expected: All pass

- [ ] **Step 5: Commit**

```
feat: add IdProofingRequirements coherence validator (write >= view, step-up consistency)
```

---

## Task 5: IdProofingService Implementation (TDD)

The singleton service implementing both interfaces.

**Files:**
- Create: `src/SEBT.Portal.Infrastructure/Services/IdProofingService.cs`
- Create: `test/SEBT.Portal.Tests/Unit/Services/IdProofingServiceTests.cs`

- [ ] **Step 1: Write failing tests for IdProofingService**

```csharp
// test/SEBT.Portal.Tests/Unit/Services/IdProofingServiceTests.cs
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Unit.Services;

public class IdProofingServiceTests
{
    private static IdProofingService CreateService(IdProofingRequirementsSettings settings)
    {
        var monitor = Substitute.For<IOptionsMonitor<IdProofingRequirementsSettings>>();
        monitor.CurrentValue.Returns(settings);
        return new IdProofingService(monitor, NullLogger<IdProofingService>.Instance);
    }

    private static IdProofingRequirementsSettings DefaultSettings()
    {
        var settings = new IdProofingRequirementsSettings();
        settings.Requirements["address+view"] = IalRequirement.Uniform(IalLevel.IAL1plus);
        settings.Requirements["address+write"] = IalRequirement.Uniform(IalLevel.IAL1plus);
        settings.Requirements["email+view"] = IalRequirement.Uniform(IalLevel.IAL1);
        settings.Requirements["phone+view"] = IalRequirement.Uniform(IalLevel.IAL1);
        settings.Requirements["household+view"] = IalRequirement.Uniform(IalLevel.IAL1plus);
        settings.Requirements["card+write"] = IalRequirement.Uniform(IalLevel.IAL1plus);
        return settings;
    }

    private static SummerEbtCase ApplicationCase() =>
        new()
        {
            ChildFirstName = "Test",
            ChildLastName = "Child",
            IsStreamlineCertified = false,
            IsCoLoaded = false
        };

    // --- Evaluate tests ---

    [Fact]
    public void Evaluate_UserMeetsRequirement_ReturnsAllowed()
    {
        var service = CreateService(DefaultSettings());
        var decision = service.Evaluate(
            ProtectedResource.Address, ProtectedAction.Write,
            UserIalLevel.IAL1plus, [ApplicationCase()]);
        Assert.True(decision.IsAllowed);
        Assert.Equal(UserIalLevel.IAL1plus, decision.RequiredLevel);
    }

    [Fact]
    public void Evaluate_UserBelowRequirement_ReturnsDenied()
    {
        var service = CreateService(DefaultSettings());
        var decision = service.Evaluate(
            ProtectedResource.Address, ProtectedAction.Write,
            UserIalLevel.IAL1, [ApplicationCase()]);
        Assert.False(decision.IsAllowed);
        Assert.Equal(UserIalLevel.IAL1plus, decision.RequiredLevel);
    }

    [Fact]
    public void Evaluate_UnconfiguredKey_DefaultsToIal1plus()
    {
        var settings = new IdProofingRequirementsSettings();
        var service = CreateService(settings);
        var decision = service.Evaluate(
            ProtectedResource.Card, ProtectedAction.Write,
            UserIalLevel.IAL1, [ApplicationCase()]);
        Assert.False(decision.IsAllowed);
        Assert.Equal(UserIalLevel.IAL1plus, decision.RequiredLevel);
    }

    // --- GetVisibility tests ---

    [Fact]
    public void GetVisibility_Ial1plus_ShowsAddress()
    {
        var service = CreateService(DefaultSettings());
        var visibility = service.GetVisibility(UserIalLevel.IAL1plus);
        Assert.True(visibility.IncludeAddress);
        Assert.True(visibility.IncludeEmail);
        Assert.True(visibility.IncludePhone);
    }

    [Fact]
    public void GetVisibility_Ial1_HidesAddressShowsEmailPhone()
    {
        var service = CreateService(DefaultSettings());
        var visibility = service.GetVisibility(UserIalLevel.IAL1);
        Assert.False(visibility.IncludeAddress);
        Assert.True(visibility.IncludeEmail);
        Assert.True(visibility.IncludePhone);
    }

    [Fact]
    public void GetVisibility_None_HidesAll()
    {
        var settings = DefaultSettings();
        settings.Requirements["email+view"] = IalRequirement.Uniform(IalLevel.IAL1);
        settings.Requirements["phone+view"] = IalRequirement.Uniform(IalLevel.IAL1);
        var service = CreateService(settings);

        var visibility = service.GetVisibility(UserIalLevel.None);
        Assert.False(visibility.IncludeAddress);
        Assert.False(visibility.IncludeEmail);
        Assert.False(visibility.IncludePhone);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter "FullyQualifiedName~IdProofingServiceTests" --verbosity normal`
Expected: FAIL — `IdProofingService` does not exist yet

- [ ] **Step 3: Implement IdProofingService**

```csharp
// src/SEBT.Portal.Infrastructure/Services/IdProofingService.cs
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Unified identity proofing service. Evaluates IAL requirements for
/// resource+action pairs and determines PII visibility.
/// Singleton lifetime — holds a volatile reference to settings that is
/// swapped safely on config changes.
/// </summary>
public class IdProofingService : IIdProofingService, IPiiVisibilityService
{
    // volatile ensures that when the OnChange callback swaps this reference,
    // HTTP request threads on other CPU cores see the new value immediately
    // rather than reading a stale cached copy from their local CPU cache.
    private volatile IdProofingRequirementsSettings _settings;
    private readonly ILogger<IdProofingService> _logger;

    public IdProofingService(
        IOptionsMonitor<IdProofingRequirementsSettings> monitor,
        ILogger<IdProofingService> logger)
    {
        _settings = monitor.CurrentValue;
        _logger = logger;

        monitor.OnChange(_ =>
        {
            try
            {
                _settings = monitor.CurrentValue;
                logger.LogInformation("IdProofingRequirements config reloaded successfully");
            }
            catch (OptionsValidationException ex)
            {
                logger.LogCritical(
                    ex,
                    "IdProofingRequirements config change rejected — retaining previous valid config. "
                    + "This is a SECURITY configuration failure that must be fixed immediately.");
            }
        });
    }

    public IdProofingDecision Evaluate(
        ProtectedResource resource,
        ProtectedAction action,
        UserIalLevel userIalLevel,
        IReadOnlyList<SummerEbtCase> cases)
    {
        var requirement = _settings.Get(resource, action);
        var requiredLevel = requirement.Resolve(cases);
        return new IdProofingDecision(
            IsAllowed: userIalLevel >= requiredLevel,
            RequiredLevel: requiredLevel);
    }

    public PiiVisibility GetVisibility(UserIalLevel userIalLevel)
    {
        return new PiiVisibility(
            IncludeAddress: EvaluateView(ProtectedResource.Address, userIalLevel),
            IncludeEmail: EvaluateView(ProtectedResource.Email, userIalLevel),
            IncludePhone: EvaluateView(ProtectedResource.Phone, userIalLevel));
    }

    private bool EvaluateView(ProtectedResource resource, UserIalLevel userIalLevel)
    {
        var requirement = _settings.Get(resource, ProtectedAction.View);
        var requiredLevel = requirement.Resolve([]);
        return userIalLevel >= requiredLevel;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter "FullyQualifiedName~IdProofingServiceTests" --verbosity normal`
Expected: All pass

- [ ] **Step 5: Commit**

```
feat: add IdProofingService with singleton hot-reload and fail-safe config updates
```

---

## Task 6: DI Registration and Config Update

Wire everything up and update `appsettings.json`.

**Files:**
- Modify: `src/SEBT.Portal.Infrastructure/Dependencies.cs`
- Modify: `src/SEBT.Portal.Api/appsettings.json`

- [ ] **Step 1: Update DI registrations in Dependencies.cs**

In `AddPortalInfrastructureServices`, replace:
```csharp
// ID Proofing Requirements (state-specific PII visibility)
services.AddScoped<IIdProofingRequirementsService, IdProofingRequirementsService>();

// Minimum IAL service (state-configurable identity assurance level requirements)
services.AddScoped<IMinimumIalService, MinimumIalService>();
```

With:
```csharp
// Unified identity proofing service (PII visibility + authorization gates)
services.AddSingleton<IdProofingService>();
services.AddSingleton<IIdProofingService>(sp => sp.GetRequiredService<IdProofingService>());
services.AddSingleton<IPiiVisibilityService>(sp => sp.GetRequiredService<IdProofingService>());
```

In `AddPortalInfrastructureAppSettings`, replace:
```csharp
services.AddSingleton<IValidateOptions<IdProofingRequirementsSettings>, IdProofingRequirementsSettingsValidator>();
services.AddOptionsWithValidateOnStart<IdProofingRequirementsSettings>()
    .BindConfiguration(IdProofingRequirementsSettings.SectionName);
services.AddSingleton<IValidateOptions<MinimumIalSettings>, MinimumIalSettingsValidator>();
services.AddOptionsWithValidateOnStart<MinimumIalSettings>()
    .BindConfiguration(MinimumIalSettings.SectionName);
```

With:
```csharp
services.ConfigureOptions<ConfigureIdProofingRequirements>();
services.AddSingleton<IValidateOptions<IdProofingRequirementsSettings>, IdProofingRequirementsCoherenceValidator>();
services.AddOptionsWithValidateOnStart<IdProofingRequirementsSettings>();
```

Update the `using` statements as needed — add `using SEBT.Portal.Infrastructure.Services;` if not present, remove unused imports for `MinimumIalSettings`, `MinimumIalSettingsValidator`, and `IdProofingRequirementsSettingsValidator`.

- [ ] **Step 2: Update appsettings.json**

Replace the existing `IdProofingRequirements` section:
```json
"IdProofingRequirements": {
  "address+view": "IAL1plus",
  "email+view": "IAL1",
  "phone+view": "IAL1"
}
```

With:
```json
"IdProofingRequirements": {
  "address+view": "IAL1plus",
  "address+write": "IAL1plus",
  "email+view": "IAL1",
  "phone+view": "IAL1",
  "household+view": "IAL1plus",
  "card+write": "IAL1plus"
}
```

- [ ] **Step 3: Verify build**

Run: `dotnet build src/SEBT.Portal.Api/SEBT.Portal.Api.csproj`
Expected: Build succeeded (handler compilation will fail — that's expected, handled in next task)

Note: If the build fails because the handlers still reference old interfaces, that's expected. They'll be updated in Task 7.

- [ ] **Step 4: Commit**

```
feat: wire up unified IdProofingRequirements DI and update appsettings defaults
```

---

## Task 7: Migrate Handlers (Immediate Cutover)

Update the three handlers and their tests to use the new interfaces.

**Files:**
- Modify: `src/SEBT.Portal.UseCases/Household/GetHouseholdData/GetHouseholdDataQueryHandler.cs`
- Modify: `src/SEBT.Portal.UseCases/Household/UpdateAddress/UpdateAddressCommandHandler.cs`
- Modify: `src/SEBT.Portal.UseCases/Household/RequestCardReplacement/RequestCardReplacementCommandHandler.cs`
- Modify: `test/SEBT.Portal.Tests/Unit/UseCases/Household/GetHouseholdDataQueryHandlerTests.cs`
- Modify: `test/SEBT.Portal.Tests/Unit/UseCases/Household/UpdateAddressCommandHandlerTests.cs`
- Modify: `test/SEBT.Portal.Tests/Unit/UseCases/Household/RequestCardReplacementCommandHandlerTests.cs`
- Modify: `test/SEBT.Portal.Tests/Unit/UseCases/DependenciesTests.cs`

- [ ] **Step 1: Update GetHouseholdDataQueryHandler**

Replace the constructor parameters and IAL check. The full handler is at `src/SEBT.Portal.UseCases/Household/GetHouseholdData/GetHouseholdDataQueryHandler.cs`. Read it first, then:

1. Replace `IIdProofingRequirementsService idProofingRequirementsService` with `IPiiVisibilityService piiVisibilityService`
2. Replace `IMinimumIalService minimumIalService` with `IIdProofingService idProofingService`
3. Replace `idProofingRequirementsService.GetPiiVisibility(userIalLevel)` with `piiVisibilityService.GetVisibility(userIalLevel)`
4. Replace the MinimumIal block:

```csharp
// Before:
var minimumIal = minimumIalService.GetMinimumIal(householdData.SummerEbtCases);
if (userIalLevel < minimumIal)
{
    // SECURITY: ...
    logger.LogInformation(
        "Household data access denied: user IAL {UserIal} is below minimum {MinimumIal}",
        userIalLevel,
        minimumIal);
    return Result<HouseholdData>.Forbidden(
        $"This household requires {minimumIal}. Complete identity verification to access this data.",
        new Dictionary<string, object?> { ["requiredIal"] = minimumIal.ToString() });
}
```

With:
```csharp
// SECURITY: Never return household case data when the user has not met
// the IAL required by their cases. See docs/config/ial/README.md.
var decision = idProofingService.Evaluate(
    ProtectedResource.Household, ProtectedAction.View,
    userIalLevel, householdData.SummerEbtCases);
if (!decision.IsAllowed)
{
    logger.LogInformation(
        "Household data access denied: user IAL {UserIal} is below required {RequiredIal}",
        userIalLevel,
        decision.RequiredLevel);
    return Result<HouseholdData>.Forbidden(
        $"This household requires {decision.RequiredLevel}. Complete identity verification to access this data.",
        new Dictionary<string, object?> { ["requiredIal"] = decision.RequiredLevel.ToString() });
}
```

Update `using` statements: add `SEBT.Portal.Core.Models.Auth` (for `ProtectedResource`, `ProtectedAction`), remove unused `IMinimumIalService` / `IIdProofingRequirementsService` if they were explicit.

- [ ] **Step 2: Update UpdateAddressCommandHandler**

Read the full handler first. Then:

1. Replace `IIdProofingRequirementsService idProofingRequirementsService` with `IPiiVisibilityService piiVisibilityService`
2. Replace `IMinimumIalService minimumIalService` with `IIdProofingService idProofingService`
3. Replace `idProofingRequirementsService.GetPiiVisibility(userIalLevel)` with `piiVisibilityService.GetVisibility(userIalLevel)`
4. Replace the MinimumIal block:

```csharp
// Before:
var minimumIal = minimumIalService.GetMinimumIal(household.SummerEbtCases);
if (userIalLevel < minimumIal)
{
    logger.LogInformation(
        "Address update denied: user IAL {UserIal} is below minimum {MinimumIal}",
        userIalLevel,
        minimumIal);
    return Result<AddressValidationResult>.Forbidden(
        $"This household requires {minimumIal}. Complete identity verification to update your address.",
        new Dictionary<string, object?> { ["requiredIal"] = minimumIal.ToString() });
}
```

With:
```csharp
// SECURITY: Block write operations when the user has not met the IAL
// required by their cases. See docs/config/ial/README.md.
var decision = idProofingService.Evaluate(
    ProtectedResource.Address, ProtectedAction.Write,
    userIalLevel, household.SummerEbtCases);
if (!decision.IsAllowed)
{
    logger.LogInformation(
        "Address update denied: user IAL {UserIal} is below required {RequiredIal}",
        userIalLevel,
        decision.RequiredLevel);
    return Result<AddressValidationResult>.Forbidden(
        $"This household requires {decision.RequiredLevel}. Complete identity verification to update your address.",
        new Dictionary<string, object?> { ["requiredIal"] = decision.RequiredLevel.ToString() });
}
```

- [ ] **Step 3: Update RequestCardReplacementCommandHandler**

Read the full handler first. Then:

1. Replace `IMinimumIalService minimumIalService` with `IIdProofingService idProofingService`
2. Replace the MinimumIal block:

```csharp
// Before:
var minimumIal = minimumIalService.GetMinimumIal(household.SummerEbtCases);
if (userIalLevel < minimumIal)
{
    logger.LogInformation(
        "Card replacement denied: user IAL {UserIal} is below minimum {MinimumIal}",
        userIalLevel,
        minimumIal);
    return Result.Forbidden(
        $"This household requires {minimumIal}. Complete identity verification to request card replacements.");
}
```

With:
```csharp
// SECURITY: Block write operations when the user has not met the IAL
// required by their cases. See docs/config/ial/README.md.
var decision = idProofingService.Evaluate(
    ProtectedResource.Card, ProtectedAction.Write,
    userIalLevel, household.SummerEbtCases);
if (!decision.IsAllowed)
{
    logger.LogInformation(
        "Card replacement denied: user IAL {UserIal} is below required {RequiredIal}",
        userIalLevel,
        decision.RequiredLevel);
    return Result.Forbidden(
        $"This household requires {decision.RequiredLevel}. Complete identity verification to request card replacements.");
}
```

- [ ] **Step 4: Update GetHouseholdDataQueryHandlerTests**

Read the test file first. The key changes:
1. Replace `IIdProofingRequirementsService` mock with `IPiiVisibilityService` mock
2. Replace `IMinimumIalService` mock with `IIdProofingService` mock
3. Replace `_idProofingRequirementsService.GetPiiVisibility(...)` setups with `_piiVisibilityService.GetVisibility(...)`
4. Replace `_minimumIalService.GetMinimumIal(...)` setups with `_idProofingService.Evaluate(...)` setups that return `new IdProofingDecision(IsAllowed: true/false, RequiredLevel: ...)`
5. Update the handler construction to use new service mocks

Default mock setup in constructor:
```csharp
// Before:
_minimumIalService.GetMinimumIal(Arg.Any<IReadOnlyList<SummerEbtCase>>()).Returns(UserIalLevel.None);

// After:
_idProofingService.Evaluate(
    Arg.Any<ProtectedResource>(), Arg.Any<ProtectedAction>(),
    Arg.Any<UserIalLevel>(), Arg.Any<IReadOnlyList<SummerEbtCase>>())
    .Returns(new IdProofingDecision(IsAllowed: true, RequiredLevel: UserIalLevel.None));
```

- [ ] **Step 5: Update UpdateAddressCommandHandlerTests**

Same pattern as Step 4. Read the test file first. Replace both old service mocks with `IIdProofingService` and `IPiiVisibilityService` mocks. Update constructor default setups and handler factory.

- [ ] **Step 6: Update RequestCardReplacementCommandHandlerTests**

Read the test file first. Replace `IMinimumIalService` mock with `IIdProofingService` mock. Update constructor default setup. This handler doesn't use `IPiiVisibilityService`.

- [ ] **Step 7: Update DependenciesTests**

Read `test/SEBT.Portal.Tests/Unit/UseCases/DependenciesTests.cs` first. Replace:
```csharp
services.AddSingleton(Substitute.For<IMinimumIalService>());
```

With:
```csharp
services.AddSingleton(Substitute.For<IIdProofingService>());
services.AddSingleton(Substitute.For<IPiiVisibilityService>());
```

Add the necessary `using` for `IIdProofingService` and `IPiiVisibilityService`.

- [ ] **Step 8: Verify all tests pass**

Run: `dotnet test --verbosity normal`
Expected: All tests pass

- [ ] **Step 9: Commit**

```
refactor: migrate handlers to unified IIdProofingService and IPiiVisibilityService
```

---

## Task 8: Delete Old Code

Remove the old settings, services, validators, and their tests.

**Files to delete:**
- `src/SEBT.Portal.Core/AppSettings/MinimumIalSettings.cs`
- `src/SEBT.Portal.Core/Services/IMinimumIalService.cs`
- `src/SEBT.Portal.Core/Services/IIdProofingRequirementsService.cs`
- `src/SEBT.Portal.Infrastructure/Services/MinimumIalService.cs`
- `src/SEBT.Portal.Infrastructure/Services/IdProofingRequirementsService.cs`
- `src/SEBT.Portal.Infrastructure/Configuration/MinimumIalSettingsValidator.cs`
- `src/SEBT.Portal.Infrastructure/Configuration/IdProofingRequirementsSettingsValidator.cs`
- `test/SEBT.Portal.Tests/Unit/Services/MinimumIalServiceTests.cs`
- `test/SEBT.Portal.Tests/Unit/Services/IdProofingRequirementsServiceTests.cs`
- `test/SEBT.Portal.Tests/Unit/Configuration/IdProofingRequirementsSettingsValidatorTests.cs`

- [ ] **Step 1: Delete old files**

Delete all files listed above. Use `git rm` for each.

- [ ] **Step 2: Remove unused imports from Dependencies.cs**

Read `src/SEBT.Portal.Infrastructure/Dependencies.cs` and remove any `using` statements that reference deleted types (e.g., `MinimumIalSettings`, `MinimumIalSettingsValidator`, `IdProofingRequirementsSettingsValidator`).

- [ ] **Step 3: Verify build and tests**

Run: `dotnet test --verbosity normal`
Expected: All tests pass, no build errors. If there are compilation errors referencing deleted types, fix the remaining references.

- [ ] **Step 4: Commit**

```
refactor: remove old MinimumIal and IdProofingRequirements code
```

---

## Task 9: Update Security Test Harness

Adapt the existing `MinimumIalFailOpenTests` to use the new unified interface and prove the fail-open scenario is prevented by secure defaults.

**Files:**
- Modify: `test/SEBT.Portal.Tests/Unit/Security/MinimumIalFailOpenTests.cs`

- [ ] **Step 1: Rewrite tests using new types**

Read the existing test file. Rewrite it to:
1. Test `IalRequirement` and `IdProofingRequirementsSettings` directly (replacing `MinimumIalService` + `MinimumIalSettings`)
2. Test that the default settings (IAL1plus) block IAL1 users
3. Test that the coherence validator rejects the CO misconfiguration scenario
4. Test that loading actual `appsettings.json` produces secure defaults
5. Keep the test class name or rename to `IdProofingFailOpenTests` — the intent is the same

- [ ] **Step 2: Verify all tests pass**

Run: `dotnet test --filter "FullyQualifiedName~FailOpenTests" --verbosity normal`
Expected: All pass

- [ ] **Step 3: Run full test suite**

Run: `dotnet test --verbosity normal`
Expected: All pass, 0 failures

- [ ] **Step 4: Commit**

```
test: adapt security test harness to unified IdProofingRequirements
```

---

## Task 10: Update State Overlay Configs

Update the state-specific appsettings files that are in source control.

**Files:**
- Modify: `src/SEBT.Portal.Api/appsettings.co.json`
- Modify: `src/SEBT.Portal.Api/appsettings.dc.json`

- [ ] **Step 1: Update CO config**

Read `src/SEBT.Portal.Api/appsettings.co.json` first. Remove the `MinimumIal` section entirely. Replace the `IdProofingRequirements` section with:

```json
"IdProofingRequirements": {
  "address+view": "IAL1plus",
  "address+write": "IAL1plus",
  "email+view": "IAL1plus",
  "phone+view": "IAL1",
  "household+view": "IAL1plus",
  "card+write": "IAL1plus"
}
```

- [ ] **Step 2: Update DC config**

Read `src/SEBT.Portal.Api/appsettings.dc.json` first. Remove the `MinimumIal` section. DC does not need an `IdProofingRequirements` override — the base `appsettings.json` defaults are correct for DC except for `household+view` which needs per-case-type config. Add:

```json
"IdProofingRequirements": {
  "household+view": {
    "application": "IAL1",
    "coloadedStreamline": "IAL1",
    "streamline": "IAL1plus"
  }
}
```

- [ ] **Step 3: Verify build and tests**

Run: `dotnet test --verbosity normal`
Expected: All tests pass

- [ ] **Step 4: Commit**

```
fix: update state overlay configs to unified IdProofingRequirements format
```
