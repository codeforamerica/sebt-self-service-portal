namespace SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

/// <summary>
/// Result of an address update operation from a state connector.
/// Distinguishes success, policy rejection (household not eligible), and backend errors.
/// </summary>
public class AddressUpdateResult
{
    /// <summary>Whether the address was successfully persisted.</summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Whether the failure is a policy rejection (household not eligible for portal address updates)
    /// rather than a technical backend error.
    /// </summary>
    public bool IsPolicyRejection { get; init; }

    /// <summary>Machine-readable error code for frontend/analytics consumption.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>Human-readable error message.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>The address was persisted successfully.</summary>
    public static AddressUpdateResult Success() =>
        new() { IsSuccess = true };

    /// <summary>The household is not eligible to update their address via the portal.</summary>
    public static AddressUpdateResult PolicyRejected(string code, string message) =>
        new() { IsSuccess = false, IsPolicyRejection = true, ErrorCode = code, ErrorMessage = message };

    /// <summary>The state backend returned an error.</summary>
    public static AddressUpdateResult BackendError(string code, string message) =>
        new() { IsSuccess = false, IsPolicyRejection = false, ErrorCode = code, ErrorMessage = message };
}
