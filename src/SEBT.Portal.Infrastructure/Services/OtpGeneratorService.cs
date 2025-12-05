using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Infrastructure.Services
{
    public class OtpGeneratorService:IOtpGeneratorService
    {
        private readonly Random random = Random.Shared;
        public string GenerateOtp()
        {
            // Simple OTP generation logic (6-digit numeric code)
            return random.Next(100000, 1000000).ToString();
        }
    }
}