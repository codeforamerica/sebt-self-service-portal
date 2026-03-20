namespace SEBT.Portal.Api;

/// <summary>
/// Constants for rate-limit policy names used across controllers and Program.cs configuration.
/// </summary>
internal static class RateLimitPolicies
{
    public const string Otp = "otp-policy";
    public const string EnrollmentCheck = "enrollment-check-policy";
}
