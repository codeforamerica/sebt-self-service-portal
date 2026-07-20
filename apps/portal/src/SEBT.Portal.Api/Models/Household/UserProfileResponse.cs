namespace SEBT.Portal.Api.Models.Household;

/// <summary>
/// API response model for the logged-in user's profile name
/// </summary>
public record UserProfileResponse
{
    /// <summary>
    /// The user's first name.
    /// </summary>
    public string FirstName { get; init; } = string.Empty;

    /// <summary>
    /// The user's middle name, if any.
    /// </summary>
    public string? MiddleName { get; init; }

    /// <summary>
    /// The user's last name.
    /// </summary>
    public string LastName { get; init; } = string.Empty;
}
