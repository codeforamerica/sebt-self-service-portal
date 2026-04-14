using SEBT.Portal.Core.Models.Auth;

namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// State-configurable minimum IAL requirements based on case origin.
/// The service evaluates each case against these thresholds and returns the
/// highest required level across all cases ("highest wins").
/// </summary>
public class MinimumIalSettings
{
    public static readonly string SectionName = "MinimumIal";

    /// <summary>
    /// Minimum IAL required for cases that originated from guardian-submitted applications.
    /// Required — the app will fail to start if not configured in the state overlay.
    /// </summary>
    public IalLevel? ApplicationCases { get; set; }

    /// <summary>
    /// Minimum IAL required for streamline-certified cases that were co-loaded
    /// (bulk-imported from the state system).
    /// Required — the app will fail to start if not configured in the state overlay.
    /// </summary>
    public IalLevel? CoLoadedStreamlineCases { get; set; }

    /// <summary>
    /// Minimum IAL required for streamline-certified cases that were NOT co-loaded
    /// (added through the portal, not bulk-imported).
    /// Required — the app will fail to start if not configured in the state overlay.
    /// </summary>
    public IalLevel? NonCoLoadedStreamlineCases { get; set; }
}
