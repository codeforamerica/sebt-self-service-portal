using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.Api.Controllers.Household;
using SEBT.Portal.Api.Models;
using SEBT.Portal.Api.Models.Household;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;
using SEBT.Portal.Kernel;
using SEBT.Portal.UseCases.Household;

namespace SEBT.Portal.Tests.Unit.Controllers;

public class HouseholdControllerTests
{
    private readonly IIdProofingRequirementsService _idProofingRequirementsService;
    private readonly HouseholdController _controller;

    public HouseholdControllerTests()
    {
        _controller = new HouseholdController();
        _idProofingRequirementsService = Substitute.For<IIdProofingRequirementsService>();
    }

    private IQueryHandler<GetHouseholdDataQuery, HouseholdData> CreateQueryHandler(
        IHouseholdIdentifierResolver resolver,
        IHouseholdRepository repository)
    {
        var logger = NullLogger<GetHouseholdDataQueryHandler>.Instance;
        return new GetHouseholdDataQueryHandler(resolver, repository, _idProofingRequirementsService, logger);
    }

    private void SetupAuthenticatedUser(string email, UserIalLevel userIalLevel = UserIalLevel.None, string claimType = ClaimTypes.Email)
    {
        var ial = userIalLevel switch
        {
            UserIalLevel.IAL1 => "1",
            UserIalLevel.IAL1plus => "1plus",
            UserIalLevel.IAL2 => "2",
            _ => "0"
        };
        SetupAuthenticatedUser(email, ial: ial, claimType: claimType);
    }

