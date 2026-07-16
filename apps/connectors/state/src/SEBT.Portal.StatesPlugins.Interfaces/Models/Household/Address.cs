namespace SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

/// <summary>
/// Represents an address on file for a household.
/// </summary>
public class Address
{
    public string? StreetAddress1 { get; set; }
    public string? StreetAddress2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
}
