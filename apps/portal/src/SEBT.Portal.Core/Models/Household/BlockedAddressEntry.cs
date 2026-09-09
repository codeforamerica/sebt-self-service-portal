namespace SEBT.Portal.Core.Models.Household;

/// <summary>
/// A single blocked-address record sourced from a state-specific data file.
/// <see cref="Street"/> is the unnormalized street form (line 1, or a synthesized
/// "PO BOX {n}" string when the record is a PO box only). <see cref="PostalCodeFive"/>
/// is the 5-digit ZIP, used to disambiguate same-street collisions across cities.
/// </summary>
public sealed record BlockedAddressEntry(string Street, string PostalCodeFive);
