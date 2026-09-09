namespace SEBT.Portal.Core.Models.Household;

/// <summary>
/// A typed identifier used to look up and authorize access to household data.
/// The type is determined by state configuration (e.g. Email, SNAP ID); the value comes from the authenticated user.
/// </summary>
/// <param name="Type">The kind of identifier (e.g. Email, SnapId).</param>
/// <param name="Value">The identifier value (normalized for lookups).</param>
public sealed record HouseholdIdentifier(PreferredHouseholdIdType Type, string Value)
{
    /// <summary>
    /// Creates an identifier with <see cref="PreferredHouseholdIdType.Email"/>.
    /// </summary>
    public static HouseholdIdentifier Email(string value) => new(PreferredHouseholdIdType.Email, value);

    /// <summary>
    /// Creates an identifier with <see cref="PreferredHouseholdIdType.Phone"/>.
    /// </summary>
    public static HouseholdIdentifier Phone(string value) => new(PreferredHouseholdIdType.Phone, value);

    /// <summary>
    /// Creates an identifier with <see cref="PreferredHouseholdIdType.SnapId"/>.
    /// </summary>
    public static HouseholdIdentifier SnapId(string value) => new(PreferredHouseholdIdType.SnapId, value);

    /// <summary>
    /// Creates an identifier with <see cref="PreferredHouseholdIdType.TanfId"/>.
    /// </summary>
    public static HouseholdIdentifier TanfId(string value) => new(PreferredHouseholdIdType.TanfId, value);

    /// <summary>
    /// Creates an identifier with <see cref="PreferredHouseholdIdType.Ssn"/>.
    /// </summary>
    public static HouseholdIdentifier Ssn(string value) => new(PreferredHouseholdIdType.Ssn, value);
}
