using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Sebt.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.UseCases.Auth;

namespace SEBT.Portal.UseCases.Tests.Unit;
public class ValidateOtpCommandHandlerTests
{
    private readonly IOtpRepository otpRepository = Substitute.For<IOtpRepository>();
    private readonly NullLogger<ValidateOtpCommandHandler> logger = NullLogger<ValidateOtpCommandHandler>.Instance;
    private readonly IValidator<ValidateOtpCommand> validator = new DataAnnotationsValidator<ValidateOtpCommand>(null!);
    private readonly ValidateOtpCommandHandler handler;
    public ValidateOtpCommandHandlerTests()
    {
        handler = new ValidateOtpCommandHandler(
            otpRepository,
            validator,
            logger);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessResult_WhenUnexpiredOtpAndEmailMatch()
    {
        // Arrange
        var command = new ValidateOtpCommand
        {
            Email = "jim@example.com",
            Otp = "123456"
        };

        otpRepository.GetOtpCodeByEmailAsync(Arg.Any<string>())
            .Returns(new Sebt.Portal.Core.Models.Auth.OtpCode(command.Otp, command.Email));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
    }
    [Fact]
    public async Task Handle_ShouldReturnValidattionFailure_WhenOtpIsExpired()
    {
        // Arrange
        var command = new ValidateOtpCommand
        {
            Email = "jim@example.com",
            Otp = "123456"
        };

        otpRepository.GetOtpCodeByEmailAsync(Arg.Any<string>())
            .Returns(new Sebt.Portal.Core.Models.Auth.OtpCode(command.Otp, command.Email)
            {
                ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
            });

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        var failedResult = Assert.IsType<ValidationFailedResult>(result);

    }

    [Fact]
    public async Task Handle_ShouldReturnValidattionFailure_WhenOtpDoesNotMatchEmail()
    {
        // Arrange
        var command = new ValidateOtpCommand
        {
            Email = "jim@example.com",
            Otp = "123456"
        };

        otpRepository.GetOtpCodeByEmailAsync(Arg.Any<string>())
            .Returns(new OtpCode("", ""));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        var failedResult = Assert.IsType<ValidationFailedResult>(result);
    }
    [Fact]
    public async Task Handle_ShouldReturnValidattionFailure_WhenOtpDoesNotExist()
    {
        // Arrange
        var command = new ValidateOtpCommand
        {
            Email = "jim@example.com",
            Otp = "123456"
        };

        otpRepository.GetOtpCodeByEmailAsync(Arg.Any<string>())
            .ThrowsAsync(new KeyNotFoundException());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        var failedResult = Assert.IsType<ValidationFailedResult>(result);
    }
}
