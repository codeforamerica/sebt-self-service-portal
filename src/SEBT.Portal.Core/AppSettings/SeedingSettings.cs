namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Configuration for database seeding behavior.
/// </summary>
public class SeedingSettings
{
    public static readonly string SectionName = "Seeding";

    /// <summary>
    /// Format string for seed user emails. Use {0} as placeholder for the scenario name.
    /// Default: "{0}@example.com" (e.g., "co-loaded@example.com").
    /// For deployed environments, set to something like "sebt.dc+{0}@codeforamerica.org".
    /// </summary>
    public string EmailPattern { get; set; } = "{0}@example.com";

    /// <summary>
    /// When true, database seeding runs even outside the Development environment.
    /// Default: false (seeding only runs when ASPNETCORE_ENVIRONMENT is Development).
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Constructs a full email address from a scenario name using the configured pattern.
    /// </summary>
    /// <param name="scenarioName">The scenario name (e.g., "co-loaded", "verified").</param>
    /// <returns>The full email address.</returns>
    public string BuildEmail(string scenarioName) =>
        string.Format(EmailPattern, scenarioName);
}
