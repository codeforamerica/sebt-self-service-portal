# Unified IdProofingRequirements — Design Spec

## Summary

Unify two separate IAL configuration systems (`IdProofingRequirements` and `MinimumIal`) into a single `IdProofingRequirements` config section with `resource+action` keys. This eliminates a class of security misconfiguration where write-level IAL could be lower than view-level IAL for the same data — the exact bug that allowed Colorado users to change their address without step-up identity verification.

## Problem

The portal has two independent config sections controlling identity assurance:

- **`IdProofingRequirements`** — per-field PII visibility (`address+view`, `email+view`, `phone+view`)
- **`MinimumIal`** — per-case-type feature access (`application`, `coloadedStreamline`, `streamline`)

No validation enforced coherence between them. Colorado's `MinimumIal` was set to `IAL1` across the board, meaning a user at IAL1 could change their address (a write operation) even though `IdProofingRequirements` correctly required IAL1plus to *view* the address. The backend enforcement code was correct — the configuration was not.

Root causes:
1. `MinimumIalSettings` had no secure defaults (nullable properties, no in-code defaults)
2. No cross-system invariant checked write >= view for the same resource
3. The `MinimumIal` name didn't communicate what it protected
4. Two config sections made the relationship between view and write requirements implicit

## Design

### Configuration shape

A single `IdProofingRequirements` section with `resource+action` keys. Values are either:
- A **string** (e.g., `"IAL1plus"`) — uniform requirement for all case types
- An **object** with per-case-type sub-requirements — when different case types have different IAL needs

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

Per-case-type example (when different case types have different IAL needs):

```json
"household+view": {
  "application": "IAL1",
  "coloadedStreamline": "IAL1",
  "streamline": "IAL1plus"
}
```

The `MinimumIal` config section is removed.

### Secure defaults

The settings class defaults every requirement to `IalLevel.IAL1plus`. If a key is missing from config, it defaults to the most restrictive level. States explicitly opt *down* to IAL1 where policy allows — never the reverse.

Base `appsettings.json` includes the full section with IAL1plus defaults for all write operations, making the secure intent visible in source control.

### Enums

```csharp
public enum ProtectedResource { Address, Email, Phone, Household, Card }
public enum ProtectedAction { View, Write }
```

These provide compile-time safety at call sites and define the set of valid config keys. The mapping to config key strings is: `$"{resource}+{action}"` (lowercased).

Not all combinations are meaningful — `email+write` and `phone+write` don't correspond to any current feature, and `card+view` isn't a distinct operation (card info is part of household data). The config binder warns on unrecognized keys, and the validator only enforces coherence for combinations that exist in config.

### Settings class

```csharp
public class IalRequirement
{
    // Uniform form: same level for all case types
    private IalLevel? _uniform;

    // Per-case-type form
    private Dictionary<string, IalLevel>? _perCaseType;

    public static IalRequirement Uniform(IalLevel level);
    public static IalRequirement PerCaseType(Dictionary<string, IalLevel> levels);

    /// Resolves the required level. For uniform requirements, returns the level directly.
    /// For per-case-type, applies "highest wins" across the user's cases.
    public UserIalLevel Resolve(IReadOnlyList<SummerEbtCase> cases);

    /// Returns all configured levels (for validation comparisons).
    public IEnumerable<IalLevel> AllLevels();
}

public class IdProofingRequirementsSettings
{
    public Dictionary<string, IalRequirement> Requirements { get; set; } = new();

    public IalRequirement Get(string key);
    public IalRequirement Get(ProtectedResource resource, ProtectedAction action);
}
```

### Config binding

Uses `IConfigureOptions<IdProofingRequirementsSettings>` for custom parsing. This preserves the full `IOptions` ecosystem including hot reload.

