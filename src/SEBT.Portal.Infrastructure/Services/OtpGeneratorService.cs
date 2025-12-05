using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Infrastructure.Services
{    /// <summary>
     /// Service responsible for generating One-Time Passwords (OTP).
     /// </summary>
    public class OtpGeneratorService : IOtpGeneratorService
    {
        /// <summary>
        /// A shared instance of the Random class used for generating random numbers.
        /// </summary>
        private readonly Random random = Random.Shared;

        /// <summary>
        /// Generates a 6-digit numeric One-Time Password (OTP).
        /// </summary>
        /// <returns>A string representation of a randomly generated 6-digit numeric code.</returns>
        public string GenerateOtp()
        {
            // Simple OTP generation logic (6-digit numeric code)
            return random.Next(100000, 1000000).ToString();
        }
    }
}