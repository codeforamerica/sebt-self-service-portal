namespace SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

/// <summary>
/// Result of a card replacement request from a state connector.
/// Distinguishes success, policy rejection (household not eligible), and backend errors.
/// </summary>
public class CardReplacementResult
{
    /// <summary>Whether the replacement was successfully initiated.</summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Whether the failure is a policy rejection (household not eligible for portal card replacement)
    /// rather than a technical backend error.
    /// </summary>
    public bool IsPolicyRejection { get; init; }

    /// <summary>Machine-readable error code for frontend/analytics consumption.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>Human-readable error message.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>The replacement was initiated successfully.</summary>
    public static CardReplacementResult Success() =>
        new() { IsSuccess = true };

    /// <summary>The household is not eligible to request a replacement via the portal.</summary>
    public static CardReplacementResult PolicyRejected(string code, string message) =>
        new() { IsSuccess = false, IsPolicyRejection = true, ErrorCode = code, ErrorMessage = message };

    /// <summary>The state backend returned an error.</summary>
    public static CardReplacementResult BackendError(string code, string message) =>
        new() { IsSuccess = false, IsPolicyRejection = false, ErrorCode = code, ErrorMessage = message };
}
