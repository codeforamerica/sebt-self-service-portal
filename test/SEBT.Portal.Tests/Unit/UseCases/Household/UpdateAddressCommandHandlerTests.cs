using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.Core.Models.AddressUpdate;
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
    private readonly IAddressUpdateService _addressUpdate = Substitute.For<IAddressUpdateService>();
    private readonly IAddressValidationService _addressValidator =
        Substitute.For<IAddressValidationService>();
    private readonly NullLogger<UpdateAddressCommandHandler> _logger =
        NullLogger<UpdateAddressCommandHandler>.Instance;

    public UpdateAddressCommandHandlerTests()
    {
        // Default: address update service passes so existing tests aren't affected
        _addressUpdate
            .ValidateAndNormalizeAsync(Arg.Any<AddressUpdateOperationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                Result<AddressUpdateSuccess>.Success(
                    new AddressUpdateSuccess
                    {
                        NormalizedAddress = new Address
                        {
                            StreetAddress1 = "123 Main St NW",
                            City = "Washington",
                            State = "DC",
                            PostalCode = "20001"
                        },
                        WasCorrected = false,
                        IsGeneralDelivery = false
                    })));

        // Default: address validation passes so existing tests aren't affected
        _addressValidator.ValidateAsync(Arg.Any<Address>(), Arg.Any<CancellationToken>())
            .Returns(AddressValidationResult.Valid());
    }

    private UpdateAddressCommandHandler CreateHandler() =>
        new(_validator, _addressUpdate, _resolver, _addressValidator, _logger);

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
        Assert.IsType<ValidationFailedResult<AddressValidationResult>>(result);
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
        Assert.IsType<ValidationFailedResult<AddressValidationResult>>(result);
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
        Assert.IsType<ValidationFailedResult<AddressValidationResult>>(result);
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
        Assert.IsType<ValidationFailedResult<AddressValidationResult>>(result);
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
        Assert.IsType<ValidationFailedResult<AddressValidationResult>>(result);
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
        Assert.IsType<ValidationFailedResult<AddressValidationResult>>(result);
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
        Assert.IsType<UnauthorizedResult<AddressValidationResult>>(result);
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
        var success = Assert.IsType<SuccessResult<AddressValidationResult>>(result);
        Assert.True(success.Value.IsValid);
    }

    [Fact]
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

    [Fact]
    public async Task Handle_PassesCancellationTokenToAddressService()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();
        var cts = new CancellationTokenSource();
        var token = cts.Token;

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), token)
            .Returns(HouseholdIdentifier.Email(EmailNormalizer.Normalize("user@example.com")));

        await handler.Handle(command, token);

        await _addressUpdate.Received(1).ValidateAndNormalizeAsync(
            Arg.Any<AddressUpdateOperationRequest>(), token);
    }

    // --- Short-circuit when input validation fails ---

    [Fact]
    public async Task Handle_DoesNotCallResolver_WhenInputValidationFails()
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

    // --- Address validation integration ---

    [Fact]
    public async Task Handle_CallsAddressValidator_AfterIdentifierResolved()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(HouseholdIdentifier.Email(EmailNormalizer.Normalize("user@example.com")));

        await handler.Handle(command, CancellationToken.None);

        await _addressValidator.Received(1)
            .ValidateAsync(Arg.Any<Address>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DoesNotCallAddressValidator_WhenInputValidationFails()
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

        await _addressValidator.DidNotReceive()
            .ValidateAsync(Arg.Any<Address>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DoesNotCallAddressService_WhenInputValidationFails()
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

        await _addressUpdate.DidNotReceive()
            .ValidateAndNormalizeAsync(Arg.Any<AddressUpdateOperationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsSuccessWithInvalidResult_WhenAddressIsBlocked()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(HouseholdIdentifier.Email(EmailNormalizer.Normalize("user@example.com")));

        _addressValidator.ValidateAsync(Arg.Any<Address>(), Arg.Any<CancellationToken>())
            .Returns(AddressValidationResult.Invalid("This address cannot be used for mail delivery.", "blocked"));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var success = Assert.IsType<SuccessResult<AddressValidationResult>>(result);
        Assert.False(success.Value.IsValid);
        Assert.Equal("This address cannot be used for mail delivery.", success.Value.ErrorMessage);
    }

    [Fact]
    public async Task Handle_ReturnsSuccessWithSuggestion_WhenAddressHasSuggestion()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();
        var suggested = new Address
        {
            StreetAddress1 = "123 MLK Jr Ave NW",
            City = "Washington",
            State = "District of Columbia",
            PostalCode = "20001"
        };

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(HouseholdIdentifier.Email(EmailNormalizer.Normalize("user@example.com")));

        _addressValidator.ValidateAsync(Arg.Any<Address>(), Arg.Any<CancellationToken>())
            .Returns(AddressValidationResult.Suggestion(suggested, "abbreviated"));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var success = Assert.IsType<SuccessResult<AddressValidationResult>>(result);
        Assert.False(success.Value.IsValid);
        Assert.NotNull(success.Value.SuggestedAddress);
        Assert.Equal("123 MLK Jr Ave NW", success.Value.SuggestedAddress!.StreetAddress1);
    }

    [Fact]
    public async Task Handle_PassesCommandFieldsToAddressValidator()
    {
        var handler = CreateHandler();
        var command = new UpdateAddressCommand
        {
            User = CreateUser("user@example.com"),
            StreetAddress1 = "456 Oak Ave NE",
            StreetAddress2 = "Suite 100",
            City = "Washington",
            State = "District of Columbia",
            PostalCode = "20002"
        };

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(HouseholdIdentifier.Email(EmailNormalizer.Normalize("user@example.com")));

        await handler.Handle(command, CancellationToken.None);

        await _addressValidator.Received(1).ValidateAsync(
            Arg.Is<Address>(a =>
                a.StreetAddress1 == "456 Oak Ave NE" &&
                a.StreetAddress2 == "Suite 100" &&
                a.City == "Washington" &&
                a.State == "District of Columbia" &&
                a.PostalCode == "20002"),
            Arg.Any<CancellationToken>());
    }

    // --- Address update service (Smarty) integration ---

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenSmartyRejectsAndValidatorPasses()
    {
        _addressUpdate
            .ValidateAndNormalizeAsync(Arg.Any<AddressUpdateOperationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                Result<AddressUpdateSuccess>.ValidationFailed("address", "Could not verify address.")));

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(HouseholdIdentifier.Email(EmailNormalizer.Normalize("user@example.com")));

        var handler = CreateHandler();
        var command = CreateValidCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        var success = Assert.IsType<SuccessResult<AddressValidationResult>>(result);
        Assert.False(success.Value.IsValid);
        Assert.Equal("not_found", success.Value.Reason);
        Assert.Contains("Could not verify address", success.Value.ErrorMessage);
    }

    [Fact]
    public async Task Handle_ReturnsPolicyViolation_WhenSmartyRejectsGeneralDelivery()
    {
        _addressUpdate
            .ValidateAndNormalizeAsync(Arg.Any<AddressUpdateOperationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                Result<AddressUpdateSuccess>.ValidationFailed(
                    "streetAddress1", "General Delivery addresses are not accepted for this state.")));

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(HouseholdIdentifier.Email(EmailNormalizer.Normalize("user@example.com")));

        var handler = CreateHandler();
        var command = CreateValidCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        var success = Assert.IsType<SuccessResult<AddressValidationResult>>(result);
        Assert.False(success.Value.IsValid);
        Assert.Equal("policy_violation", success.Value.Reason);
        Assert.Contains("General Delivery", success.Value.ErrorMessage);
    }

    [Fact]
    public async Task Handle_ReturnsBlocked_WhenSmartyCorrectedButAddressIsBlocked()
    {
        _addressUpdate
            .ValidateAndNormalizeAsync(Arg.Any<AddressUpdateOperationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                Result<AddressUpdateSuccess>.Success(
                    new AddressUpdateSuccess
                    {
                        NormalizedAddress = new Address
                        {
                            StreetAddress1 = "645 H St NE",
                            City = "Washington",
                            State = "DC",
                            PostalCode = "20002-4347"
                        },
                        WasCorrected = true
                    })));

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(HouseholdIdentifier.Email(EmailNormalizer.Normalize("user@example.com")));

        _addressValidator.ValidateAsync(Arg.Any<Address>(), Arg.Any<CancellationToken>())
            .Returns(AddressValidationResult.Invalid("This address cannot be used.", "blocked"));

        var handler = CreateHandler();
        var command = CreateValidCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        var success = Assert.IsType<SuccessResult<AddressValidationResult>>(result);
        Assert.False(success.Value.IsValid);
        Assert.Equal("blocked", success.Value.Reason);
    }

    [Fact]
    public async Task Handle_ReturnsSmartyCorrection_WhenSmartyCorrectedAndValidatorPasses()
    {
        var normalizedAddress = new Address
        {
            StreetAddress1 = "1600 Pennsylvania Ave NW",
            City = "Washington",
            State = "DC",
            PostalCode = "20500-0005"
        };

        _addressUpdate
            .ValidateAndNormalizeAsync(Arg.Any<AddressUpdateOperationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                Result<AddressUpdateSuccess>.Success(
                    new AddressUpdateSuccess
                    {
                        NormalizedAddress = normalizedAddress,
                        WasCorrected = true
                    })));

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(HouseholdIdentifier.Email(EmailNormalizer.Normalize("user@example.com")));

        _addressValidator.ValidateAsync(Arg.Any<Address>(), Arg.Any<CancellationToken>())
            .Returns(AddressValidationResult.Valid());

        var handler = CreateHandler();
        var command = CreateValidCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        var success = Assert.IsType<SuccessResult<AddressValidationResult>>(result);
        Assert.False(success.Value.IsValid);
        Assert.Equal("corrected", success.Value.Reason);
        Assert.Equal("20500-0005", success.Value.SuggestedAddress?.PostalCode);
    }

    [Fact]
    public async Task Handle_ReturnsDependencyFailed_WhenAddressServiceReturnsDependencyFailed()
    {
        _addressUpdate
            .ValidateAndNormalizeAsync(Arg.Any<AddressUpdateOperationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                Result<AddressUpdateSuccess>.DependencyFailed(
                    DependencyFailedReason.Timeout,
                    "Address verification timed out.")));

        var handler = CreateHandler();
        var command = CreateValidCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<DependencyFailedResult<AddressValidationResult>>(result);
        await _resolver.DidNotReceive()
            .ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>());
    }
}
