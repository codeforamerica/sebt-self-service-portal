namespace SEBT.Portal.Core.Models.Household;

/// <summary>
/// Types of identifiers that can be used to authorize guardians and link them to household data.
/// Configurable per state; a state may support one or more types (e.g. Email today, SNAP ID later).
/// </summary>
public enum PreferredHouseholdIdType
{
    /// <summary>Email address.</summary>
    Email = 0,

    /// <summary>Phone number.</summary>
    Phone = 1,

    /// <summary>SNAP (Supplemental Nutrition Assistance Program) case/client ID.</summary>
    SnapId = 2,

    /// <summary>TANF (Temporary Assistance for Needy Families) case/client ID.</summary>
    TanfId = 3,

    /// <summary>Social Security Number (last 4 or full, per state policy).</summary>
    Ssn = 4
}
