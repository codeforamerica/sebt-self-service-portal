namespace Sebt.Portal.Core.Models.Auth
{
    /// <summary>
    /// Represents a one-time password (OTP) code used for authentication purposes.
    /// </summary>
    public record OtpCode
    {
        public required string Code { get; init; }
        public required DateTime ExpiresAt { get; init; }
        public required string Email { get; init; }
        /// <summary>
        /// Initializes a new instance of the <see cref="OtpCode"/> class with the specified code, email, and validity duration.
        /// </summary>
        /// <param name="code">The OTP code.</param>
        /// <param name="email">The email address associated with the OTP code.</param>
        /// <param name="validityMinutes">The duration in minutes for which the OTP code is valid. Default is 10 minutes.</param>
        public OtpCode(string code, string email, int validityMinutes = 10)
        {
            Code = code;
            Email = email;
            ExpiresAt = DateTime.UtcNow.AddMinutes(validityMinutes);
        }
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