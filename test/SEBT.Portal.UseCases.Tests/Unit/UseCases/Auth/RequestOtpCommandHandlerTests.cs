using SEBT.Portal.Core.Models.Results;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.UseCases.Auth;

public class RequestOtpCommandHandlerTests
{
    private readonly RequestOtpCommandHandler _handler;

    public RequestOtpCommandHandlerTests()
    {
        var validator = new RequestOtpCommandValidator(new DataAnnotationsValidator<RequestOtpCommand>(null!));
        _handler = new RequestOtpCommandHandler(validator);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccessResultWithSixDigitOtp_WhenEmailIsValid()
    {
        // Arrange
        var command = new RequestOtpCommand { Email = "user@example.com" };
        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var succcessResult = Assert.IsType<SuccessResult<RequestOtpCommandResult>>(result);
        Assert.True(succcessResult.Value.OtpReference.Length == 6);
    }

    [Fact]
    public async Task Handle_ShouldReturnFailure_WhenEmailIsInvalid()
    {
        // Arrange
        var command = new RequestOtpCommand { Email = "user@" };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        var failedResult = Assert.IsType<ValidationFailedResult<RequestOtpCommandResult>>(result);
        Assert.Contains("Invalid email format.", failedResult.Errors.Select(e => e.Message));
    }
}