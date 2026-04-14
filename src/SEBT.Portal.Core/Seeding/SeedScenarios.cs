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

    // DC-157 scenarios: exercise the card-status and mixed-issuance paths.
    // IAL1+ so walkthrough tests bypass id-proofing and go straight to dashboard.
    public static readonly SeedScenario LostCard = new("lost-card", UserIalLevel.IAL1plus);
    public static readonly SeedScenario Mixed = new("mixed", UserIalLevel.IAL1plus);

    // DC-157 CO scenarios: card statuses that fall OUTSIDE the replacement allowlist
    // (Lost/Stolen/Damaged), so the replacement CTA correctly hides on CO while
    // address update stays available.
    public static readonly SeedScenario CoNotActivated = new("not-activated", UserIalLevel.IAL1plus);
    public static readonly SeedScenario CoDeactivatedByState = new("deactivated-state", UserIalLevel.IAL1plus);
    public static readonly SeedScenario CoUndeliverable = new("undeliverable", UserIalLevel.IAL1plus);
    // Mixed: one replacement-eligible case (Lost) + one denied case (DeactivatedByState).
    // Exercises CardSelection per-case filtering on CO.
    public static readonly SeedScenario CoMixed = new("mixed-co", UserIalLevel.IAL1plus);
    // CO control: single-case SummerEbt Active — replacement denied (Active not in allowlist),
    // address update allowed. Baseline for Stop 10 of the walkthrough.
    public static readonly SeedScenario CoActive = new("active-co", UserIalLevel.IAL1plus);

    /// <summary>
    /// Scenarios that are seeded as User entities in the database.
    /// </summary>
    public static readonly IReadOnlyList<SeedScenario> UserScenarios =
    [
        CoLoaded, Verified, SingleChild, LargeFamily, Expired,
        NonCoLoaded, NotStarted, Pending, Minimal, Denied,
        Review, Cancelled, Unknown,
        Simple1, Simple2, Simple3, Simple4, Simple5, Simple6, Simple7,
        LostCard, Mixed,
        CoNotActivated, CoDeactivatedByState, CoUndeliverable, CoMixed, CoActive
    ];

    /// <summary>
    /// Scenarios that should only be seeded when STATE=dc.
    /// </summary>
    public static readonly IReadOnlySet<SeedScenario> DcOnlyScenarios =
        new HashSet<SeedScenario> { Simple1, Simple2, Simple3, Simple4, Simple5, Simple6, Simple7, LostCard, Mixed };

    /// <summary>
    /// Scenarios that should only be seeded when STATE=co.
    /// </summary>
    public static readonly IReadOnlySet<SeedScenario> CoOnlyScenarios =
        new HashSet<SeedScenario> { CoNotActivated, CoDeactivatedByState, CoUndeliverable, CoMixed, CoActive };

    /// <summary>
    /// All scenarios including household-only entries (e.g., MultipleApps).
    /// </summary>
    public static readonly IReadOnlyList<SeedScenario> AllScenarios =
        [.. UserScenarios, MultipleApps];
}
