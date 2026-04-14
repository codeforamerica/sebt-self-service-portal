using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.Core.Models;
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
    private readonly IIdProofingRequirementsService _idProofingRequirementsService = Substitute.For<IIdProofingRequirementsService>();
    private readonly IMinimumIalService _minimumIalService = Substitute.For<IMinimumIalService>();
    private readonly NullLogger<GetHouseholdDataQueryHandler> _logger = NullLogger<GetHouseholdDataQueryHandler>.Instance;

    public GetHouseholdDataQueryHandlerTests()
    {
        // Default: no elevated IAL requirement, so existing tests pass without per-test mock setup.
        _minimumIalService.GetMinimumIal(Arg.Any<IReadOnlyList<SummerEbtCase>>()).Returns(UserIalLevel.None);
    }

    private static ClaimsPrincipal CreateUser(string email, UserIalLevel ialLevel, string claimType = ClaimTypes.Email)
    {
        var ial = ialLevel switch
        {
            UserIalLevel.IAL1 => "1",
            UserIalLevel.IAL1plus => "1plus",
            UserIalLevel.IAL2 => "2",
            _ => "0"
        };
        var claims = new List<Claim>
        {
            new Claim(claimType, email),
            new Claim(JwtClaimTypes.Ial, ial)
        };
        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }

    private static ClaimsPrincipal CreateUserWithoutIalClaim(string email)
    {
        var claims = new List<Claim> { new Claim(ClaimTypes.Email, email) };
        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task Handle_WhenIdentifierResolvedAndHouseholdExistsAndIdVerified_ReturnsSuccessWithAddress()
    {
        // Arrange
        var email = "user@example.com";
        var user = CreateUser(email, UserIalLevel.IAL1plus);
        var identifier = HouseholdIdentifier.Email(EmailNormalizer.Normalize(email));
        var householdData = new HouseholdData
        {
            Email = email,
            AddressOnFile = new Address { StreetAddress1 = "123 Main St", City = "Denver", State = "CO", PostalCode = "80202" }
        };

        var piiVisibility = new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true);
        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(identifier);
        _idProofingRequirementsService.GetPiiVisibility(UserIalLevel.IAL1plus).Returns(piiVisibility);
        _repository.GetHouseholdByIdentifierAsync(identifier, Arg.Any<PiiVisibility>(), Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
            .Returns(householdData);

        var handler = new GetHouseholdDataQueryHandler(_resolver, _repository, _idProofingRequirementsService, _minimumIalService, _logger);
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
            Arg.Any<PiiVisibility>(),
            Arg.Any<UserIalLevel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenIdentifierResolvedAndHouseholdExistsButNotIdVerified_ReturnsSuccessWithoutAddress()
    {
        // Arrange
        var email = "user@example.com";
        var user = CreateUser(email, UserIalLevel.None);
        var identifier = HouseholdIdentifier.Email(EmailNormalizer.Normalize(email));
        var householdData = new HouseholdData { Email = email };

        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: true, IncludePhone: true);
        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(identifier);
        _idProofingRequirementsService.GetPiiVisibility(UserIalLevel.None).Returns(piiVisibility);
        _repository.GetHouseholdByIdentifierAsync(identifier, Arg.Any<PiiVisibility>(), Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
            .Returns(householdData);

        var handler = new GetHouseholdDataQueryHandler(_resolver, _repository, _idProofingRequirementsService, _minimumIalService, _logger);
        var query = new GetHouseholdDataQuery { User = user };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var successResult = Assert.IsType<SuccessResult<HouseholdData>>(result);
        Assert.Same(householdData, successResult.Value);
        await _repository.Received(1).GetHouseholdByIdentifierAsync(
            Arg.Any<HouseholdIdentifier>(),
            Arg.Any<PiiVisibility>(),
            Arg.Any<UserIalLevel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenResolverReturnsNull_ReturnsUnauthorized()
    {
        // Arrange
        var user = CreateUser("user@example.com", UserIalLevel.IAL1plus);
        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns((HouseholdIdentifier?)null);

        var handler = new GetHouseholdDataQueryHandler(_resolver, _repository, _idProofingRequirementsService, _minimumIalService, _logger);
        var query = new GetHouseholdDataQuery { User = user };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        var unauthorizedResult = Assert.IsType<UnauthorizedResult<HouseholdData>>(result);
        Assert.Contains("Unable to identify user", unauthorizedResult.Message, StringComparison.OrdinalIgnoreCase);
        await _repository.DidNotReceive().GetHouseholdByIdentifierAsync(Arg.Any<HouseholdIdentifier>(), Arg.Any<PiiVisibility>(), Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenHouseholdNotFound_ReturnsPreconditionFailed()
    {
        // Arrange
        var email = "nonexistent@example.com";
        var user = CreateUser(email, UserIalLevel.IAL1plus);
        var identifier = HouseholdIdentifier.Email(EmailNormalizer.Normalize(email));

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(identifier);
        _idProofingRequirementsService.GetPiiVisibility(UserIalLevel.IAL1plus)
            .Returns(new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true));
        _repository.GetHouseholdByIdentifierAsync(Arg.Any<HouseholdIdentifier>(), Arg.Any<PiiVisibility>(), Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
            .Returns((HouseholdData?)null);

        var handler = new GetHouseholdDataQueryHandler(_resolver, _repository, _idProofingRequirementsService, _minimumIalService, _logger);
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
    public async Task Handle_WhenIalLevelIsNone_DoesNotIncludeAddress()
    {
        // Arrange
        var email = "user@example.com";
        var user = CreateUser(email, UserIalLevel.None);
        var identifier = HouseholdIdentifier.Email(EmailNormalizer.Normalize(email));
        var householdData = new HouseholdData { Email = email };

        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: true, IncludePhone: true);
        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(identifier);
        _idProofingRequirementsService.GetPiiVisibility(UserIalLevel.None).Returns(piiVisibility);
        _repository.GetHouseholdByIdentifierAsync(Arg.Any<HouseholdIdentifier>(), Arg.Any<PiiVisibility>(), Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
            .Returns(householdData);

        var handler = new GetHouseholdDataQueryHandler(_resolver, _repository, _idProofingRequirementsService, _minimumIalService, _logger);
        var query = new GetHouseholdDataQuery { User = user };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        await _repository.Received(1).GetHouseholdByIdentifierAsync(
            Arg.Any<HouseholdIdentifier>(),
            Arg.Any<PiiVisibility>(),
            Arg.Any<UserIalLevel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenIalClaimMissing_DoesNotIncludeAddress()
    {
        // Arrange
        var email = "user@example.com";
        var user = CreateUserWithoutIalClaim(email);
        var identifier = HouseholdIdentifier.Email(EmailNormalizer.Normalize(email));
        var householdData = new HouseholdData { Email = email };

        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: true, IncludePhone: true);
        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(identifier);
        _idProofingRequirementsService.GetPiiVisibility(UserIalLevel.None).Returns(piiVisibility);
        _repository.GetHouseholdByIdentifierAsync(Arg.Any<HouseholdIdentifier>(), Arg.Any<PiiVisibility>(), Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
            .Returns(householdData);

        var handler = new GetHouseholdDataQueryHandler(_resolver, _repository, _idProofingRequirementsService, _minimumIalService, _logger);
        var query = new GetHouseholdDataQuery { User = user };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        await _repository.Received(1).GetHouseholdByIdentifierAsync(
            Arg.Any<HouseholdIdentifier>(),
            Arg.Any<PiiVisibility>(),
            Arg.Any<UserIalLevel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenUserIalBelowMinimum_ReturnsForbiddenWithRequiredIal()
    {
        // Arrange: user at IAL1, but cases require IAL1+
        var email = "user@example.com";
        var user = CreateUser(email, UserIalLevel.IAL1);
        var identifier = HouseholdIdentifier.Email(EmailNormalizer.Normalize(email));
        var householdData = new HouseholdData { Email = email };

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(identifier);
        _idProofingRequirementsService.GetPiiVisibility(UserIalLevel.IAL1)
            .Returns(new PiiVisibility(IncludeAddress: false, IncludeEmail: true, IncludePhone: true));
        _repository.GetHouseholdByIdentifierAsync(Arg.Any<HouseholdIdentifier>(), Arg.Any<PiiVisibility>(), Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
            .Returns(householdData);
        _minimumIalService.GetMinimumIal(Arg.Any<IReadOnlyList<SummerEbtCase>>())
            .Returns(UserIalLevel.IAL1plus);

        var handler = new GetHouseholdDataQueryHandler(_resolver, _repository, _idProofingRequirementsService, _minimumIalService, _logger);
        var query = new GetHouseholdDataQuery { User = user };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        var forbidden = Assert.IsType<ForbiddenResult<HouseholdData>>(result);
        Assert.Contains("IAL1plus", forbidden.Message);
        Assert.Equal("IAL1plus", forbidden.Extensions["requiredIal"]);
    }

    [Fact]
    public async Task Handle_WhenUserIalMeetsMinimum_ReturnsSuccess()
    {
        // Arrange: user at IAL1+, cases require IAL1+
        var email = "user@example.com";
        var user = CreateUser(email, UserIalLevel.IAL1plus);
        var identifier = HouseholdIdentifier.Email(EmailNormalizer.Normalize(email));
        var householdData = new HouseholdData { Email = email };

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(identifier);
        _idProofingRequirementsService.GetPiiVisibility(UserIalLevel.IAL1plus)
            .Returns(new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true));
        _repository.GetHouseholdByIdentifierAsync(Arg.Any<HouseholdIdentifier>(), Arg.Any<PiiVisibility>(), Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
            .Returns(householdData);
        _minimumIalService.GetMinimumIal(Arg.Any<IReadOnlyList<SummerEbtCase>>())
            .Returns(UserIalLevel.IAL1plus);

        var handler = new GetHouseholdDataQueryHandler(_resolver, _repository, _idProofingRequirementsService, _minimumIalService, _logger);
        var query = new GetHouseholdDataQuery { User = user };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var success = Assert.IsType<SuccessResult<HouseholdData>>(result);
        Assert.Same(householdData, success.Value);
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToResolverAndRepository()
    {
        // Arrange
        var email = "user@example.com";
        var user = CreateUser(email, UserIalLevel.IAL1plus);
        var identifier = HouseholdIdentifier.Email(EmailNormalizer.Normalize(email));
        var householdData = new HouseholdData { Email = email };
        var cts = new CancellationTokenSource();
        var token = cts.Token;

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), token).Returns(identifier);
        _idProofingRequirementsService.GetPiiVisibility(UserIalLevel.IAL1plus)
            .Returns(new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true));
        _repository.GetHouseholdByIdentifierAsync(Arg.Any<HouseholdIdentifier>(), Arg.Any<PiiVisibility>(), Arg.Any<UserIalLevel>(), token)
            .Returns(householdData);

        var handler = new GetHouseholdDataQueryHandler(_resolver, _repository, _idProofingRequirementsService, _minimumIalService, _logger);
        var query = new GetHouseholdDataQuery { User = user };

        // Act
        var result = await handler.Handle(query, token);

        // Assert
        Assert.True(result.IsSuccess);
        await _resolver.Received(1).ResolveAsync(Arg.Any<ClaimsPrincipal>(), token);
        await _repository.Received(1).GetHouseholdByIdentifierAsync(
            Arg.Any<HouseholdIdentifier>(),
            Arg.Any<PiiVisibility>(),
            Arg.Any<UserIalLevel>(),
            token);
    }
}
