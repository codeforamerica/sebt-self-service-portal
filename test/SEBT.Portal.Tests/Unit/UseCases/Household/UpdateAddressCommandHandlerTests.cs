using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.AddressUpdate;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.UseCases.Household;
using ICoreAddressUpdateService = SEBT.Portal.Core.Services.IAddressUpdateService;
using IStateAddressUpdateService = SEBT.Portal.StatesPlugins.Interfaces.IAddressUpdateService;
using AddressUpdateRequest = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.AddressUpdateRequest;
using AddressUpdateResult = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.AddressUpdateResult;
using HouseholdData = SEBT.Portal.Core.Models.Household.HouseholdData;

namespace SEBT.Portal.Tests.Unit.UseCases.Household;

public class UpdateAddressCommandHandlerTests
{
    private readonly IValidator<UpdateAddressCommand> _validator =
        new DataAnnotationsValidator<UpdateAddressCommand>(null!);
    private readonly IHouseholdIdentifierResolver _resolver =
        Substitute.For<IHouseholdIdentifierResolver>();
    private readonly ICoreAddressUpdateService _addressUpdateService = Substitute.For<ICoreAddressUpdateService>();
    private readonly IAddressValidationService _addressValidationService = Substitute.For<IAddressValidationService>();
    private readonly IHouseholdRepository _householdRepository =
        Substitute.For<IHouseholdRepository>();
    private readonly IIdProofingRequirementsService _idProofingRequirementsService =
        Substitute.For<IIdProofingRequirementsService>();
    private readonly IStateAddressUpdateService _stateAddressUpdateService =
        Substitute.For<IStateAddressUpdateService>();
    private readonly IMinimumIalService _minimumIalService =
        Substitute.For<IMinimumIalService>();
    private readonly NullLogger<UpdateAddressCommandHandler> _logger =
        NullLogger<UpdateAddressCommandHandler>.Instance;

    public UpdateAddressCommandHandlerTests()
    {
        // Default: address validation passes, state connector succeeds, PII visibility minimal
        _addressUpdateService
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
        _addressValidationService.ValidateAsync(Arg.Any<Address>(), Arg.Any<CancellationToken>())
            .Returns(AddressValidationResult.Valid());
        _stateAddressUpdateService.UpdateAddressAsync(Arg.Any<AddressUpdateRequest>(), Arg.Any<CancellationToken>())
            .Returns(AddressUpdateResult.Success());
        _idProofingRequirementsService.GetPiiVisibility(Arg.Any<UserIalLevel>())
            .Returns(new PiiVisibility(false, false, false));
        // Default: IAL gate passes (no elevated requirement)
        _minimumIalService.GetMinimumIal(Arg.Any<IReadOnlyList<SummerEbtCase>>()).Returns(UserIalLevel.None);
    }

    private UpdateAddressCommandHandler CreateHandler() =>
        new(_validator, _addressUpdateService, _addressValidationService, _resolver, _householdRepository,
            _idProofingRequirementsService, _minimumIalService, _stateAddressUpdateService, _logger);

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

    private void SetupResolverReturnsEmail(string email = "user@example.com")
    {
        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(HouseholdIdentifier.Email(EmailNormalizer.Normalize(email)));
    }

    private void SetupHouseholdWithCases(params SummerEbtCase[] cases)
    {
        _householdRepository.GetHouseholdByIdentifierAsync(
                Arg.Any<HouseholdIdentifier>(), Arg.Any<PiiVisibility>(),
                Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
            .Returns(new HouseholdData { SummerEbtCases = cases.ToList() });
    }

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
        var command = new UpdateAddressCommand
        {
            User = CreateUser("user@example.com"),
            StreetAddress1 = "123 Main St NW",
            City = "Washington",
            State = "District of Columbia",
            PostalCode = "20001-1234"
        };

        SetupResolverReturnsEmail();
        SetupHouseholdWithCases();

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

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

    [Fact]
    public async Task Handle_ReturnsPreconditionFailed_WhenAnyCaseIsCoLoaded()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        SetupResolverReturnsEmail();
        SetupHouseholdWithCases(
            new SummerEbtCase { SummerEBTCaseID = "S1", ChildFirstName = "A", ChildLastName = "B", IsCoLoaded = true });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var preconditionFailed = Assert.IsType<PreconditionFailedResult<AddressValidationResult>>(result);
        Assert.Equal(PreconditionFailedReason.Conflict, preconditionFailed.Reason);
    }

