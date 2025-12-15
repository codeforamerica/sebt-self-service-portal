using System.Security.Cryptography;

namespace Sebt.Portal.Core.Models.Auth
{
    /// <summary>
    /// Represents a one-time password (OTP) code used for authentication purposes.
    /// </summary>
    public record OtpCode(string Code, string Email, int MinutesToExpire = 10)
    {
        public DateTime ExpiresAt { get; init; } = DateTime.UtcNow.AddMinutes(MinutesToExpire);
        /// <summary>
        /// Validates the provided OTP code against the stored code.
        /// </summary>
        /// <param name="code">The OTP code to validate.</param>
        /// <returns>Returns <c>true</c> if the provided code matches the stored code and is not expired; otherwise, <c>false</c>.</returns>
        public bool IsCodeValid(string code)
        {
            return CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(Code),
                System.Text.Encoding.UTF8.GetBytes(code)
            ) && DateTime.UtcNow <= ExpiresAt;
        }
    }
}