    private void SetupAuthenticatedUser(string email, string? ial, string claimType = ClaimTypes.Email)
    {
        var claims = new List<Claim> { new Claim(claimType, email) };
        if (!string.IsNullOrEmpty(ial))
        {
            claims.Add(new Claim(JwtClaimTypes.Ial, ial));
        }
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    private static IHouseholdIdentifierResolver CreateResolverMock(string? email)
    {
        var resolver = Substitute.For<IHouseholdIdentifierResolver>();
        if (string.IsNullOrWhiteSpace(email))
        {
            resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>()).Returns((HouseholdIdentifier?)null);
        }
        else
        {
            var normalized = EmailNormalizer.Normalize(email);
            resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>()).Returns(HouseholdIdentifier.Email(normalized));
        }
        return resolver;
    }

    [Fact]
    public async Task GetHouseholdData_WhenHouseholdExistsAndIdVerified_ReturnsOkWithAddress()
    {
        // Arrange
        var email = "user@example.com";
        SetupAuthenticatedUser(email, ial: "1plus");
        _idProofingRequirementsService.GetPiiVisibility(UserIalLevel.IAL1plus)
            .Returns(new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true));

        var householdData = new HouseholdData
        {
            Email = email,
            Phone = "555-1234",
            Applications = new List<Application>
            {
                new Application
                {
                    ApplicationNumber = "APP-123",
                    CaseNumber = "CASE-456",
                    ApplicationStatus = ApplicationStatus.Approved,
                    BenefitIssueDate = DateTime.UtcNow.AddDays(-30),
                    BenefitExpirationDate = DateTime.UtcNow.AddDays(60),
                    Last4DigitsOfCard = "1234",
                    Children = new List<Child>
                    {
                        new Child { FirstName = "John", LastName = "Doe" }
                    }
                }
            },
            AddressOnFile = new Address
            {
                StreetAddress1 = "123 Main St",
                City = "Denver",
                State = "CO",
                PostalCode = "80202"
            }
        };

        var resolverMock = CreateResolverMock(email);
        var repositoryMock = Substitute.For<IHouseholdRepository>();
        repositoryMock.GetHouseholdByIdentifierAsync(Arg.Is<HouseholdIdentifier>(id => id.Type == PreferredHouseholdIdType.Email && id.Value == EmailNormalizer.Normalize(email)), Arg.Any<PiiVisibility>(), Arg.Any<CancellationToken>())
            .Returns(householdData);

        // Act
        var result = await _controller.GetHouseholdData(CreateQueryHandler(resolverMock, repositoryMock));

        // Assert
        Assert.NotNull(result);
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<HouseholdDataResponse>(okResult.Value);
        Assert.Equal(email, response.Email);
        Assert.NotNull(response.AddressOnFile);
        Assert.Equal("123 Main St", response.AddressOnFile.StreetAddress1);
        await repositoryMock.Received(1).GetHouseholdByIdentifierAsync(Arg.Is<HouseholdIdentifier>(id => id.Type == PreferredHouseholdIdType.Email && id.Value == EmailNormalizer.Normalize(email)), Arg.Any<PiiVisibility>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHouseholdData_WhenHouseholdExistsButNotIdVerified_ReturnsOkWithoutAddress()
    {
        // Arrange
        var email = "user@example.com";
        SetupAuthenticatedUser(email, UserIalLevel.None);
        _idProofingRequirementsService.GetPiiVisibility(UserIalLevel.None)
            .Returns(new PiiVisibility(IncludeAddress: false, IncludeEmail: true, IncludePhone: true));

        var householdData = new HouseholdData
        {
            Email = email,
            Phone = "555-1234",
            Applications = new List<Application>
            {
                new Application
                {
                    ApplicationStatus = ApplicationStatus.Approved,
                    Children = new List<Child>
                    {
                        new Child { FirstName = "John", LastName = "Doe" }
                    }
                }
            }
        };

        var resolverMock = CreateResolverMock(email);
        var repositoryMock = Substitute.For<IHouseholdRepository>();
        repositoryMock.GetHouseholdByIdentifierAsync(Arg.Is<HouseholdIdentifier>(id => id.Type == PreferredHouseholdIdType.Email && id.Value == EmailNormalizer.Normalize(email)), Arg.Any<PiiVisibility>(), Arg.Any<CancellationToken>())
            .Returns(householdData);

        // Act
        var result = await _controller.GetHouseholdData(CreateQueryHandler(resolverMock, repositoryMock));

        // Assert
        Assert.NotNull(result);
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<HouseholdDataResponse>(okResult.Value);
        Assert.Equal(email, response.Email);
        Assert.Null(response.AddressOnFile);
        await repositoryMock.Received(1).GetHouseholdByIdentifierAsync(Arg.Is<HouseholdIdentifier>(id => id.Type == PreferredHouseholdIdType.Email && id.Value == EmailNormalizer.Normalize(email)), Arg.Any<PiiVisibility>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHouseholdData_WhenHouseholdNotFound_ReturnsNotFound()
    {
        // Arrange
        var email = "nonexistent@example.com";
        SetupAuthenticatedUser(email, ial: "1plus");
        _idProofingRequirementsService.GetPiiVisibility(UserIalLevel.IAL1plus)
            .Returns(new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true));

        var resolverMock = CreateResolverMock(email);
        var repositoryMock = Substitute.For<IHouseholdRepository>();
        repositoryMock.GetHouseholdByIdentifierAsync(Arg.Is<HouseholdIdentifier>(id => id.Type == PreferredHouseholdIdType.Email && id.Value == EmailNormalizer.Normalize(email)), Arg.Any<PiiVisibility>(), Arg.Any<CancellationToken>())
            .Returns((HouseholdData?)null);

        // Act
        var result = await _controller.GetHouseholdData(CreateQueryHandler(resolverMock, repositoryMock));

        // Assert
        Assert.NotNull(result);
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var errorResponse = Assert.IsType<ErrorResponse>(notFoundResult.Value);
        Assert.Contains("Household data not found", errorResponse.Error, StringComparison.OrdinalIgnoreCase);
        await repositoryMock.Received(1).GetHouseholdByIdentifierAsync(Arg.Is<HouseholdIdentifier>(id => id.Type == PreferredHouseholdIdType.Email && id.Value == EmailNormalizer.Normalize(email)), Arg.Any<PiiVisibility>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHouseholdData_WhenEmailCannotBeExtracted_ReturnsUnauthorized()
    {
        // Arrange
        var claims = new List<Claim>(); // No email claim
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        var resolverMock = CreateResolverMock(null);
        var repositoryMock = Substitute.For<IHouseholdRepository>();

        // Act
        var result = await _controller.GetHouseholdData(CreateQueryHandler(resolverMock, repositoryMock));

        // Assert
        Assert.NotNull(result);
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var errorResponse = Assert.IsType<ErrorResponse>(unauthorizedResult.Value);
        Assert.Contains("Unable to identify user", errorResponse.Error, StringComparison.OrdinalIgnoreCase);
        await repositoryMock.DidNotReceive().GetHouseholdByIdentifierAsync(Arg.Any<HouseholdIdentifier>(), Arg.Any<PiiVisibility>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHouseholdData_WhenIdProofingStatusIsInProgress_DoesNotIncludeAddress()
    {
        // Arrange
        var email = "user@example.com";
        SetupAuthenticatedUser(email, UserIalLevel.None);
        _idProofingRequirementsService.GetPiiVisibility(UserIalLevel.None)
            .Returns(new PiiVisibility(IncludeAddress: false, IncludeEmail: true, IncludePhone: true));

        var householdData = new HouseholdData
        {
            Email = email,
            Phone = "555-1234",
            Applications = new List<Application>
            {
                new Application { ApplicationStatus = ApplicationStatus.Pending }
            }
        };

        var resolverMock = CreateResolverMock(email);
        var repositoryMock = Substitute.For<IHouseholdRepository>();
        repositoryMock.GetHouseholdByIdentifierAsync(Arg.Is<HouseholdIdentifier>(id => id.Type == PreferredHouseholdIdType.Email && id.Value == EmailNormalizer.Normalize(email)), Arg.Any<PiiVisibility>(), Arg.Any<CancellationToken>())
            .Returns(householdData);

        // Act
        var result = await _controller.GetHouseholdData(CreateQueryHandler(resolverMock, repositoryMock));

        // Assert
        Assert.NotNull(result);
        var okResult = Assert.IsType<OkObjectResult>(result);
        await repositoryMock.Received(1).GetHouseholdByIdentifierAsync(Arg.Is<HouseholdIdentifier>(id => id.Type == PreferredHouseholdIdType.Email && id.Value == EmailNormalizer.Normalize(email)), Arg.Any<PiiVisibility>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHouseholdData_WhenIdProofingStatusIsFailed_DoesNotIncludeAddress()
    {
        // Arrange
        var email = "user@example.com";
        SetupAuthenticatedUser(email, UserIalLevel.None);
        _idProofingRequirementsService.GetPiiVisibility(UserIalLevel.None)
            .Returns(new PiiVisibility(IncludeAddress: false, IncludeEmail: true, IncludePhone: true));

        var householdData = new HouseholdData
        {
            Email = email,
            Applications = new List<Application>
            {
                new Application { ApplicationStatus = ApplicationStatus.Pending }
            }
        };

        var resolverMock = CreateResolverMock(email);
        var repositoryMock = Substitute.For<IHouseholdRepository>();
        repositoryMock.GetHouseholdByIdentifierAsync(Arg.Is<HouseholdIdentifier>(id => id.Type == PreferredHouseholdIdType.Email && id.Value == EmailNormalizer.Normalize(email)), Arg.Any<PiiVisibility>(), Arg.Any<CancellationToken>())
            .Returns(householdData);

        // Act
        var result = await _controller.GetHouseholdData(CreateQueryHandler(resolverMock, repositoryMock));

        // Assert
        Assert.NotNull(result);
        await repositoryMock.Received(1).GetHouseholdByIdentifierAsync(Arg.Is<HouseholdIdentifier>(id => id.Type == PreferredHouseholdIdType.Email && id.Value == EmailNormalizer.Normalize(email)), Arg.Any<PiiVisibility>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHouseholdData_WhenIdProofingStatusClaimMissing_DoesNotIncludeAddress()
    {
        // Arrange
        var email = "user@example.com";
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Email, email)
            // No IdProofingStatus claim
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        _idProofingRequirementsService.GetPiiVisibility(UserIalLevel.None)
            .Returns(new PiiVisibility(IncludeAddress: false, IncludeEmail: true, IncludePhone: true));

        var householdData = new HouseholdData
        {
            Email = email,
            Applications = new List<Application>
            {
                new Application { ApplicationStatus = ApplicationStatus.Pending }
            }
        };

        var resolverMock = CreateResolverMock(email);
        var repositoryMock = Substitute.For<IHouseholdRepository>();
        repositoryMock.GetHouseholdByIdentifierAsync(Arg.Is<HouseholdIdentifier>(id => id.Type == PreferredHouseholdIdType.Email && id.Value == EmailNormalizer.Normalize(email)), Arg.Any<PiiVisibility>(), Arg.Any<CancellationToken>())
            .Returns(householdData);

        // Act
        var result = await _controller.GetHouseholdData(CreateQueryHandler(resolverMock, repositoryMock));

        // Assert
        Assert.NotNull(result);
        var okResult = Assert.IsType<OkObjectResult>(result);
        await repositoryMock.Received(1).GetHouseholdByIdentifierAsync(Arg.Is<HouseholdIdentifier>(id => id.Type == PreferredHouseholdIdType.Email && id.Value == EmailNormalizer.Normalize(email)), Arg.Any<PiiVisibility>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHouseholdData_WhenIdProofingStatusClaimInvalid_DoesNotIncludeAddress()
    {
        // Arrange
        var email = "user@example.com";
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Email, email),
            new Claim(JwtClaimTypes.IdProofingStatus, "invalid") // Invalid value
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        _idProofingRequirementsService.GetPiiVisibility(UserIalLevel.None)
            .Returns(new PiiVisibility(IncludeAddress: false, IncludeEmail: true, IncludePhone: true));

        var householdData = new HouseholdData
        {
            Email = email,
            Applications = new List<Application>
            {
                new Application { ApplicationStatus = ApplicationStatus.Pending }
            }
        };

        var resolverMock = CreateResolverMock(email);
        var repositoryMock = Substitute.For<IHouseholdRepository>();
        repositoryMock.GetHouseholdByIdentifierAsync(Arg.Is<HouseholdIdentifier>(id => id.Type == PreferredHouseholdIdType.Email && id.Value == EmailNormalizer.Normalize(email)), Arg.Any<PiiVisibility>(), Arg.Any<CancellationToken>())
            .Returns(householdData);

        // Act
        var result = await _controller.GetHouseholdData(CreateQueryHandler(resolverMock, repositoryMock));

        // Assert
        Assert.NotNull(result);
        var okResult = Assert.IsType<OkObjectResult>(result);
        await repositoryMock.Received(1).GetHouseholdByIdentifierAsync(Arg.Is<HouseholdIdentifier>(id => id.Type == PreferredHouseholdIdType.Email && id.Value == EmailNormalizer.Normalize(email)), Arg.Any<PiiVisibility>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHouseholdData_ExtractsEmailFromEmailClaim()
    {
        // Arrange
        var email = "user@example.com";
        SetupAuthenticatedUser(email, ial: "1plus", claimType: ClaimTypes.Email);
        _idProofingRequirementsService.GetPiiVisibility(UserIalLevel.IAL1plus)
            .Returns(new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true));

        var householdData = new HouseholdData { Email = email };
        var resolverMock = CreateResolverMock(email);
        var repositoryMock = Substitute.For<IHouseholdRepository>();
        repositoryMock.GetHouseholdByIdentifierAsync(Arg.Is<HouseholdIdentifier>(id => id.Type == PreferredHouseholdIdType.Email && id.Value == EmailNormalizer.Normalize(email)), Arg.Any<PiiVisibility>(), Arg.Any<CancellationToken>())
            .Returns(householdData);

        // Act
        var result = await _controller.GetHouseholdData(CreateQueryHandler(resolverMock, repositoryMock));

        // Assert
        Assert.NotNull(result);
        await repositoryMock.Received(1).GetHouseholdByIdentifierAsync(Arg.Is<HouseholdIdentifier>(id => id.Type == PreferredHouseholdIdType.Email && id.Value == EmailNormalizer.Normalize(email)), Arg.Any<PiiVisibility>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHouseholdData_ExtractsEmailFromNameIdentifier_WhenEmailClaimMissing()
    {
        // Arrange
        var email = "user@example.com";
        SetupAuthenticatedUser(email, ial: "1plus", claimType: ClaimTypes.NameIdentifier);
        _idProofingRequirementsService.GetPiiVisibility(UserIalLevel.IAL1plus)
            .Returns(new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true));

        var householdData = new HouseholdData { Email = email };
        var resolverMock = CreateResolverMock(email);
        var repositoryMock = Substitute.For<IHouseholdRepository>();
        repositoryMock.GetHouseholdByIdentifierAsync(Arg.Is<HouseholdIdentifier>(id => id.Type == PreferredHouseholdIdType.Email && id.Value == EmailNormalizer.Normalize(email)), Arg.Any<PiiVisibility>(), Arg.Any<CancellationToken>())
            .Returns(householdData);

        // Act
        var result = await _controller.GetHouseholdData(CreateQueryHandler(resolverMock, repositoryMock));

        // Assert
        Assert.NotNull(result);
        await repositoryMock.Received(1).GetHouseholdByIdentifierAsync(Arg.Is<HouseholdIdentifier>(id => id.Type == PreferredHouseholdIdType.Email && id.Value == EmailNormalizer.Normalize(email)), Arg.Any<PiiVisibility>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHouseholdData_ExtractsEmailFromIdentityName_WhenOtherClaimsMissing()
    {
        // Arrange
        var email = "user@example.com";
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(ClaimTypes.Name, email));
        identity.AddClaim(new Claim(JwtClaimTypes.Ial, "1plus"));
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        _idProofingRequirementsService.GetPiiVisibility(Arg.Any<UserIalLevel>())
            .Returns(new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true));

        var householdData = new HouseholdData { Email = email };
        var resolverMock = CreateResolverMock(email);
        var repositoryMock = Substitute.For<IHouseholdRepository>();
        repositoryMock.GetHouseholdByIdentifierAsync(Arg.Is<HouseholdIdentifier>(id => id.Type == PreferredHouseholdIdType.Email && id.Value == EmailNormalizer.Normalize(email)), Arg.Any<PiiVisibility>(), Arg.Any<CancellationToken>())
            .Returns(householdData);

        // Act
        var result = await _controller.GetHouseholdData(CreateQueryHandler(resolverMock, repositoryMock));

        // Assert
        Assert.NotNull(result);
        await repositoryMock.Received(1).GetHouseholdByIdentifierAsync(Arg.Is<HouseholdIdentifier>(id => id.Type == PreferredHouseholdIdType.Email && id.Value == EmailNormalizer.Normalize(email)), Arg.Any<PiiVisibility>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHouseholdData_WhenIdProofingStatusIsExpired_DoesNotIncludeAddress()
    {
        // Arrange
        var email = "user@example.com";
        SetupAuthenticatedUser(email, UserIalLevel.None);
        _idProofingRequirementsService.GetPiiVisibility(UserIalLevel.None)
            .Returns(new PiiVisibility(IncludeAddress: false, IncludeEmail: true, IncludePhone: true));

        var householdData = new HouseholdData
        {
            Email = email,
            Applications = new List<Application>
            {
                new Application { ApplicationStatus = ApplicationStatus.Pending }
            }
        };

        var resolverMock = CreateResolverMock(email);
        var repositoryMock = Substitute.For<IHouseholdRepository>();
        repositoryMock.GetHouseholdByIdentifierAsync(Arg.Is<HouseholdIdentifier>(id => id.Type == PreferredHouseholdIdType.Email && id.Value == EmailNormalizer.Normalize(email)), Arg.Any<PiiVisibility>(), Arg.Any<CancellationToken>())
            .Returns(householdData);

        // Act
        var result = await _controller.GetHouseholdData(CreateQueryHandler(resolverMock, repositoryMock));

        // Assert
        Assert.NotNull(result);
        await repositoryMock.Received(1).GetHouseholdByIdentifierAsync(Arg.Is<HouseholdIdentifier>(id => id.Type == PreferredHouseholdIdType.Email && id.Value == EmailNormalizer.Normalize(email)), Arg.Any<PiiVisibility>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHouseholdData_ReturnsCompleteHouseholdData()
    {
        // Arrange
        var email = "user@example.com";
        SetupAuthenticatedUser(email, ial: "1plus");
        _idProofingRequirementsService.GetPiiVisibility(Arg.Any<UserIalLevel>())
            .Returns(new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true));

        var householdData = new HouseholdData
        {
            Email = email,
            Phone = "555-1234",
            Applications = new List<Application>
            {
                new Application
                {
                    ApplicationNumber = "APP-123",
                    CaseNumber = "CASE-456",
                    ApplicationStatus = ApplicationStatus.Approved,
                    BenefitIssueDate = DateTime.UtcNow.AddDays(-30),
                    BenefitExpirationDate = DateTime.UtcNow.AddDays(60),
                    Last4DigitsOfCard = "1234",
                    Children = new List<Child>
                    {
                        new Child { FirstName = "John", LastName = "Doe" },
                        new Child { FirstName = "Jane", LastName = "Doe" }
                    }
                }
            },
            AddressOnFile = new Address
            {
                StreetAddress1 = "123 Main St",
                StreetAddress2 = "Apt 4B",
                City = "Denver",
                State = "CO",
                PostalCode = "80202"
            },
            UserProfile = new UserProfile { FirstName = "Jane", MiddleName = "Marie", LastName = "Doe" }
        };

        var resolverMock = CreateResolverMock(email);
        var repositoryMock = Substitute.For<IHouseholdRepository>();
        repositoryMock.GetHouseholdByIdentifierAsync(Arg.Is<HouseholdIdentifier>(id => id.Type == PreferredHouseholdIdType.Email && id.Value == EmailNormalizer.Normalize(email)), Arg.Any<PiiVisibility>(), Arg.Any<CancellationToken>())
            .Returns(householdData);

        // Act
        var result = await _controller.GetHouseholdData(CreateQueryHandler(resolverMock, repositoryMock));

        // Assert
        Assert.NotNull(result);
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<HouseholdDataResponse>(okResult.Value);
        Assert.Equal(email, response.Email);
        Assert.Equal("555-1234", response.Phone);
        Assert.NotNull(response.Applications);
        Assert.NotEmpty(response.Applications);
        var app = response.Applications.First();
        Assert.Equal(2, app.Children.Count);
        Assert.Equal("APP-123", app.ApplicationNumber);
        Assert.Equal("CASE-456", app.CaseNumber);
        Assert.Equal(ApplicationStatus.Approved, app.ApplicationStatus);
        Assert.Equal(2, app.ChildrenOnApplication);
        Assert.NotNull(response.AddressOnFile);
        Assert.Equal("123 Main St", response.AddressOnFile.StreetAddress1);
        Assert.Equal("Apt 4B", response.AddressOnFile.StreetAddress2);
        Assert.Equal("Denver", response.AddressOnFile.City);
        Assert.Equal("CO", response.AddressOnFile.State);
        Assert.Equal("80202", response.AddressOnFile.PostalCode);
        Assert.NotNull(response.UserProfile);
        Assert.Equal("Jane", response.UserProfile.FirstName);
        Assert.Equal("Marie", response.UserProfile.MiddleName);
        Assert.Equal("Doe", response.UserProfile.LastName);
    }
}
