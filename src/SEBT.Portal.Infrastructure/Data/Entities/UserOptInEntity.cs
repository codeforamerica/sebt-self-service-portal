namespace SEBT.Portal.Infrastructure.Data.Entities;

/// <summary>
/// Entity model for tracking user opt-ins for storing email and/or date of birth.
/// </summary>
public class UserOptInEntity
{
    /// <summary>
    /// Primary key identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The email address of the user.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Whether the user has opted in to storing their email address.
    /// </summary>
    public bool EmailOptIn
    {
        get => field;
        set
        {
            if (field == value) return;
            field = value;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Whether the user has opted in to storing their date of birth.
    /// </summary>
    public bool DobOptIn
    {
        get => field;
        set
        {
            if (field == value) return;
            field = value;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// When the opt-in record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the opt-in record was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
