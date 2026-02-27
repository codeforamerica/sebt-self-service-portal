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
    public static readonly SeedScenario SingleChild = new("singlechild", UserIalLevel.IAL1plus);
    public static readonly SeedScenario LargeFamily = new("largefamily", UserIalLevel.IAL1plus);
    public static readonly SeedScenario Expired = new("expired", UserIalLevel.IAL1plus);

    // Non-IAL scenarios
    public static readonly SeedScenario NonCoLoaded = new("non-co-loaded", UserIalLevel.None);
    public static readonly SeedScenario NotStarted = new("not-started", UserIalLevel.None);
    public static readonly SeedScenario Pending = new("pending", UserIalLevel.None);
    public static readonly SeedScenario Minimal = new("minimal", UserIalLevel.None);
    public static readonly SeedScenario Denied = new("denied", UserIalLevel.None);
    public static readonly SeedScenario Review = new("review", UserIalLevel.None);
    public static readonly SeedScenario Cancelled = new("cancelled", UserIalLevel.None);
    public static readonly SeedScenario Unknown = new("unknown", UserIalLevel.None);

    // Household-only scenario (not seeded as a User in the database)
    public static readonly SeedScenario MultipleApps = new("multipleapps", UserIalLevel.None);

    /// <summary>
    /// Scenarios that are seeded as User entities in the database.
    /// </summary>
    public static readonly IReadOnlyList<SeedScenario> UserScenarios =
    [
        CoLoaded, Verified, SingleChild, LargeFamily, Expired,
        NonCoLoaded, NotStarted, Pending, Minimal, Denied,
        Review, Cancelled, Unknown
    ];

    /// <summary>
    /// All scenarios including household-only entries (e.g., MultipleApps).
    /// </summary>
    public static readonly IReadOnlyList<SeedScenario> AllScenarios =
        [.. UserScenarios, MultipleApps];
}
