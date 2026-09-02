using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using SEBT.Portal.Api.Controllers.Household;
using SEBT.Portal.Api.Models.Household;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.UseCases.Household;

namespace SEBT.Portal.Tests.Unit.Api.Controllers.Household;

public class HouseholdControllerUpdateAddressTests
{
    private readonly ICommandHandler<UpdateAddressCommand, AddressValidationResult> _commandHandler =
        Substitute.For<ICommandHandler<UpdateAddressCommand, AddressValidationResult>>();

    private static HouseholdController CreateController() =>
        new()
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    private static UpdateAddressRequest CreateRequest(bool? acceptEnteredAddress = null) =>
        new()
        {
            StreetAddress1 = "123 Main St NW",
            StreetAddress2 = "Apt 4",
            City = "Washington",
            State = "DC",
            PostalCode = "20001",
            AcceptEnteredAddress = acceptEnteredAddress
        };

    private static Address CreateAddress() =>
        new()
        {
            StreetAddress1 = "123 MAIN ST NW",
            StreetAddress2 = "APT 4",
            City = "WASHINGTON",
            State = "DC",
            PostalCode = "20001-1234"
        };

    [Fact]
    public void UpdateAddress_RequiresAuthorization()
    {
        // Attribute enforcement only runs in a host; the presence assert pins the
        // guard against accidental removal.
        var method = typeof(HouseholdController).GetMethod(nameof(HouseholdController.UpdateAddress))!;

        Assert.NotEmpty(method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));
    }

    [Fact]
    public async Task UpdateAddress_MapsRequestToCommand()
    {
        // Arrange — a distinct authenticated principal, so the forwarded-User assert
        // can't pass vacuously on DefaultHttpContext's empty principal.
        var controller = CreateController();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", Guid.NewGuid().ToString())], "TestAuth"));
        controller.ControllerContext.HttpContext.User = principal;
        using var cts = new CancellationTokenSource();
        _commandHandler.Handle(Arg.Any<UpdateAddressCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<AddressValidationResult>.Success(AddressValidationResult.Valid()));

        // Act
        await controller.UpdateAddress(CreateRequest(acceptEnteredAddress: true), _commandHandler, cts.Token);

        // Assert — includes the caller's CancellationToken reaching the handler.
        await _commandHandler.Received(1).Handle(
            Arg.Is<UpdateAddressCommand>(c =>
                c.User == principal
                && c.StreetAddress1 == "123 Main St NW"
                && c.StreetAddress2 == "Apt 4"
                && c.City == "Washington"
                && c.State == "DC"
                && c.PostalCode == "20001"
                && c.AcceptEnteredAddress),
            cts.Token);
    }

    [Fact]
    public async Task UpdateAddress_CoalescesNullAcceptEnteredAddressToFalse()
    {
        // Arrange
        var controller = CreateController();
        _commandHandler.Handle(Arg.Any<UpdateAddressCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<AddressValidationResult>.Success(AddressValidationResult.Valid()));

        // Act
        await controller.UpdateAddress(CreateRequest(acceptEnteredAddress: null), _commandHandler, CancellationToken.None);

        // Assert
        await _commandHandler.Received(1).Handle(
            Arg.Is<UpdateAddressCommand>(c => !c.AcceptEnteredAddress),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAddress_ReturnsOkValidWithNormalizedAddress_WhenValidationPasses()
    {
        // Arrange
        var controller = CreateController();
        _commandHandler.Handle(Arg.Any<UpdateAddressCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<AddressValidationResult>.Success(AddressValidationResult.Valid(CreateAddress())));

        // Act
        var result = await controller.UpdateAddress(CreateRequest(), _commandHandler, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AddressUpdateResponse>(okResult.Value);
        Assert.Equal("valid", response.Status);
        Assert.NotNull(response.NormalizedAddress);
        Assert.Equal("123 MAIN ST NW", response.NormalizedAddress.StreetAddress1);
        Assert.Equal("APT 4", response.NormalizedAddress.StreetAddress2);
        Assert.Equal("WASHINGTON", response.NormalizedAddress.City);
        Assert.Equal("DC", response.NormalizedAddress.State);
        Assert.Equal("20001-1234", response.NormalizedAddress.PostalCode);
        Assert.Null(response.SuggestedAddress);
    }

    [Fact]
    public async Task UpdateAddress_ReturnsOkValidWithNullNormalizedAddress_WhenValidatorOmitsIt()
    {
        // Arrange
        var controller = CreateController();
        _commandHandler.Handle(Arg.Any<UpdateAddressCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<AddressValidationResult>.Success(AddressValidationResult.Valid()));

        // Act
        var result = await controller.UpdateAddress(CreateRequest(), _commandHandler, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AddressUpdateResponse>(okResult.Value);
        Assert.Equal("valid", response.Status);
        Assert.Null(response.NormalizedAddress);
    }

    [Fact]
    public async Task UpdateAddress_ReturnsUnprocessableSuggestion_WhenValidatorSuggestsAlternative()
    {
        // Arrange
        var controller = CreateController();
        _commandHandler.Handle(Arg.Any<UpdateAddressCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<AddressValidationResult>.Success(
                AddressValidationResult.Suggestion(CreateAddress(), "abbreviated")));

        // Act
        var result = await controller.UpdateAddress(CreateRequest(), _commandHandler, CancellationToken.None);

        // Assert
        var unprocessableResult = Assert.IsType<UnprocessableEntityObjectResult>(result);
        var response = Assert.IsType<AddressUpdateResponse>(unprocessableResult.Value);
        Assert.Equal("suggestion", response.Status);
        Assert.Equal("abbreviated", response.Reason);
        Assert.NotNull(response.SuggestedAddress);
        Assert.Equal("123 MAIN ST NW", response.SuggestedAddress.StreetAddress1);
        Assert.Equal("APT 4", response.SuggestedAddress.StreetAddress2);
        Assert.Equal("WASHINGTON", response.SuggestedAddress.City);
        Assert.Equal("DC", response.SuggestedAddress.State);
        Assert.Equal("20001-1234", response.SuggestedAddress.PostalCode);
        Assert.Null(response.NormalizedAddress);
    }

    [Fact]
    public async Task UpdateAddress_ReturnsUnprocessableInvalid_WhenValidatorRejectsWithoutSuggestion()
    {
        // Arrange
        var controller = CreateController();
        _commandHandler.Handle(Arg.Any<UpdateAddressCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<AddressValidationResult>.Success(
                AddressValidationResult.Invalid("This address cannot receive mail.", "blocked")));

        // Act
        var result = await controller.UpdateAddress(CreateRequest(), _commandHandler, CancellationToken.None);

        // Assert
        var unprocessableResult = Assert.IsType<UnprocessableEntityObjectResult>(result);
        var response = Assert.IsType<AddressUpdateResponse>(unprocessableResult.Value);
        Assert.Equal("invalid", response.Status);
        Assert.Equal("blocked", response.Reason);
        Assert.Equal("This address cannot receive mail.", response.Message);
        Assert.Null(response.SuggestedAddress);
    }

    [Fact]
    public async Task UpdateAddress_ReturnsBadRequest_WhenValidationFails()
    {
        // Arrange
        var controller = CreateController();
        _commandHandler.Handle(Arg.Any<UpdateAddressCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<AddressValidationResult>.ValidationFailed("PostalCode", "Postal code is invalid."));

        // Act
        var result = await controller.UpdateAddress(CreateRequest(), _commandHandler, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        Assert.IsType<ValidationProblemDetails>(objectResult.Value);
    }

    [Fact]
    public async Task UpdateAddress_ReturnsForbidden_WhenUserUnauthorized()
    {
        // Arrange
        var controller = CreateController();
        _commandHandler.Handle(Arg.Any<UpdateAddressCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<AddressValidationResult>.Unauthorized("No household identifier could be resolved."));

        // Act
        var result = await controller.UpdateAddress(CreateRequest(), _commandHandler, CancellationToken.None);

        // Assert — the title pins this as the unresolved-household 403, not the
        // insufficient-IAL 403 covered below, which shares the status code.
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("No household identifier could be resolved.", problemDetails.Title);
    }

    [Fact]
    public async Task UpdateAddress_ReturnsForbiddenWithRequiredIal_WhenIdentityAssuranceInsufficient()
    {
        // Arrange — distinct from the Unauthorized 403 above: a ForbiddenResult carries
        // structured extensions, and the mapping that merges them into ProblemDetails is
        // only reachable through a controller action.
        var controller = CreateController();
        _commandHandler.Handle(Arg.Any<UpdateAddressCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<AddressValidationResult>.Forbidden(
                "This household requires IAL1plus. Complete identity verification to update your address.",
                new Dictionary<string, object?> { ["requiredIal"] = "IAL1plus" }));

        // Act
        var result = await controller.UpdateAddress(CreateRequest(), _commandHandler, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Insufficient identity assurance level", problemDetails.Title);
        Assert.Equal(
            "This household requires IAL1plus. Complete identity verification to update your address.",
            problemDetails.Detail);
        Assert.Equal("IAL1plus", problemDetails.Extensions["requiredIal"]);
    }

    [Fact]
    public async Task UpdateAddress_ReturnsBadGateway_WhenVerificationProviderFails()
    {
        // Arrange
        var controller = CreateController();
        _commandHandler.Handle(Arg.Any<UpdateAddressCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<AddressValidationResult>.DependencyFailed(
                DependencyFailedReason.Timeout, "Address verification timed out."));

        // Act
        var result = await controller.UpdateAddress(CreateRequest(), _commandHandler, CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status502BadGateway, objectResult.StatusCode);
    }
}
