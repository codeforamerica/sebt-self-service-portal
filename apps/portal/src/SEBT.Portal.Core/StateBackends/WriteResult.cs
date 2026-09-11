namespace SEBT.Portal.Core.StateBackends;

/// <summary>
/// Canonical write outcome (card replacement, address update): success, a policy rejection
/// (household not eligible), or a backend error.
/// </summary>
public sealed record WriteResult
{
    public bool IsSuccess { get; init; }

    /// <summary>The failure is a policy rejection rather than a technical backend error.</summary>
    public bool IsPolicyRejection { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static WriteResult Success() =>
        new() { IsSuccess = true };

    public static WriteResult PolicyRejected(string code, string message) =>
        new() { IsSuccess = false, IsPolicyRejection = true, ErrorCode = code, ErrorMessage = message };

    public static WriteResult BackendError(string code, string message) =>
        new() { IsSuccess = false, IsPolicyRejection = false, ErrorCode = code, ErrorMessage = message };
}
