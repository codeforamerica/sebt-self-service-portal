namespace SEBT.Portal.Core.Models.Household;

/// <summary>
/// Represents the logged-in user's profile name for display
/// </summary>
public class UserProfile
{
    /// <summary>
    /// The user's first name.
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// The user's middle name, if any.
    /// </summary>
    public string? MiddleName { get; set; }

    /// <summary>
    /// The user's last name.
    /// </summary>
    public string LastName { get; set; } = string.Empty;
}
