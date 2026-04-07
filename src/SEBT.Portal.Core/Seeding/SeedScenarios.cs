using SEBT.Portal.Core.Models.Auth;

namespace SEBT.Portal.Core.Seeding;

/// <summary>
/// Central catalog of all seed scenarios. Both DatabaseSeeder and MockHouseholdRepository
/// reference this catalog so scenario names and metadata are defined in one place.
/// </summary>
public static class SeedScenarios
{
    // IAL1+ scenarios
    public static readonly SeedScenario CoLoaded = new("co-loaded", UserIalLevel.IAL1plus);
    public static readonly SeedScenario Verified = new("verified", UserIalLevel.IAL1plus);
    public static readonly SeedScenario Expired = new("expired", UserIalLevel.IAL1plus);
    public static readonly SeedScenario Review = new("review", UserIalLevel.IAL1plus);

    // IAL1 scenarios
    public static readonly SeedScenario SingleChild = new("singlechild", UserIalLevel.IAL1);
    public static readonly SeedScenario NonCoLoaded = new("non-co-loaded", UserIalLevel.IAL1);

    // Non-IAL scenarios
    public static readonly SeedScenario LargeFamily = new("largefamily", UserIalLevel.None);
    public static readonly SeedScenario NotStarted = new("not-started", UserIalLevel.None);
    public static readonly SeedScenario Pending = new("pending", UserIalLevel.None);
    public static readonly SeedScenario Minimal = new("minimal", UserIalLevel.None);
    public static readonly SeedScenario Denied = new("denied", UserIalLevel.None);
    public static readonly SeedScenario Cancelled = new("cancelled", UserIalLevel.None);
    public static readonly SeedScenario Unknown = new("unknown", UserIalLevel.None);

    // Household-only scenario (not seeded as a User in the database)
    public static readonly SeedScenario MultipleApps = new("multipleapps", UserIalLevel.None);

    // Simple scenarios (non-co-loaded, Summer EBT, active benefits)
    public static readonly SeedScenario Simple1 = new("simple1", UserIalLevel.None);
    public static readonly SeedScenario Simple2 = new("simple2", UserIalLevel.None);
    public static readonly SeedScenario Simple3 = new("simple3", UserIalLevel.None);
    public static readonly SeedScenario Simple4 = new("simple4", UserIalLevel.None);
    public static readonly SeedScenario Simple5 = new("simple5", UserIalLevel.None);
    public static readonly SeedScenario Simple6 = new("simple6", UserIalLevel.None);
    public static readonly SeedScenario Simple7 = new("simple7", UserIalLevel.None);

    /// <summary>
    /// Scenarios that are seeded as User entities in the database.
    /// </summary>
    public static readonly IReadOnlyList<SeedScenario> UserScenarios =
    [
        CoLoaded, Verified, SingleChild, LargeFamily, Expired,
        NonCoLoaded, NotStarted, Pending, Minimal, Denied,
        Review, Cancelled, Unknown,
        Simple1, Simple2, Simple3, Simple4, Simple5, Simple6, Simple7
    ];

    /// <summary>
    /// Scenarios that should only be seeded when STATE=dc.
    /// </summary>
    public static readonly IReadOnlySet<SeedScenario> DcOnlyScenarios =
        new HashSet<SeedScenario> { Simple1, Simple2, Simple3, Simple4, Simple5, Simple6, Simple7 };

    /// <summary>
    /// All scenarios including household-only entries (e.g., MultipleApps).
    /// </summary>
    public static readonly IReadOnlyList<SeedScenario> AllScenarios =
        [.. UserScenarios, MultipleApps];
}
