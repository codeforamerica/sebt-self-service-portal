namespace SEBT.Portal.Infrastructure.Data.Entities;

/// <summary>
/// Entity model for storing address information for a household.
/// </summary>
public class AddressEntity
{
    /// <summary>
    /// Primary key identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The ID of the household (foreign key to HouseholdEntity).
    /// </summary>
    public int HouseholdId { get; set; }

    /// <summary>
    /// The street address line 1.
    /// </summary>
    public string? StreetAddress1 { get; set; }

    /// <summary>
    /// The street address line 2 (apartment, suite, etc.).
    /// </summary>
    public string? StreetAddress2 { get; set; }

    /// <summary>
    /// The city.
    /// </summary>
    public string? City { get; set; }

    /// <summary>
    /// The state or province.
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// The postal or ZIP code.
    /// </summary>
    public string? PostalCode { get; set; }

    /// <summary>
    /// Navigation property to the household.
    /// </summary>
    public HouseholdEntity? Household { get; set; }
}