```csharp
public class ConfigureIdProofingRequirements(IConfiguration config, ILogger<...> logger)
    : IConfigureOptions<IdProofingRequirementsSettings>
{
    public void Configure(IdProofingRequirementsSettings options)
    {
        var section = config.GetSection("IdProofingRequirements");
        options.Requirements.Clear();

        foreach (var child in section.GetChildren())
        {
            // Warn on unrecognized keys (typos, stale config)
            if (!IsKnownKey(child.Key))
            {
                logger.LogWarning(
                    "Unrecognized IdProofingRequirements key '{Key}'. " +
                    "Valid keys are resource+action combinations: {ValidKeys}",
                    child.Key, string.Join(", ", GetAllValidKeys()));
            }

            if (child.Value is not null)
            {
                // Simple form: "address+view": "IAL1plus"
                var level = Enum.Parse<IalLevel>(child.Value, ignoreCase: true);
                options.Requirements[child.Key] = IalRequirement.Uniform(level);
            }
            else
            {
                // Object form: "card+write": { "application": "IAL1plus", ... }
                var perCase = child.GetChildren()
                    .ToDictionary(c => c.Key, c => Enum.Parse<IalLevel>(c.Value!, ignoreCase: true));
                options.Requirements[child.Key] = IalRequirement.PerCaseType(perCase);
            }
        }
    }

    private static bool IsKnownKey(string key)
    {
        // Check against all ProtectedResource+ProtectedAction combinations
    }
}
```

The polymorphism detection: `IConfigurationSection.Value` is non-null for simple string values, null for object values with children. This is how .NET's configuration flattening already works.

### Config change failure mode

Uses `IOptionsMonitor<T>` (not `IOptionsSnapshot<T>`) in the service. When validation fails on a config change:
- The previous valid config is retained (last-known-good)
- A **Critical** log is emitted — this is a security configuration failure
- The application continues serving on the last-good config rather than returning 500s

**Service lifetime: singleton.** `IdProofingService` holds no per-request state — `Evaluate()` and `GetVisibility()` are pure functions over config + inputs. Singleton lifetime is correct and avoids the subscription-leak problem that scoped + `OnChange` would create.

**Mechanism:** The service holds a `volatile` reference to the current settings. On construction, it subscribes once to `IOptionsMonitor<T>.OnChange`. In the callback, it wraps `settingsMonitor.CurrentValue` in a try-catch: on success, it swaps the reference; on `OptionsValidationException`, it logs at Critical and retains the previous value.

```csharp
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
        _settings = monitor.CurrentValue; // validated at startup
        _logger = logger;

        monitor.OnChange(_ =>
        {
            try
            {
                _settings = monitor.CurrentValue;
            }
            catch (OptionsValidationException ex)
            {
                logger.LogCritical(ex,
                    "IdProofingRequirements config change rejected — retaining previous valid config. "
                    + "This is a SECURITY configuration failure that must be fixed immediately.");
            }
        });
    }
    // ...
}
```

Registered as singleton:

```csharp
services.AddSingleton<IdProofingService>();
services.AddSingleton<IIdProofingService>(sp => sp.GetRequiredService<IdProofingService>());
services.AddSingleton<IPiiVisibilityService>(sp => sp.GetRequiredService<IdProofingService>());
```

### Validation

Registered as `IValidateOptions<IdProofingRequirementsSettings>`. Runs at startup (via `ValidateOnStart`) and on every config rebind.

Checks:
1. **Required keys present** — all `ProtectedResource+ProtectedAction` combinations that the code references must exist in config (or fall back to IAL1plus default)
2. **Coherence: write >= view** — for each resource, every level in the `+write` requirement must be >= every level in the `+view` requirement. Prevents the exact inversion that caused the CO bug.
3. **Step-up consistency** — if `Oidc:StepUp` is configured (indicating the state intends to use step-up auth), at least one `+write` requirement must be > IAL1. Otherwise the step-up infrastructure is configured but never triggered.
4. **Unknown key warning** — config keys that don't match a known `ProtectedResource+ProtectedAction` combination are logged as warnings during binding (not validation failures).

### Service interfaces

Two interfaces, one implementation — interface segregation by consumer intent.

```csharp
/// <summary>
/// Authorization gate: "Can this user do this thing?"
/// Used by command/query handlers.
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

public readonly record struct IdProofingDecision(
    bool IsAllowed,
    UserIalLevel RequiredLevel);
```

