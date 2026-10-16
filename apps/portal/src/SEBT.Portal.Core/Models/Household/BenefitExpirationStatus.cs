using System.Text.Json.Serialization;

namespace SEBT.Portal.Core.Models.Household;

/// <summary>
/// Where a case's benefits stand relative to their expiration date.
/// Serialized as the member name over the API, so member identifiers are the wire contract.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BenefitExpirationStatus
{
    /// <summary>
    /// Benefits expire within the state's configured warning window
    /// (<c>SelfServiceRules:CardReplacement:DisableDaysBeforeExpiration</c>).
    /// </summary>
    Expiring = 0,

    /// <summary>Benefits can no longer be used: the expiration date has arrived or passed.</summary>
    Expired = 1
}
