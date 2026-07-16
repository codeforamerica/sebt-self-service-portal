using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Configuration for per-state self-service action rules.
/// Controls which portal actions (address update, card replacement) are available
/// based on the user's program type (issuance type) and card status.
///
/// Per-state overrides go in <c>appsettings.{State}.json</c> under the <c>SelfServiceRules</c> section.
///
/// <example>
/// DC configuration (allows SummerEbt users only):
/// <code>
/// "SelfServiceRules": {
///   "AddressUpdate": {
///     "Enabled": true,
///     "ByIssuanceType": {
///       "SummerEbt": { "Enabled": true, "AllowedCardStatuses": ["Active", "Mailed"] }
///     }
///   }
/// }
/// </code>
/// </example>
/// </summary>
public class SelfServiceRulesSettings
{
    public static readonly string SectionName = "SelfServiceRules";

    /// <summary>
    /// Rules for portal address update actions.
    /// </summary>
    public ActionRuleSettings AddressUpdate { get; set; } = new();

    /// <summary>
    /// Rules for portal card replacement request actions.
    /// </summary>
    public ActionRuleSettings CardReplacement { get; set; } = new();
}

/// <summary>
/// Rules for a single self-service action (e.g., address update or card replacement).
/// </summary>
public class ActionRuleSettings
{
    /// <summary>
    /// Top-level toggle for this action at the state level.
    /// When false, the action is disabled regardless of issuance type or card status.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Optional i18n key for the message shown when this action is denied.
    /// The frontend maps this key to a localized string via the translation system.
    /// </summary>
    public string? DisabledMessageKey { get; set; }

    /// <summary>
    /// Per-issuance-type rules. Keys are <see cref="IssuanceType"/> enum names
    /// (e.g., "SummerEbt", "TanfEbtCard", "SnapEbtCard", "Unknown").
    /// Issuance types not present in this dictionary are denied by default.
    /// </summary>
    public Dictionary<IssuanceType, IssuanceTypeRuleSettings> ByIssuanceType { get; set; } = new();
}

/// <summary>
/// Rules for a specific issuance type within an action.
/// </summary>
public class IssuanceTypeRuleSettings
{
    /// <summary>
    /// Whether this issuance type is permitted for the parent action.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Card statuses for which the action is allowed.
    /// If empty and <see cref="Enabled"/> is true, the action is allowed regardless of card status.
    /// </summary>
    public List<CardStatus> AllowedCardStatuses { get; set; } = new();

    /// <summary>
    /// Case (application) statuses for which the action is allowed.
    /// If empty and <see cref="Enabled"/> is true, the action is allowed regardless of case status.
    /// Combined with <see cref="AllowedCardStatuses"/> using AND semantics: both dimensions
    /// must match for an application to be eligible. An empty list effectively skips that dimension.
    /// </summary>
    public List<ApplicationStatus> AllowedCaseStatuses { get; set; } = new();
}
