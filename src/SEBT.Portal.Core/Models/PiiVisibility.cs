namespace SEBT.Portal.Core.Models;

/// <summary>
/// Indicates which PII data elements a user is allowed to view.
/// </summary>
/// <param name="IncludeAddress">Whether the user can view address information.</param>
/// <param name="IncludeEmail">Whether the user can view email information.</param>
/// <param name="IncludePhone">Whether the user can view phone information.</param>
public record PiiVisibility(bool IncludeAddress, bool IncludeEmail, bool IncludePhone);
