namespace SEBT.Portal.StatesPlugins.Interfaces.Models;

/// <summary>
/// Indicates which PII data elements the caller is allowed to receive.
/// Used when fetching household data so the plugin can optionally filter or the portal can filter at the boundary.
/// </summary>
/// <param name="IncludeAddress">Whether address information may be included.</param>
/// <param name="IncludeEmail">Whether email may be included.</param>
/// <param name="IncludePhone">Whether phone may be included.</param>
public record PiiVisibility(bool IncludeAddress, bool IncludeEmail, bool IncludePhone);
