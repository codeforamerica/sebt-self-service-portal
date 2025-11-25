namespace Sebt.Portal.Core.Models.Auth
{
    /// <summary>
    /// Represents a one-time password (OTP) code used for authentication purposes.
    /// </summary>
    public record OtpCode(string Code, string Email, int MinutessToExpire = 10)
    {
        public DateTime ExpiresAt { get; init; } = DateTime.UtcNow.AddMinutes(MinutessToExpire);
        /// <summary>
        /// Validates the provided code against the stored code and checks if it is still valid based on expiration time.
        /// </summary>
        /// <param name="code">The OTP code to validate.</param>
        /// <returns>Returns <c>true</c> if the provided code matches the stored code and is not expired; otherwise, <c>false</c>.</returns>
        public bool IsCodeValid(string code)
        {
            return string.Equals(Code, code, StringComparison.CurrentCultureIgnoreCase) && DateTime.UtcNow <= ExpiresAt;
        }
    }
}