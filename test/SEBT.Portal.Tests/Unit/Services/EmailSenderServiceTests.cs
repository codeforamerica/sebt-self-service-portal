namespace SEBT.Portal.Tests.Unit.Services;

public class EmailSenderServiceTests
{
    [Fact]
    public async Task SendEmailAsync_WithValidParams_ShouldSendEmailSuccessfully()
    {
        // Arrange
        var emailSenderService = new EmailSenderService();
        var to = "jon@example.com";
        var subject = "Test Subject";
        var body = "This is a test email body.";

        // Act
        var sendEmailResult = emailSenderService.SendEmailAsync(to, subject, body);

        // Assert
        Assert.True(sendEmailResult.IsCompletedSuccessfully);
    }
}   