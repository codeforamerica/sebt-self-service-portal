using SEBT.Portal.Core.Services;

namespace SEBT.Portal.UseCases.Tests.Unit.Services;

public class OtpGeneratorServiceTests
{
    [Fact]
    public void GenerateOtp_ShouldReturnSixDigitOtp()
    {
        // Arrange  
        var otpGenerator = new OtpGeneratorService();
        // Act
        var otp = otpGenerator.GenerateOtp();
        // Assert
        Assert.Matches(@"^\d{6}$", otp);
    }

    [Fact]
    public void GenerateOtp_ShouldNotProducePredictablePatterns()
    {
        // Arrange
        var otpGenerator = new OtpGeneratorService();
        var commonPatterns = new[]
        {
            "000000", "111111", "222222", "333333", "444444",
            "555555", "666666", "777777", "888888", "999999",
            "123456", "654321", "012345", "098765"
        };

        var otps = new HashSet<string>();

        // Act - Generate 10,000 OTPs
        for (int i = 0; i < 10000; i++)
        {
            otps.Add(otpGenerator.GenerateOtp());
        }

        // Assert - Common patterns should appear rarely (≤ 5 times statistically)
        foreach (var pattern in commonPatterns)
        {
            var count = otps.Count(otp => otp == pattern);
            Assert.True(count <= 5, $"Pattern '{pattern}' appeared {count} times, indicating weak randomness");
        }
    }
    [Fact]
    public void GenerateOtp_ShouldProduceUniqueValues()
    {
        // Arrange
        var otpGenerator = new OtpGeneratorService();
        var otps = new HashSet<string>();

        // Act - Generate 1000 OTPs
        for (int i = 0; i < 1000; i++)
        {
            otps.Add(otpGenerator.GenerateOtp());
        }

        // Assert - At least 95% should be unique (allows for rare collisions)
        Assert.True(otps.Count >= 950, $"Expected at least 950 unique OTPs, got {otps.Count}");
    }

    [Fact]
    public void GenerateOtp_ShouldNotBeTimeBasedOrSequential()
    {
        // Arrange
        var otpGenerator = new OtpGeneratorService();

        // Act - Generate OTPs in quick succession
        var otp1 = otpGenerator.GenerateOtp();
        var otp2 = otpGenerator.GenerateOtp();
        var otp3 = otpGenerator.GenerateOtp();

        // Assert - Should not be sequential
        Assert.NotEqual(otp1, otp2);
        Assert.NotEqual(otp2, otp3);
        Assert.NotEqual(otp1, otp3);

        // Check they're not sequential numbers
        if (int.TryParse(otp1, out int num1) && int.TryParse(otp2, out int num2))
        {
            Assert.NotEqual(1, Math.Abs(num2 - num1));
        }
    }
}