    [Fact]
    public async Task Handle_ReturnsPreconditionFailed_WhenMixedCoLoadedAndNonCoLoaded()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        SetupResolverReturnsEmail();
        SetupHouseholdWithCases(
            new SummerEbtCase { SummerEBTCaseID = "S1", ChildFirstName = "A", ChildLastName = "B", IsCoLoaded = true },
            new SummerEbtCase { SummerEBTCaseID = "S2", ChildFirstName = "C", ChildLastName = "D", IsCoLoaded = false });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var preconditionFailed = Assert.IsType<PreconditionFailedResult<AddressValidationResult>>(result);
        Assert.Equal(PreconditionFailedReason.Conflict, preconditionFailed.Reason);
    }

    [Fact]
    public async Task Handle_AllowsUpdate_WhenNoCasesAreCoLoaded()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        SetupResolverReturnsEmail();
        SetupHouseholdWithCases(
            new SummerEbtCase { SummerEBTCaseID = "S1", ChildFirstName = "A", ChildLastName = "B", IsCoLoaded = false });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_AllowsUpdate_WhenNoCasesExist()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        SetupResolverReturnsEmail();
        SetupHouseholdWithCases(); // empty

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_AllowsUpdate_WhenHouseholdNotFound()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        SetupResolverReturnsEmail();
        _householdRepository.GetHouseholdByIdentifierAsync(
                Arg.Any<HouseholdIdentifier>(), Arg.Any<PiiVisibility>(),
                Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
            .Returns((HouseholdData?)null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_ReturnsNotFoundResult_WhenAddressServiceReturnsValidationFailed()
    {
        _addressUpdateService
            .ValidateAndNormalizeAsync(Arg.Any<AddressUpdateOperationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                Result<AddressUpdateSuccess>.ValidationFailed("address", "Could not verify address.")));

        var handler = CreateHandler();
        var command = CreateValidCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        // Smarty verification failures become structured "not-found" results (422)
        // so the frontend routes to Address Not Found, not a generic 400.
        Assert.True(result.IsSuccess);
        var successResult = Assert.IsType<SuccessResult<AddressValidationResult>>(result);
        Assert.False(successResult.Value.IsValid);
        Assert.Equal("not-found", successResult.Value.Reason);
        await _resolver.DidNotReceive()
            .ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsDependencyFailed_WhenAddressServiceReturnsDependencyFailed()
    {
        _addressUpdateService
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

    [Fact]
    public async Task Handle_DoesNotCallAddressValidation_WhenInputValidationFails()
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

        await _addressUpdateService.DidNotReceive()
            .ValidateAndNormalizeAsync(Arg.Any<AddressUpdateOperationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DoesNotCallStateConnector_WhenPolicyCheckRejects()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        SetupResolverReturnsEmail();
        SetupHouseholdWithCases(
            new SummerEbtCase { SummerEBTCaseID = "S1", ChildFirstName = "A", ChildLastName = "B", IsCoLoaded = true });

        await handler.Handle(command, CancellationToken.None);

        await _stateAddressUpdateService.DidNotReceive()
            .UpdateAddressAsync(Arg.Any<AddressUpdateRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsSuccess_WhenStateConnectorSucceeds()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        SetupResolverReturnsEmail();
        SetupHouseholdWithCases();

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.IsType<SuccessResult<AddressValidationResult>>(result);
    }

    [Fact]
    public async Task Handle_ReturnsPreconditionFailed_WhenStateConnectorReturnsPolicyRejection()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        SetupResolverReturnsEmail();
        SetupHouseholdWithCases();
        _stateAddressUpdateService.UpdateAddressAsync(Arg.Any<AddressUpdateRequest>(), Arg.Any<CancellationToken>())
            .Returns(AddressUpdateResult.PolicyRejected("HOUSEHOLD_NOT_ELIGIBLE", "Not eligible."));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var preconditionFailed = Assert.IsType<PreconditionFailedResult<AddressValidationResult>>(result);
        Assert.Equal(PreconditionFailedReason.Conflict, preconditionFailed.Reason);
    }

    [Fact]
    public async Task Handle_ReturnsDependencyFailed_WhenStateConnectorReturnsBackendError()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        SetupResolverReturnsEmail();
        SetupHouseholdWithCases();
        _stateAddressUpdateService.UpdateAddressAsync(Arg.Any<AddressUpdateRequest>(), Arg.Any<CancellationToken>())
            .Returns(AddressUpdateResult.BackendError("BACKEND_ERROR", "Something went wrong."));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<DependencyFailedResult<AddressValidationResult>>(result);
    }

    [Fact]
    public async Task Handle_ReturnsDependencyFailed_WhenStateConnectorThrowsException()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        SetupResolverReturnsEmail();
        SetupHouseholdWithCases();
        _stateAddressUpdateService.UpdateAddressAsync(Arg.Any<AddressUpdateRequest>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Connection failed"));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<DependencyFailedResult<AddressValidationResult>>(result);
    }

    [Fact]
    public async Task Handle_DoesNotCallStateConnector_WhenAddressValidationFails()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        _addressUpdateService
            .ValidateAndNormalizeAsync(Arg.Any<AddressUpdateOperationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                Result<AddressUpdateSuccess>.ValidationFailed("address", "Bad address")));

        await handler.Handle(command, CancellationToken.None);

        await _stateAddressUpdateService.DidNotReceive()
            .UpdateAddressAsync(Arg.Any<AddressUpdateRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToResolver()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();
        var cts = new CancellationTokenSource();
        var token = cts.Token;

        SetupResolverReturnsEmail();
        SetupHouseholdWithCases();

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

        SetupResolverReturnsEmail();
        SetupHouseholdWithCases();

        await handler.Handle(command, token);

        await _addressUpdateService.Received(1).ValidateAndNormalizeAsync(
            Arg.Any<AddressUpdateOperationRequest>(), token);
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToStateConnector()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();
        var cts = new CancellationTokenSource();
        var token = cts.Token;

        SetupResolverReturnsEmail();
        SetupHouseholdWithCases();

        await handler.Handle(command, token);

        await _stateAddressUpdateService.Received(1)
            .UpdateAddressAsync(Arg.Any<AddressUpdateRequest>(), token);
    }

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

        await _addressUpdateService.DidNotReceive()
            .ValidateAndNormalizeAsync(Arg.Any<AddressUpdateOperationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsSuccess_WhenOptionalStreetAddress2IsProvided()
    {
        var handler = CreateHandler();
        var command = new UpdateAddressCommand
        {
            User = CreateUser("user@example.com"),
            StreetAddress1 = "123 Main St NW",
            StreetAddress2 = "Apt 4B",
            City = "Washington",
            State = "District of Columbia",
            PostalCode = "20001"
        };

        SetupResolverReturnsEmail();
        SetupHouseholdWithCases();

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_UsesNormalizedAddressForStateConnector()
    {
        var normalizedAddress = new Address
        {
            StreetAddress1 = "123 MAIN ST NW",
            City = "WASHINGTON",
            State = "DC",
            PostalCode = "20001-0001"
        };

        _addressUpdateService
            .ValidateAndNormalizeAsync(Arg.Any<AddressUpdateOperationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                Result<AddressUpdateSuccess>.Success(
                    new AddressUpdateSuccess
                    {
                        NormalizedAddress = normalizedAddress,
                        WasCorrected = true,
                        IsGeneralDelivery = false
                    })));

        var handler = CreateHandler();
        var command = CreateValidCommand();

        SetupResolverReturnsEmail();
        SetupHouseholdWithCases();

        await handler.Handle(command, CancellationToken.None);

        await _stateAddressUpdateService.Received(1).UpdateAddressAsync(
            Arg.Is<AddressUpdateRequest>(r =>
                r.Address.StreetAddress1 == "123 MAIN ST NW" &&
                r.Address.City == "WASHINGTON" &&
                r.Address.State == "DC" &&
                r.Address.PostalCode == "20001-0001"),
            Arg.Any<CancellationToken>());
    }
}
