using SEBT.Portal.Core.Services;

public class OtpGeneratorService:IOtpGeneratorService
{
    private readonly Random _random = Random.Shared;
    public string GenerateOtp()
    {
        // Simple OTP generation logic (6-digit numeric code)
        return _random.Next(100000, 1000000).ToString();
    }
}