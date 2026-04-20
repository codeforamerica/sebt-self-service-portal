namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Settings related to bypassing OTP validation for a specific email address in staging environment.
/// This is intended to facilitate SEBT's DAST scanning in staging, but should be used
/// with caution and should never be enabled in production or for any real user accounts.
/// </summary>
public static class OtpBypassSettings
{
    /// <summary>
    /// Email address for which OTP validation will be bypassed when criteria are met. Intended for use by SEBT's DAST scanner in staging only.
    /// </summary>
    public static readonly string Email = "dast-scanner@sebtportal.com";

    /// <summary>
    /// Fixed, well-known OTP code used by the DAST scanner to bypass validation.
    /// The value is intentionally predictable; safety is provided by the gating criteria
    /// (feature flag + staging environment + scanner-specific email).
    /// </summary>
    public static readonly string OtpCode = "123456";

    /// <summary>
    /// Feature flag name to enable OTP bypass. When enabled, requests that meet the bypass criteria will skip OTP validation.
    /// This should only be enabled in staging and should never be enabled in production.
    /// The bypass criteria are:
    /// 1. The feature flag named by <see cref="FeatureFlagName"/> is enabled
    /// 2. The application is running in the staging environment
    /// 3. The email in the request matches <see cref="Email"/>
    /// 4. The OTP code in the request matches <see cref="OtpCode"/>
    /// When all criteria are met, OTP validation will be bypassed, allowing testing of the login flow without needing to receive an OTP.
    /// This is intended to facilitate SEBT's DAST scanning in staging, but should be used with caution and should never be enabled in production or for any real user accounts.
    /// </summary>
    public static readonly string FeatureFlagName = "bypass_otp";
}
