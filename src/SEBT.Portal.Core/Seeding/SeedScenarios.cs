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
    /// <summary>Co-loaded with SNAP/TANF on file; ID proofing not started (DC mock household + benefit-match dev).</summary>
    public static readonly SeedScenario CoLoadedPendingIdProofing = new("co-loaded-pending-id-proofing", UserIalLevel.None);
    /// <summary>Co-loaded with SNAP/TANF on file; ID proofing completed, but the linked household has zero enrolled children and zero applications.</summary>
    public static readonly SeedScenario CoLoadedNoChildren = new("co-loaded-no-children", UserIalLevel.IAL1plus);
    public static readonly SeedScenario Verified = new("verified", UserIalLevel.IAL1plus);
    public static readonly SeedScenario Expired = new("expired", UserIalLevel.IAL1plus);
    public static readonly SeedScenario Review = new("review", UserIalLevel.IAL1plus);
    public static readonly SeedScenario SummerActive = new("summer-active", UserIalLevel.IAL1plus);
    public static readonly SeedScenario SummerLost = new("summer-lost", UserIalLevel.IAL1plus);
    public static readonly SeedScenario DcMixed = new("dc-mixed", UserIalLevel.IAL1plus);
    public static readonly SeedScenario CoUndeliverable = new("co-undeliverable", UserIalLevel.IAL1plus);
    public static readonly SeedScenario CoFrozen = new("co-frozen", UserIalLevel.IAL1plus);
    public static readonly SeedScenario CoNotActivated = new("co-notactivated", UserIalLevel.IAL1plus);
    public static readonly SeedScenario CoDeactivatedByState = new("co-deactivatedbystate", UserIalLevel.IAL1plus);
    public static readonly SeedScenario CoActive = new("co-active", UserIalLevel.IAL1plus);

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
        CoLoaded, CoLoadedPendingIdProofing, CoLoadedNoChildren, Verified, SingleChild, LargeFamily, Expired,
        NonCoLoaded, NotStarted, Pending, Minimal, Denied,
        Review, Cancelled, Unknown, SummerActive, SummerLost,
        DcMixed, CoUndeliverable, CoFrozen, CoNotActivated, CoDeactivatedByState, CoActive,
        Simple1, Simple2, Simple3, Simple4, Simple5, Simple6, Simple7
    ];

    /// <summary>
    /// Scenarios that should only be seeded when STATE=dc.
    /// </summary>
    public static readonly IReadOnlySet<SeedScenario> DcOnlyScenarios =
        new HashSet<SeedScenario>
        {
            SummerActive, SummerLost, DcMixed,
            Simple1, Simple2, Simple3, Simple4, Simple5, Simple6, Simple7,
            CoLoadedPendingIdProofing,
            CoLoadedNoChildren,
        };

    /// <summary>
    /// All scenarios including household-only entries (e.g., MultipleApps).
    /// </summary>
    public static readonly IReadOnlyList<SeedScenario> AllScenarios =
        [.. UserScenarios, MultipleApps];
}
