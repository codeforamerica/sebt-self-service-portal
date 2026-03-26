using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.UseCases.Household;

namespace SEBT.Portal.Tests.Unit.UseCases.Household;

public class UpdateAddressCommandHandlerTests
{
    private readonly IValidator<UpdateAddressCommand> _validator =
        new DataAnnotationsValidator<UpdateAddressCommand>(null!);
    private readonly IHouseholdIdentifierResolver _resolver =
        Substitute.For<IHouseholdIdentifierResolver>();
    private readonly NullLogger<UpdateAddressCommandHandler> _logger =
        NullLogger<UpdateAddressCommandHandler>.Instance;

    private UpdateAddressCommandHandler CreateHandler() =>
        new(_validator, _resolver, _logger);

    private static ClaimsPrincipal CreateUser(string email)
    {
        var claims = new List<Claim> { new(ClaimTypes.Email, email) };
        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }

    private static UpdateAddressCommand CreateValidCommand(ClaimsPrincipal? user = null) =>
        new()
        {
            User = user ?? CreateUser("user@example.com"),
            StreetAddress1 = "123 Main St NW",
            City = "Washington",
            State = "District of Columbia",
            PostalCode = "20001"
        };

    // --- Validation tests ---

    [Fact]
    public async Task Handle_ReturnsValidationFailed_WhenStreetAddressIsMissing()
    {
        var handler = CreateHandler();
        var command = new UpdateAddressCommand
        {
            User = CreateUser("user@example.com"),
            StreetAddress1 = "",
            City = "Washington",
            State = "District of Columbia",
            PostalCode = "20001"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<ValidationFailedResult>(result);
    }

    [Fact]
    public async Task Handle_ReturnsValidationFailed_WhenCityIsMissing()
    {
        var handler = CreateHandler();
        var command = new UpdateAddressCommand
        {
            User = CreateUser("user@example.com"),
            StreetAddress1 = "123 Main St NW",
            City = "",
            State = "District of Columbia",
            PostalCode = "20001"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<ValidationFailedResult>(result);
    }

    [Fact]
    public async Task Handle_ReturnsValidationFailed_WhenStreetAddressIsWhitespaceOnly()
    {
        var handler = CreateHandler();
        var command = new UpdateAddressCommand
        {
            User = CreateUser("user@example.com"),
            StreetAddress1 = "   ",
            City = "Washington",
            State = "District of Columbia",
            PostalCode = "20001"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<ValidationFailedResult>(result);
    }

    [Fact]
    public async Task Handle_ReturnsValidationFailed_WhenCityIsWhitespaceOnly()
    {
        var handler = CreateHandler();
        var command = new UpdateAddressCommand
        {
            User = CreateUser("user@example.com"),
            StreetAddress1 = "123 Main St NW",
            City = "   ",
            State = "District of Columbia",
            PostalCode = "20001"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<ValidationFailedResult>(result);
    }

    [Fact]
    public async Task Handle_ReturnsValidationFailed_WhenStateIsWhitespaceOnly()
    {
        var handler = CreateHandler();
        var command = new UpdateAddressCommand
        {
            User = CreateUser("user@example.com"),
            StreetAddress1 = "123 Main St NW",
            City = "Washington",
            State = "   ",
            PostalCode = "20001"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<ValidationFailedResult>(result);
    }

    [Fact]
    public async Task Handle_ReturnsValidationFailed_WhenPostalCodeIsInvalid()
    {
        var handler = CreateHandler();
        var command = new UpdateAddressCommand
        {
            User = CreateUser("user@example.com"),
            StreetAddress1 = "123 Main St NW",
            City = "Washington",
            State = "District of Columbia",
            PostalCode = "ABCDE"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<ValidationFailedResult>(result);
    }

    [Fact]
    public async Task Handle_AcceptsNineDigitZipCode()
    {
        var handler = CreateHandler();
        var user = CreateUser("user@example.com");
        var command = new UpdateAddressCommand
        {
            User = user,
            StreetAddress1 = "123 Main St NW",
            City = "Washington",
            State = "District of Columbia",
            PostalCode = "20001-1234"
        };

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(HouseholdIdentifier.Email(EmailNormalizer.Normalize("user@example.com")));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    // --- Authorization tests ---

    [Fact]
    public async Task Handle_ReturnsUnauthorized_WhenHouseholdIdentifierCannotBeResolved()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns((HouseholdIdentifier?)null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<UnauthorizedResult>(result);
    }

    // --- Success tests ---

    [Fact]
    public async Task Handle_ReturnsSuccess_WhenValidCommandAndIdentifierResolved()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(HouseholdIdentifier.Email(EmailNormalizer.Normalize("user@example.com")));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.IsType<SuccessResult>(result);
    }

    [Fact]
    // TODO: Assert StreetAddress2 persisted when DC-160 lands
    public async Task Handle_ReturnsSuccess_WhenOptionalStreetAddress2IsProvided()
    {
        var handler = CreateHandler();
        var user = CreateUser("user@example.com");
        var command = new UpdateAddressCommand
        {
            User = user,
            StreetAddress1 = "123 Main St NW",
            StreetAddress2 = "Apt 4B",
            City = "Washington",
            State = "District of Columbia",
            PostalCode = "20001"
        };

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(HouseholdIdentifier.Email(EmailNormalizer.Normalize("user@example.com")));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    // --- Cancellation token propagation ---

    [Fact]
    public async Task Handle_PassesCancellationTokenToResolver()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();
        var cts = new CancellationTokenSource();
        var token = cts.Token;

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), token)
            .Returns(HouseholdIdentifier.Email(EmailNormalizer.Normalize("user@example.com")));

        await handler.Handle(command, token);

        await _resolver.Received(1).ResolveAsync(Arg.Any<ClaimsPrincipal>(), token);
    }

    // --- Resolver not called when validation fails ---

    [Fact]
    public async Task Handle_DoesNotCallResolver_WhenValidationFails()
    {
        var handler = CreateHandler();
        var command = new UpdateAddressCommand
        {
            User = CreateUser("user@example.com"),
            StreetAddress1 = "",
            City = "",
            State = "",
            PostalCode = ""
        };

        await handler.Handle(command, CancellationToken.None);

        await _resolver.DidNotReceive()
            .ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>());
    }
}
