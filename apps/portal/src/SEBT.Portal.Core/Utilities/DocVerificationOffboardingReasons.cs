namespace SEBT.Portal.Core.Utilities;

/// <summary>
/// Off-boarding reason query-param values for document verification failures.
/// Distinct values support analytics segmentation on the off-boarding route.
/// </summary>
public static class DocVerificationOffboardingReasons
{
    /// <summary>
    /// Standard DocV failure (document quality, workflow reject, etc.). User may retry ID proofing.
    /// </summary>
    public const string Failed = "docVerificationFailed";

    /// <summary>
    /// DocV failure with a configured egregious Socure reason code (tampering, liveness, etc.).
    /// User may retry ID proofing with different information.
    /// </summary>
    public const string EgregiousFailed = "docVerificationEgregiousFailed";
}
