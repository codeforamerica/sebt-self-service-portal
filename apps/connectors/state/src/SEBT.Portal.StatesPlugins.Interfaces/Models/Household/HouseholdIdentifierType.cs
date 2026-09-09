namespace SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

/// <summary>
/// Type of identifier used to look up household data.
/// Aligns with portal Core.PreferredHouseholdIdType; used at the plugin boundary.
/// </summary>
public enum HouseholdIdentifierType
{
    /// <summary>Email address.</summary>
    Email = 0,

    /// <summary>Phone number (e.g. from IdP claims; not persisted for some states).</summary>
    Phone = 1,

    /// <summary>SNAP case/client ID.</summary>
    SnapId = 2,

    /// <summary>TANF case/client ID.</summary>
    TanfId = 3,

    /// <summary>SSN or last-4 (per state policy).</summary>
    Ssn = 4
}
