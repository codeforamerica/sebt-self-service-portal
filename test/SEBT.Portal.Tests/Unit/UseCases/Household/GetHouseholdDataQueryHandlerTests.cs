using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.UseCases.Household;

namespace SEBT.Portal.Tests.Unit.UseCases.Household;

public class GetHouseholdDataQueryHandlerTests
{
    private readonly IHouseholdIdentifierResolver _resolver = Substitute.For<IHouseholdIdentifierResolver>();
    private readonly IHouseholdRepository _repository = Substitute.For<IHouseholdRepository>();
    private readonly NullLogger<GetHouseholdDataQueryHandler> _logger = NullLogger<GetHouseholdDataQueryHandler>.Instance;

    private static ClaimsPrincipal CreateUser(string email, IdProofingStatus idProofingStatus, string claimType = ClaimTypes.Email)
    {
        var claims = new List<Claim>
        {
            new Claim(claimType, email),
            new Claim(JwtClaimTypes.IdProofingStatus, ((int)idProofingStatus).ToString())
        };
        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal CreateUserWithoutIdProofingClaim(string email)
    {
        var claims = new List<Claim> { new Claim(ClaimTypes.Email, email) };
        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal CreateUserWithInvalidIdProofingClaim(string email)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Email, email),
            new Claim(JwtClaimTypes.IdProofingStatus, "invalid")
        };
        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task Handle_WhenIdentifierResolvedAndHouseholdExistsAndIdVerified_ReturnsSuccessWithAddress()
    {
        // Arrange
        var email = "user@example.com";
        var user = CreateUser(email, IdProofingStatus.Completed);
        var identifier = HouseholdIdentifier.Email(EmailNormalizer.Normalize(email));
        var householdData = new HouseholdData
        {
            Email = email,
            AddressOnFile = new Address { StreetAddress1 = "123 Main St", City = "Denver", State = "CO", PostalCode = "80202" }
        };

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(identifier);
        _repository.GetHouseholdByIdentifierAsync(identifier, includeAddress: true, Arg.Any<CancellationToken>())
            .Returns(householdData);

        var handler = new GetHouseholdDataQueryHandler(_resolver, _repository, _logger);
        var query = new GetHouseholdDataQuery { User = user };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var successResult = Assert.IsType<SuccessResult<HouseholdData>>(result);
        Assert.Same(householdData, successResult.Value);
        Assert.NotNull(successResult.Value.AddressOnFile);
        await _repository.Received(1).GetHouseholdByIdentifierAsync(
            Arg.Is<HouseholdIdentifier>(id => id.Type == PreferredHouseholdIdType.Email && id.Value == EmailNormalizer.Normalize(email)),
            includeAddress: true,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenIdentifierResolvedAndHouseholdExistsButNotIdVerified_ReturnsSuccessWithoutAddress()
    {
        // Arrange
        var email = "user@example.com";
        var user = CreateUser(email, IdProofingStatus.NotStarted);
        var identifier = HouseholdIdentifier.Email(EmailNormalizer.Normalize(email));
        var householdData = new HouseholdData { Email = email };

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(identifier);
        _repository.GetHouseholdByIdentifierAsync(identifier, includeAddress: false, Arg.Any<CancellationToken>())
            .Returns(householdData);

        var handler = new GetHouseholdDataQueryHandler(_resolver, _repository, _logger);
        var query = new GetHouseholdDataQuery { User = user };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var successResult = Assert.IsType<SuccessResult<HouseholdData>>(result);
        Assert.Same(householdData, successResult.Value);
        await _repository.Received(1).GetHouseholdByIdentifierAsync(
            Arg.Any<HouseholdIdentifier>(),
            includeAddress: false,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenResolverReturnsNull_ReturnsUnauthorized()
    {
        // Arrange
        var user = CreateUser("user@example.com", IdProofingStatus.Completed);
        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns((HouseholdIdentifier?)null);

        var handler = new GetHouseholdDataQueryHandler(_resolver, _repository, _logger);
        var query = new GetHouseholdDataQuery { User = user };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        var unauthorizedResult = Assert.IsType<UnauthorizedResult<HouseholdData>>(result);
        Assert.Contains("Unable to identify user", unauthorizedResult.Message, StringComparison.OrdinalIgnoreCase);
        await _repository.DidNotReceive().GetHouseholdByIdentifierAsync(Arg.Any<HouseholdIdentifier>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenHouseholdNotFound_ReturnsPreconditionFailed()
    {
        // Arrange
        var email = "nonexistent@example.com";
        var user = CreateUser(email, IdProofingStatus.Completed);
        var identifier = HouseholdIdentifier.Email(EmailNormalizer.Normalize(email));

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(identifier);
        _repository.GetHouseholdByIdentifierAsync(Arg.Any<HouseholdIdentifier>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((HouseholdData?)null);

        var handler = new GetHouseholdDataQueryHandler(_resolver, _repository, _logger);
        var query = new GetHouseholdDataQuery { User = user };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        var preconditionFailed = Assert.IsType<PreconditionFailedResult<HouseholdData>>(result);
        Assert.Equal(PreconditionFailedReason.NotFound, preconditionFailed.Reason);
        Assert.Contains("Household data not found", preconditionFailed.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_WhenIdProofingStatusIsInProgress_DoesNotIncludeAddress()
    {
        // Arrange
        var email = "user@example.com";
        var user = CreateUser(email, IdProofingStatus.InProgress);
        var identifier = HouseholdIdentifier.Email(EmailNormalizer.Normalize(email));
        var householdData = new HouseholdData { Email = email };

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(identifier);
        _repository.GetHouseholdByIdentifierAsync(Arg.Any<HouseholdIdentifier>(), includeAddress: false, Arg.Any<CancellationToken>())
            .Returns(householdData);

        var handler = new GetHouseholdDataQueryHandler(_resolver, _repository, _logger);
        var query = new GetHouseholdDataQuery { User = user };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        await _repository.Received(1).GetHouseholdByIdentifierAsync(
            Arg.Any<HouseholdIdentifier>(),
            includeAddress: false,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenIdProofingStatusIsFailed_DoesNotIncludeAddress()
    {
        // Arrange
        var email = "user@example.com";
        var user = CreateUser(email, IdProofingStatus.Failed);
        var identifier = HouseholdIdentifier.Email(EmailNormalizer.Normalize(email));
        var householdData = new HouseholdData { Email = email };

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(identifier);
        _repository.GetHouseholdByIdentifierAsync(Arg.Any<HouseholdIdentifier>(), includeAddress: false, Arg.Any<CancellationToken>())
            .Returns(householdData);

        var handler = new GetHouseholdDataQueryHandler(_resolver, _repository, _logger);
        var query = new GetHouseholdDataQuery { User = user };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        await _repository.Received(1).GetHouseholdByIdentifierAsync(
            Arg.Any<HouseholdIdentifier>(),
            includeAddress: false,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenIdProofingStatusIsExpired_DoesNotIncludeAddress()
    {
        // Arrange
        var email = "user@example.com";
        var user = CreateUser(email, IdProofingStatus.Expired);
        var identifier = HouseholdIdentifier.Email(EmailNormalizer.Normalize(email));
        var householdData = new HouseholdData { Email = email };

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(identifier);
        _repository.GetHouseholdByIdentifierAsync(Arg.Any<HouseholdIdentifier>(), includeAddress: false, Arg.Any<CancellationToken>())
            .Returns(householdData);

        var handler = new GetHouseholdDataQueryHandler(_resolver, _repository, _logger);
        var query = new GetHouseholdDataQuery { User = user };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        await _repository.Received(1).GetHouseholdByIdentifierAsync(
            Arg.Any<HouseholdIdentifier>(),
            includeAddress: false,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenIdProofingStatusClaimMissing_DoesNotIncludeAddress()
    {
        // Arrange
        var email = "user@example.com";
        var user = CreateUserWithoutIdProofingClaim(email);
        var identifier = HouseholdIdentifier.Email(EmailNormalizer.Normalize(email));
        var householdData = new HouseholdData { Email = email };

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(identifier);
        _repository.GetHouseholdByIdentifierAsync(Arg.Any<HouseholdIdentifier>(), includeAddress: false, Arg.Any<CancellationToken>())
            .Returns(householdData);

        var handler = new GetHouseholdDataQueryHandler(_resolver, _repository, _logger);
        var query = new GetHouseholdDataQuery { User = user };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        await _repository.Received(1).GetHouseholdByIdentifierAsync(
            Arg.Any<HouseholdIdentifier>(),
            includeAddress: false,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenIdProofingStatusClaimInvalid_DoesNotIncludeAddress()
    {
        // Arrange
        var email = "user@example.com";
        var user = CreateUserWithInvalidIdProofingClaim(email);
        var identifier = HouseholdIdentifier.Email(EmailNormalizer.Normalize(email));
        var householdData = new HouseholdData { Email = email };

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(identifier);
        _repository.GetHouseholdByIdentifierAsync(Arg.Any<HouseholdIdentifier>(), includeAddress: false, Arg.Any<CancellationToken>())
            .Returns(householdData);

        var handler = new GetHouseholdDataQueryHandler(_resolver, _repository, _logger);
        var query = new GetHouseholdDataQuery { User = user };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        await _repository.Received(1).GetHouseholdByIdentifierAsync(
            Arg.Any<HouseholdIdentifier>(),
            includeAddress: false,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToResolverAndRepository()
    {
        // Arrange
        var email = "user@example.com";
        var user = CreateUser(email, IdProofingStatus.Completed);
        var identifier = HouseholdIdentifier.Email(EmailNormalizer.Normalize(email));
        var householdData = new HouseholdData { Email = email };
        var cts = new CancellationTokenSource();
        var token = cts.Token;

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), token).Returns(identifier);
        _repository.GetHouseholdByIdentifierAsync(Arg.Any<HouseholdIdentifier>(), Arg.Any<bool>(), token)
            .Returns(householdData);

        var handler = new GetHouseholdDataQueryHandler(_resolver, _repository, _logger);
        var query = new GetHouseholdDataQuery { User = user };

        // Act
        var result = await handler.Handle(query, token);

        // Assert
        Assert.True(result.IsSuccess);
        await _resolver.Received(1).ResolveAsync(Arg.Any<ClaimsPrincipal>(), token);
        await _repository.Received(1).GetHouseholdByIdentifierAsync(
            Arg.Any<HouseholdIdentifier>(),
            Arg.Any<bool>(),
            token);
    }
}
