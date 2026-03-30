using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Core.Models.AddressUpdate;

/// <summary>
/// Canonical input for shared address validation and normalization (portal handlers and state connectors).
/// Carries optional identifiers and metadata so callers can correlate requests without coupling to HTTP.
/// </summary>
public sealed record AddressUpdateOperationRequest
{
    /// <summary>Line-one street address or USPS-style General Delivery line.</summary>
    public required string StreetAddress1 { get; init; }

    /// <summary>Optional secondary line (unit, suite, etc.).</summary>
    public string? StreetAddress2 { get; init; }

    /// <summary>City or USPS post office location for General Delivery.</summary>
    public required string City { get; init; }

    /// <summary>US state name or two-letter abbreviation.</summary>
    public required string State { get; init; }

    /// <summary>US ZIP or ZIP+4.</summary>
    public required string PostalCode { get; init; }

    /// <summary>Optional end-to-end correlation id (logging, support).</summary>
    public string? CorrelationId { get; init; }

    /// <summary>Optional state case or eligibility system identifier for the household.</summary>
    public string? HouseholdExternalId { get; init; }

    /// <summary>Optional opaque key/value metadata (connector-specific; not sent to Smarty).</summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    /// <summary>
    /// Builds a request from the domain <see cref="Household.Address"/> model.
    /// </summary>
    public static AddressUpdateOperationRequest FromHouseholdAddress(Address address) =>
        new()
        {
            StreetAddress1 = address.StreetAddress1 ?? string.Empty,
            StreetAddress2 = address.StreetAddress2,
            City = address.City ?? string.Empty,
            State = address.State ?? string.Empty,
            PostalCode = address.PostalCode ?? string.Empty
        };
}
