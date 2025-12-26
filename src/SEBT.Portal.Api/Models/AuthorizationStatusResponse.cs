namespace SEBT.Portal.Api.Models;

/// <summary>
/// Response model for authorization status check.
/// </summary>
/// <param name="IsAuthorized">Indicates whether the user is authorized.</param>
/// <param name="Email">The email address of the authenticated user, if available.</param>
public record AuthorizationStatusResponse(bool IsAuthorized, string? Email = null);

