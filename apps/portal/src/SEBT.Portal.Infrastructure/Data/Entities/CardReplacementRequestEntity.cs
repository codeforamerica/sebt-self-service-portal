namespace SEBT.Portal.Infrastructure.Data.Entities;

/// <summary>
/// Records a card replacement request for cooldown enforcement.
/// Household and case identifiers are stored as HMAC-SHA256 hashes (via IIdentifierHasher)
/// because only lookup-by-hash is needed — original values are never retrieved.
/// </summary>
public class CardReplacementRequestEntity
{
    /// <summary>
    /// Primary key (UUIDv7, application-generated).
    /// </summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// HMAC-SHA256 hash of the household identifier value.
    /// 64-character hex string produced by <see cref="Core.Services.IIdentifierHasher"/>.
    /// </summary>
    public string HouseholdIdentifierHash { get; set; } = string.Empty;

    /// <summary>
    /// HMAC-SHA256 hash of the Summer EBT case ID.
    /// 64-character hex string produced by <see cref="Core.Services.IIdentifierHasher"/>.
    /// </summary>
    public string CaseIdHash { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the card replacement was requested.
    /// </summary>
    public DateTime RequestedAt { get; set; }

    /// <summary>
    /// Foreign key to the user who made the request. Audit trail only.
    /// </summary>
    public Guid RequestedByUserId { get; set; }

    /// <summary>
    /// Navigation property to the requesting user.
    /// </summary>
    public UserEntity? RequestedByUser { get; set; }
}
