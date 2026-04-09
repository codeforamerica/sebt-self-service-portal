namespace SEBT.Portal.UseCases.Auth
{
    /// <summary>
    /// Result of a successful OTP validation, containing the JWT token and
    /// whether the user needs to complete ID proofing before accessing the portal.
    /// </summary>
    /// <param name="Token">The JWT authentication token.</param>
    /// <param name="RequiresIdProofing">
    /// True if the user must complete ID proofing (non-co-loaded, proofing not yet completed).
    /// False if the user can proceed directly to the dashboard.
    /// </param>
    public record ValidateOtpResult(string Token, bool RequiresIdProofing);
}