```csharp
/// <summary>
/// PII visibility: "What can this user see?"
/// Used by the repository layer for query filtering.
/// </summary>
public interface IPiiVisibilityService
{
    PiiVisibility GetVisibility(UserIalLevel userIalLevel);
}
```

Single implementation (see "Config change failure mode" above for the full constructor with `IOptionsMonitor` and `volatile` semantics):

```csharp
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
    // View requirements are always uniform (no cases needed)
    var requiredLevel = requirement.Resolve([]);
    return userIalLevel >= requiredLevel;
}
```

### Consumer migration (immediate cutover)

Three handlers updated in one PR:

| Handler | Before | After | Key used |
|---------|--------|-------|----------|
| `GetHouseholdDataQueryHandler` | `IIdProofingRequirementsService` + `IMinimumIalService` | `IIdProofingService` + `IPiiVisibilityService` | `household+view` |
| `UpdateAddressCommandHandler` | `IIdProofingRequirementsService` + `IMinimumIalService` | `IIdProofingService` + `IPiiVisibilityService` | `address+write` |
| `RequestCardReplacementCommandHandler` | `IMinimumIalService` | `IIdProofingService` | `card+write` |

Handler code change examples:

**UpdateAddressCommandHandler:**
```csharp
// Before:
var piiVisibility = idProofingRequirementsService.GetPiiVisibility(userIalLevel);
// ...
var minimumIal = minimumIalService.GetMinimumIal(household.SummerEbtCases);
if (userIalLevel < minimumIal)
    return Result.Forbidden(...);

// After:
var piiVisibility = piiVisibilityService.GetVisibility(userIalLevel);
// ...
var decision = idProofingService.Evaluate(
    ProtectedResource.Address, ProtectedAction.Write,
    userIalLevel, household.SummerEbtCases);
if (!decision.IsAllowed)
    return Result.Forbidden(..., new { requiredIal = decision.RequiredLevel.ToString() });
```

**GetHouseholdDataQueryHandler:**
```csharp
// Before:
var minimumIal = minimumIalService.GetMinimumIal(householdData.SummerEbtCases);
if (userIalLevel < minimumIal)
    return Result<HouseholdData>.Forbidden(...);

// After:
var decision = idProofingService.Evaluate(
    ProtectedResource.Household, ProtectedAction.View,
    userIalLevel, householdData.SummerEbtCases);
if (!decision.IsAllowed)
    return Result<HouseholdData>.Forbidden(..., new { requiredIal = decision.RequiredLevel.ToString() });
```

### Deleted code

- `MinimumIalSettings` (settings class)
- `MinimumIalSettingsValidator` (validator)
- `MinimumIalService` / `IMinimumIalService` (service + interface)
- `IdProofingRequirementsService` / `IIdProofingRequirementsService` (service + interface — replaced by `IPiiVisibilityService`)
- `IdProofingRequirementsSettings` (old settings class — replaced by unified version)
- `IdProofingRequirementsSettingsValidator` (old validator — replaced by unified coherence validator)
- `MinimumIal` section from all appsettings files

### Testing

- **Unit tests for `IalRequirement`** — uniform resolution, per-case-type "highest wins", defaults
- **Unit tests for config binding** — simple string form, object form, mixed, unknown keys warning, missing keys default to IAL1plus
- **Unit tests for validation** — coherence (write >= view), step-up consistency, required keys
- **Unit tests for `IdProofingService`** — `Evaluate` with various user IAL / case combinations, `GetVisibility`
- **Updated handler tests** — swap mocked `IMinimumIalService` + `IIdProofingRequirementsService` for mocked `IIdProofingService` + `IPiiVisibilityService`
- **Security characterization tests** — keep the `MinimumIalFailOpenTests` (adapted to the new interface) as regression tests proving the fail-open scenario is no longer possible with correct defaults

### Config changes

**`appsettings.json`** (base defaults, in source control):
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

Remove the `MinimumIal` section (does not exist in base today — confirmed).

State overlays and AppConfig profiles: update to the new key format. The `MinimumIal` section is no longer read.
