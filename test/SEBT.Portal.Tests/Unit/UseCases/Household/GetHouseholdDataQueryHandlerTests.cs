using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
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
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPiiVisibilityService _piiVisibilityService = Substitute.For<IPiiVisibilityService>();
    private readonly IIdProofingService _idProofingService = Substitute.For<IIdProofingService>();
    private readonly ISelfServiceEvaluator _selfServiceEvaluator = Substitute.For<ISelfServiceEvaluator>();
    private readonly ICardReplacementRequestRepository _cardReplacementRepo = Substitute.For<ICardReplacementRequestRepository>();
    private readonly IIdentifierHasher _identifierHasher = Substitute.For<IIdentifierHasher>();
    private readonly NullLogger<GetHouseholdDataQueryHandler> _logger = NullLogger<GetHouseholdDataQueryHandler>.Instance;

    private GetHouseholdDataQueryHandler CreateHandler(CoLoadedCohortFilterSettings? coLoadedCohortFilter = null) =>
        new(
            _resolver,
            _repository,
            _userRepository,
            _piiVisibilityService,
            _idProofingService,
            _selfServiceEvaluator,
            _cardReplacementRepo,
            _identifierHasher,
            coLoadedCohortFilter ?? new CoLoadedCohortFilterSettings(),
            _logger);

    public GetHouseholdDataQueryHandlerTests()
    {
        _userRepository.GetUserByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Default: no elevated IAL requirement, so existing tests pass without per-test mock setup.
        _idProofingService.Evaluate(
            Arg.Any<ProtectedResource>(), Arg.Any<ProtectedAction>(),
            Arg.Any<UserIalLevel>(), Arg.Any<IReadOnlyList<SummerEbtCase>>())
            .Returns(new IdProofingDecision(IsAllowed: true, RequiredLevel: UserIalLevel.None));

        // Default: self-service rules allow both actions so existing tests don't need to mock this.
        _selfServiceEvaluator.Evaluate(Arg.Any<SummerEbtCase>())
            .Returns(new AllowedActions { CanUpdateAddress = true, CanRequestReplacementCard = true });
        _selfServiceEvaluator.EvaluateHousehold(Arg.Any<IReadOnlyList<SummerEbtCase>>())
            .Returns(new AllowedActions { CanUpdateAddress = true, CanRequestReplacementCard = true });
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

    private static ClaimsPrincipal CreateUserWithSub(string email, UserIalLevel ialLevel, Guid userId)
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
            new Claim(ClaimTypes.Email, email),
            new Claim(JwtClaimTypes.Ial, ial),
            new Claim("sub", userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task Handle_WhenEmailLookupFails_AndCoLoadedUserHasBenefitIc_LoadsHouseholdViaIcDobFallback()
    {
        var email = "guardian@example.com";
        var normalizedEmail = EmailNormalizer.Normalize(email);
        var userId = Guid.Parse("a1111111-1111-4111-8111-111111111111");
        var dob = new DateOnly(1984, 3, 5);
        var principal = CreateUserWithSub(email, UserIalLevel.IAL1plus, userId);
        var piiVisibility = new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true);

        _piiVisibilityService.GetVisibility(UserIalLevel.IAL1plus).Returns(piiVisibility);
        _resolver.ResolveAsync(principal, Arg.Any<CancellationToken>())
            .Returns(new HouseholdIdentifier(PreferredHouseholdIdType.Email, normalizedEmail));

        _repository.GetHouseholdByIdentifierAsync(
                Arg.Is<HouseholdIdentifier>(i => i.Type == PreferredHouseholdIdType.Email && i.Value == normalizedEmail),
                Arg.Any<PiiVisibility>(),
                UserIalLevel.IAL1plus,
                Arg.Any<CancellationToken>())
            .Returns((HouseholdData?)null);

        _userRepository.GetUserByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new User
            {
                Id = userId,
                Email = email,
                IsCoLoaded = true,
                DateOfBirth = dob,
                SnapId = "IC000001"
            });

        var fallbackHousehold = new HouseholdData
        {
            Email = normalizedEmail,
            SummerEbtCases = new List<SummerEbtCase>
            {
                new()
                {
                    SummerEBTCaseID = "SEBT-01",
                    ChildFirstName = "A",
                    ChildLastName = "B",
                    ChildDateOfBirth = new DateTime(2014, 1, 1),
                    HouseholdType = "DHS",
                    EligibilityType = "FOOD_STREAMLINE",
                    ApplicationStatus = ApplicationStatus.Approved,
                    IssuanceType = IssuanceType.SnapEbtCard,
                    IsCoLoaded = true,
                    IsStreamlineCertified = true
                }
            },
            Applications = new List<Application>()
        };

        _repository.GetHouseholdByBenefitIdentifierAndGuardianDobAsync(
                normalizedEmail,
                "IC000001",
                dob,
                Arg.Any<PiiVisibility>(),
                UserIalLevel.IAL1plus,
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
            .Returns(fallbackHousehold);

        _identifierHasher.Hash(normalizedEmail).Returns((string?)null);

        var handler = CreateHandler();
        var result = await handler.Handle(new GetHouseholdDataQuery { User = principal });

        Assert.True(result.IsSuccess);
        var successResult = Assert.IsType<SuccessResult<HouseholdData>>(result);
        Assert.Single(successResult.Value.SummerEbtCases);
        await _repository.Received(1).GetHouseholdByBenefitIdentifierAndGuardianDobAsync(
            normalizedEmail,
            "IC000001",
            dob,
            Arg.Any<PiiVisibility>(),
            UserIalLevel.IAL1plus,
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenEmailLookupFails_AndUserNotCoLoaded_DoesNotInvokeIcDobFallback()
    {
        var email = "solo@example.com";
        var normalizedEmail = EmailNormalizer.Normalize(email);
        var userId = Guid.Parse("b2222222-2222-4222-8222-222222222222");
        var principal = CreateUserWithSub(email, UserIalLevel.IAL1plus, userId);
        var piiVisibility = new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true);

        _piiVisibilityService.GetVisibility(UserIalLevel.IAL1plus).Returns(piiVisibility);
        _resolver.ResolveAsync(principal, Arg.Any<CancellationToken>())
            .Returns(new HouseholdIdentifier(PreferredHouseholdIdType.Email, normalizedEmail));

        _repository.GetHouseholdByIdentifierAsync(
                Arg.Any<HouseholdIdentifier>(),
                Arg.Any<PiiVisibility>(),
                Arg.Any<UserIalLevel>(),
                Arg.Any<CancellationToken>())
            .Returns((HouseholdData?)null);

        _userRepository.GetUserByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new User { Id = userId, Email = email, IsCoLoaded = false, SnapId = "IC000001", DateOfBirth = new DateOnly(1980, 1, 1) });

        var handler = CreateHandler();
        var result = await handler.Handle(new GetHouseholdDataQuery { User = principal });

        Assert.False(result.IsSuccess);
        await _repository.DidNotReceive().GetHouseholdByBenefitIdentifierAndGuardianDobAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<DateOnly>(),
            Arg.Any<PiiVisibility>(),
            Arg.Any<UserIalLevel>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
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
        _piiVisibilityService.GetVisibility(UserIalLevel.IAL1plus).Returns(piiVisibility);
        _repository.GetHouseholdByIdentifierAsync(identifier, Arg.Any<PiiVisibility>(), Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
            .Returns(householdData);

        var handler = CreateHandler();
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
        _piiVisibilityService.GetVisibility(UserIalLevel.None).Returns(piiVisibility);
        _repository.GetHouseholdByIdentifierAsync(identifier, Arg.Any<PiiVisibility>(), Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
            .Returns(householdData);

        var handler = CreateHandler();
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

        var handler = CreateHandler();
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
        _piiVisibilityService.GetVisibility(UserIalLevel.IAL1plus)
            .Returns(new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true));
        _repository.GetHouseholdByIdentifierAsync(Arg.Any<HouseholdIdentifier>(), Arg.Any<PiiVisibility>(), Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
            .Returns((HouseholdData?)null);

        var handler = CreateHandler();
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
        _piiVisibilityService.GetVisibility(UserIalLevel.None).Returns(piiVisibility);
        _repository.GetHouseholdByIdentifierAsync(Arg.Any<HouseholdIdentifier>(), Arg.Any<PiiVisibility>(), Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
            .Returns(householdData);

        var handler = CreateHandler();
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
        _piiVisibilityService.GetVisibility(UserIalLevel.None).Returns(piiVisibility);
        _repository.GetHouseholdByIdentifierAsync(Arg.Any<HouseholdIdentifier>(), Arg.Any<PiiVisibility>(), Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
            .Returns(householdData);

        var handler = CreateHandler();
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
        _piiVisibilityService.GetVisibility(UserIalLevel.IAL1)
            .Returns(new PiiVisibility(IncludeAddress: false, IncludeEmail: true, IncludePhone: true));
        _repository.GetHouseholdByIdentifierAsync(Arg.Any<HouseholdIdentifier>(), Arg.Any<PiiVisibility>(), Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
            .Returns(householdData);
        _idProofingService.Evaluate(
            Arg.Any<ProtectedResource>(), Arg.Any<ProtectedAction>(),
            Arg.Any<UserIalLevel>(), Arg.Any<IReadOnlyList<SummerEbtCase>>())
            .Returns(new IdProofingDecision(IsAllowed: false, RequiredLevel: UserIalLevel.IAL1plus));

        var handler = CreateHandler();
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
        _piiVisibilityService.GetVisibility(UserIalLevel.IAL1plus)
            .Returns(new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true));
        _repository.GetHouseholdByIdentifierAsync(Arg.Any<HouseholdIdentifier>(), Arg.Any<PiiVisibility>(), Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
            .Returns(householdData);
        _idProofingService.Evaluate(
            Arg.Any<ProtectedResource>(), Arg.Any<ProtectedAction>(),
            Arg.Any<UserIalLevel>(), Arg.Any<IReadOnlyList<SummerEbtCase>>())
            .Returns(new IdProofingDecision(IsAllowed: true, RequiredLevel: UserIalLevel.IAL1plus));

        var handler = CreateHandler();
        var query = new GetHouseholdDataQuery { User = user };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var success = Assert.IsType<SuccessResult<HouseholdData>>(result);
        Assert.Same(householdData, success.Value);
    }

    [Fact]
    public async Task Handle_FiltersCoLoadedCases_WhenMixedEligibilityHousehold()
    {
        // Mixed households: hide co-loaded cases so the user only sees non-co-loaded ones.
        // Product intent (James/Devika, 2026-04-20): MVP does not visually support mixed
        // households; only the non-co-loaded subset reaches the client.
        var email = "user@example.com";
        var user = CreateUser(email, UserIalLevel.IAL1plus);
        var identifier = HouseholdIdentifier.Email(EmailNormalizer.Normalize(email));
        var householdData = new HouseholdData
        {
            Email = email,
            BenefitIssuanceType = BenefitIssuanceType.SnapEbtCard,
            SummerEbtCases = new List<SummerEbtCase>
            {
                new() { SummerEBTCaseID = "SEBT-COLOADED", ChildFirstName = "A", ChildLastName = "B", IsCoLoaded = true },
                new() { SummerEBTCaseID = "SEBT-REGULAR", ChildFirstName = "C", ChildLastName = "D", IsCoLoaded = false }
            }
        };

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(identifier);
        _piiVisibilityService.GetVisibility(UserIalLevel.IAL1plus)
            .Returns(new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true));
        _repository.GetHouseholdByIdentifierAsync(
                Arg.Any<HouseholdIdentifier>(), Arg.Any<PiiVisibility>(),
                Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
            .Returns(householdData);

        var handler = CreateHandler();
        var query = new GetHouseholdDataQuery { User = user };

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var success = Assert.IsType<SuccessResult<HouseholdData>>(result);
        Assert.Single(success.Value.SummerEbtCases);
        Assert.Equal("SEBT-REGULAR", success.Value.SummerEbtCases[0].SummerEBTCaseID);

        // Recompute the household-level type to match the filtered view.
        // Otherwise a downstream consumer keyed on BenefitIssuanceType (e.g. the
        // address-info page's co-loaded guard) would render guidance that doesn't
        // match what the user actually sees post-filter.
        Assert.Equal(BenefitIssuanceType.SummerEbt, success.Value.BenefitIssuanceType);
    }

    [Fact]
    public async Task Handle_AttachesPerCaseAllowedActions_FromEvaluator()
    {
        var email = "user@example.com";
        var user = CreateUser(email, UserIalLevel.IAL1plus);
        var identifier = HouseholdIdentifier.Email(EmailNormalizer.Normalize(email));
        var householdData = new HouseholdData
        {
            Email = email,
            SummerEbtCases = new List<SummerEbtCase>
            {
                new() { SummerEBTCaseID = "SEBT-001", ChildFirstName = "A", ChildLastName = "B", IsCoLoaded = false },
                new() { SummerEBTCaseID = "SEBT-002", ChildFirstName = "C", ChildLastName = "D", IsCoLoaded = false }
            }
        };

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>()).Returns(identifier);
        _piiVisibilityService.GetVisibility(UserIalLevel.IAL1plus)
            .Returns(new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true));
        _repository.GetHouseholdByIdentifierAsync(
                Arg.Any<HouseholdIdentifier>(), Arg.Any<PiiVisibility>(),
                Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
            .Returns(householdData);
        _selfServiceEvaluator.Evaluate(Arg.Is<SummerEbtCase>(c => c.SummerEBTCaseID == "SEBT-001"))
            .Returns(new AllowedActions { CanUpdateAddress = true, CanRequestReplacementCard = false });
        _selfServiceEvaluator.Evaluate(Arg.Is<SummerEbtCase>(c => c.SummerEBTCaseID == "SEBT-002"))
            .Returns(new AllowedActions { CanUpdateAddress = false, CanRequestReplacementCard = true });

        var handler = CreateHandler();
        var query = new GetHouseholdDataQuery { User = user };

        var result = await handler.Handle(query, CancellationToken.None);

        var success = Assert.IsType<SuccessResult<HouseholdData>>(result);
        var caseOne = success.Value.SummerEbtCases.First(c => c.SummerEBTCaseID == "SEBT-001");
        var caseTwo = success.Value.SummerEbtCases.First(c => c.SummerEBTCaseID == "SEBT-002");
        Assert.NotNull(caseOne.AllowedActions);
        Assert.True(caseOne.AllowedActions!.CanUpdateAddress);
        Assert.False(caseOne.AllowedActions.CanRequestReplacementCard);
        Assert.NotNull(caseTwo.AllowedActions);
        Assert.False(caseTwo.AllowedActions!.CanUpdateAddress);
        Assert.True(caseTwo.AllowedActions.CanRequestReplacementCard);
    }

    [Fact]
    public async Task Handle_ComputesHouseholdRollup_FromNonCoLoadedSubset()
    {
        var email = "user@example.com";
        var user = CreateUser(email, UserIalLevel.IAL1plus);
        var identifier = HouseholdIdentifier.Email(EmailNormalizer.Normalize(email));
        var householdData = new HouseholdData
        {
            Email = email,
            SummerEbtCases = new List<SummerEbtCase>
            {
                new() { SummerEBTCaseID = "SEBT-COLOADED", ChildFirstName = "A", ChildLastName = "B", IsCoLoaded = true },
                new() { SummerEBTCaseID = "SEBT-REGULAR", ChildFirstName = "C", ChildLastName = "D", IsCoLoaded = false }
            }
        };

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>()).Returns(identifier);
        _piiVisibilityService.GetVisibility(UserIalLevel.IAL1plus)
            .Returns(new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true));
        _repository.GetHouseholdByIdentifierAsync(
                Arg.Any<HouseholdIdentifier>(), Arg.Any<PiiVisibility>(),
                Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
            .Returns(householdData);

        var handler = CreateHandler();
        var query = new GetHouseholdDataQuery { User = user };

        await handler.Handle(query, CancellationToken.None);

        _selfServiceEvaluator.Received(1).EvaluateHousehold(
            Arg.Is<IReadOnlyList<SummerEbtCase>>(list =>
                list.Count == 1 && list[0].SummerEBTCaseID == "SEBT-REGULAR"));
    }

    [Fact]
    public async Task Handle_ReturnsCoLoadedCases_WhenAllCasesAreCoLoaded()
    {
        // Co-loaded-only households: show all cases (they're all the user has).
        // Per-case flags prevent actions; command handlers enforce server-side.
        var email = "user@example.com";
        var user = CreateUser(email, UserIalLevel.IAL1plus);
        var identifier = HouseholdIdentifier.Email(EmailNormalizer.Normalize(email));
        var householdData = new HouseholdData
        {
            Email = email,
            BenefitIssuanceType = BenefitIssuanceType.SnapEbtCard,
            SummerEbtCases = new List<SummerEbtCase>
            {
                new() { SummerEBTCaseID = "SEBT-001", ChildFirstName = "A", ChildLastName = "B", IsCoLoaded = true },
                new() { SummerEBTCaseID = "SEBT-002", ChildFirstName = "C", ChildLastName = "D", IsCoLoaded = true }
            }
        };

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(identifier);
        _piiVisibilityService.GetVisibility(UserIalLevel.IAL1plus)
            .Returns(new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true));
        _repository.GetHouseholdByIdentifierAsync(
                Arg.Any<HouseholdIdentifier>(), Arg.Any<PiiVisibility>(),
                Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
            .Returns(householdData);

        var handler = CreateHandler();
        var query = new GetHouseholdDataQuery { User = user };

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var success = Assert.IsType<SuccessResult<HouseholdData>>(result);
        Assert.Equal(2, success.Value.SummerEbtCases.Count);

        // Fully-co-loaded households: no filter runs, so the upstream plugin's
        // BenefitIssuanceType is preserved and drives downstream routing honestly.
        Assert.Equal(BenefitIssuanceType.SnapEbtCard, success.Value.BenefitIssuanceType);
    }

    [Fact]
    public async Task Handle_ClassifiesCohort_AsNonCoLoaded_WhenNoCasesAreCoLoaded()
    {
        // Household with only non-co-loaded cases should be classified NonCoLoaded,
        // and the full case list should be returned unchanged.
        // BenefitIssuanceType is not rewritten for this cohort (only MixedOrApplicantExcluded
        // suppression realigns it); upstream / connector value passes through unchanged.
        var email = "user@example.com";
        var user = CreateUser(email, UserIalLevel.IAL1plus);
        var identifier = HouseholdIdentifier.Email(EmailNormalizer.Normalize(email));
        var householdData = new HouseholdData
        {
            Email = email,
            BenefitIssuanceType = BenefitIssuanceType.SummerEbt,
            SummerEbtCases = new List<SummerEbtCase>
            {
                new() { SummerEBTCaseID = "SEBT-001", ChildFirstName = "A", ChildLastName = "B", IsCoLoaded = false },
                new() { SummerEBTCaseID = "SEBT-002", ChildFirstName = "C", ChildLastName = "D", IsCoLoaded = false }
            }
        };

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>()).Returns(identifier);
        _piiVisibilityService.GetVisibility(UserIalLevel.IAL1plus)
            .Returns(new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true));
        _repository.GetHouseholdByIdentifierAsync(
                Arg.Any<HouseholdIdentifier>(), Arg.Any<PiiVisibility>(),
                Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
            .Returns(householdData);

        var handler = CreateHandler();
        var query = new GetHouseholdDataQuery { User = user };

        var result = await handler.Handle(query, CancellationToken.None);

        var success = Assert.IsType<SuccessResult<HouseholdData>>(result);
        Assert.Equal(CoLoadedCohort.NonCoLoaded, success.Value.CoLoadedCohort);
        Assert.Equal(2, success.Value.SummerEbtCases.Count);
        Assert.Equal(BenefitIssuanceType.SummerEbt, success.Value.BenefitIssuanceType);
    }

    [Fact]
    public async Task Handle_ClassifiesCohort_AsCoLoadedOnly_WhenAllCasesAreCoLoadedAndNoApplications()
    {
        // Co-loaded-only households: cases are retained (they're all the user has);
        // classification enables analytics to segment this cohort distinctly from
        // the mixed/applicant-excluded cohort whose co-loaded cases are suppressed.
        var email = "user@example.com";
        var user = CreateUser(email, UserIalLevel.IAL1plus);
        var identifier = HouseholdIdentifier.Email(EmailNormalizer.Normalize(email));
        var householdData = new HouseholdData
        {
            Email = email,
            BenefitIssuanceType = BenefitIssuanceType.SnapEbtCard,
            SummerEbtCases = new List<SummerEbtCase>
            {
                new() { SummerEBTCaseID = "SEBT-001", ChildFirstName = "A", ChildLastName = "B", IsCoLoaded = true },
                new() { SummerEBTCaseID = "SEBT-002", ChildFirstName = "C", ChildLastName = "D", IsCoLoaded = true }
            }
        };

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>()).Returns(identifier);
        _piiVisibilityService.GetVisibility(UserIalLevel.IAL1plus)
            .Returns(new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true));
        _repository.GetHouseholdByIdentifierAsync(
                Arg.Any<HouseholdIdentifier>(), Arg.Any<PiiVisibility>(),
                Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
            .Returns(householdData);

        var handler = CreateHandler();
        var query = new GetHouseholdDataQuery { User = user };

        var result = await handler.Handle(query, CancellationToken.None);

        var success = Assert.IsType<SuccessResult<HouseholdData>>(result);
        Assert.Equal(CoLoadedCohort.CoLoadedOnly, success.Value.CoLoadedCohort);
        Assert.Equal(2, success.Value.SummerEbtCases.Count);
    }

    [Fact]
    public async Task Handle_ClassifiesCohort_AsCoLoadedOnly_WhenAllCasesCoLoadedAndHouseholdApplicationsAreHistoricalOnly()
    {
        // Household Applications often retains terminal rows (Approved/Denied/Cancelled).
        // Those alone must not classify as MixedOrApplicantExcluded when every case is co-loaded.
        var email = "user@example.com";
        var user = CreateUser(email, UserIalLevel.IAL1plus);
        var identifier = HouseholdIdentifier.Email(EmailNormalizer.Normalize(email));
        var householdData = new HouseholdData
        {
            Email = email,
            BenefitIssuanceType = BenefitIssuanceType.SnapEbtCard,
            SummerEbtCases = new List<SummerEbtCase>
            {
                new() { SummerEBTCaseID = "SEBT-001", ChildFirstName = "A", ChildLastName = "B", IsCoLoaded = true },
                new() { SummerEBTCaseID = "SEBT-002", ChildFirstName = "C", ChildLastName = "D", IsCoLoaded = true }
            },
            Applications = new List<Application>
            {
                new()
                {
                    ApplicationNumber = "APP-PAST",
                    ApplicationStatus = ApplicationStatus.Approved,
                    IssuanceType = IssuanceType.SummerEbt
                }
            }
        };

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>()).Returns(identifier);
        _piiVisibilityService.GetVisibility(UserIalLevel.IAL1plus)
            .Returns(new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true));
        _repository.GetHouseholdByIdentifierAsync(
                Arg.Any<HouseholdIdentifier>(), Arg.Any<PiiVisibility>(),
                Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
            .Returns(householdData);

        var handler = CreateHandler();
        var query = new GetHouseholdDataQuery { User = user };

        var result = await handler.Handle(query, CancellationToken.None);

        var success = Assert.IsType<SuccessResult<HouseholdData>>(result);
        Assert.Equal(CoLoadedCohort.CoLoadedOnly, success.Value.CoLoadedCohort);
        Assert.Equal(2, success.Value.SummerEbtCases.Count);
        Assert.Equal(BenefitIssuanceType.SnapEbtCard, success.Value.BenefitIssuanceType);
    }

    [Fact]
    public async Task Handle_ClassifiesCohort_AsMixedOrApplicantExcluded_WhenHouseholdHasCoLoadedAndNonCoLoadedCases()
    {
        // Mixed-eligibility family: co-loaded cases are suppressed and the
        // household is tagged for analytics as excluded.
        var email = "user@example.com";
        var user = CreateUser(email, UserIalLevel.IAL1plus);
        var identifier = HouseholdIdentifier.Email(EmailNormalizer.Normalize(email));
        var householdData = new HouseholdData
        {
            Email = email,
            SummerEbtCases = new List<SummerEbtCase>
            {
                new() { SummerEBTCaseID = "SEBT-COLOADED", ChildFirstName = "A", ChildLastName = "B", IsCoLoaded = true },
                new() { SummerEBTCaseID = "SEBT-REGULAR", ChildFirstName = "C", ChildLastName = "D", IsCoLoaded = false }
            }
        };

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>()).Returns(identifier);
        _piiVisibilityService.GetVisibility(UserIalLevel.IAL1plus)
            .Returns(new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true));
        _repository.GetHouseholdByIdentifierAsync(
                Arg.Any<HouseholdIdentifier>(), Arg.Any<PiiVisibility>(),
                Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
            .Returns(householdData);

        var handler = CreateHandler();
        var query = new GetHouseholdDataQuery { User = user };

        var result = await handler.Handle(query, CancellationToken.None);

        var success = Assert.IsType<SuccessResult<HouseholdData>>(result);
        Assert.Equal(CoLoadedCohort.MixedOrApplicantExcluded, success.Value.CoLoadedCohort);
        Assert.Single(success.Value.SummerEbtCases);
        Assert.Equal("SEBT-REGULAR", success.Value.SummerEbtCases[0].SummerEBTCaseID);
    }

    [Fact]
    public async Task Handle_ClassifiesCohort_AsMixedOrApplicantExcluded_WhenApplicantHasCoLoadedBenefits()
    {
        // Applicant-with-co-loaded: all cases are co-loaded but the household has
        // a pending application. The user is on the applicant journey, so co-loaded
        // cases are suppressed and they see only their application view.
        var email = "user@example.com";
        var user = CreateUser(email, UserIalLevel.IAL1plus);
        var identifier = HouseholdIdentifier.Email(EmailNormalizer.Normalize(email));
        var householdData = new HouseholdData
        {
            Email = email,
            SummerEbtCases = new List<SummerEbtCase>
            {
                new() { SummerEBTCaseID = "SEBT-COLOADED", ChildFirstName = "A", ChildLastName = "B", IsCoLoaded = true }
            },
            Applications = new List<Application>
            {
                new() { ApplicationNumber = "APP-1", ApplicationStatus = ApplicationStatus.Pending }
            }
        };

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>()).Returns(identifier);
        _piiVisibilityService.GetVisibility(UserIalLevel.IAL1plus)
            .Returns(new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true));
        _repository.GetHouseholdByIdentifierAsync(
                Arg.Any<HouseholdIdentifier>(), Arg.Any<PiiVisibility>(),
                Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
            .Returns(householdData);

        var handler = CreateHandler();
        var query = new GetHouseholdDataQuery { User = user };

        var result = await handler.Handle(query, CancellationToken.None);

        var success = Assert.IsType<SuccessResult<HouseholdData>>(result);
        Assert.Equal(CoLoadedCohort.MixedOrApplicantExcluded, success.Value.CoLoadedCohort);
        Assert.Empty(success.Value.SummerEbtCases);
        Assert.Single(success.Value.Applications);
    }

    [Fact]
    public async Task Handle_ClassifiesCohort_AsMixedOrApplicantExcluded_WhenCoLoadedCaseHasPendingApplicationStatus()
    {
        // Applicant-with-co-loaded variant: no separate Applications[] entry, but
        // the co-loaded case itself carries ApplicationStatus.Pending. This still
        // represents an applicant and must be treated as the excluded cohort.
        var email = "user@example.com";
        var user = CreateUser(email, UserIalLevel.IAL1plus);
        var identifier = HouseholdIdentifier.Email(EmailNormalizer.Normalize(email));
        var householdData = new HouseholdData
        {
            Email = email,
            SummerEbtCases = new List<SummerEbtCase>
            {
                new()
                {
                    SummerEBTCaseID = "SEBT-COLOADED",
                    ChildFirstName = "A",
                    ChildLastName = "B",
                    IsCoLoaded = true,
                    ApplicationStatus = ApplicationStatus.Pending
                }
            }
        };

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>()).Returns(identifier);
        _piiVisibilityService.GetVisibility(UserIalLevel.IAL1plus)
            .Returns(new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true));
        _repository.GetHouseholdByIdentifierAsync(
                Arg.Any<HouseholdIdentifier>(), Arg.Any<PiiVisibility>(),
                Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
            .Returns(householdData);

        var handler = CreateHandler();
        var query = new GetHouseholdDataQuery { User = user };

        var result = await handler.Handle(query, CancellationToken.None);

        var success = Assert.IsType<SuccessResult<HouseholdData>>(result);
        Assert.Equal(CoLoadedCohort.MixedOrApplicantExcluded, success.Value.CoLoadedCohort);
        Assert.Empty(success.Value.SummerEbtCases);
    }

    [Fact]
    public async Task Handle_WhenSuppressCoLoadedCasesDisabled_KeepsMixedCohortCasesAndUpstreamBenefitIssuanceType()
    {
        var email = "user@example.com";
        var user = CreateUser(email, UserIalLevel.IAL1plus);
        var identifier = HouseholdIdentifier.Email(EmailNormalizer.Normalize(email));
        var householdData = new HouseholdData
        {
            Email = email,
            BenefitIssuanceType = BenefitIssuanceType.SnapEbtCard,
            SummerEbtCases = new List<SummerEbtCase>
            {
                new() { SummerEBTCaseID = "SEBT-COLOADED", ChildFirstName = "A", ChildLastName = "B", IsCoLoaded = true },
                new() { SummerEBTCaseID = "SEBT-REGULAR", ChildFirstName = "C", ChildLastName = "D", IsCoLoaded = false }
            }
        };

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>()).Returns(identifier);
        _piiVisibilityService.GetVisibility(UserIalLevel.IAL1plus)
            .Returns(new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true));
        _repository.GetHouseholdByIdentifierAsync(
                Arg.Any<HouseholdIdentifier>(), Arg.Any<PiiVisibility>(),
                Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
            .Returns(householdData);

        var cohortFilter = new CoLoadedCohortFilterSettings { SuppressCoLoadedCasesForExcludedCohort = false };
        var handler = CreateHandler(cohortFilter);
        var query = new GetHouseholdDataQuery { User = user };

        var result = await handler.Handle(query, CancellationToken.None);

        var success = Assert.IsType<SuccessResult<HouseholdData>>(result);
        Assert.Equal(CoLoadedCohort.MixedOrApplicantExcluded, success.Value.CoLoadedCohort);
        Assert.Equal(2, success.Value.SummerEbtCases.Count);
        Assert.Contains(success.Value.SummerEbtCases, c => c.SummerEBTCaseID == "SEBT-COLOADED");
        Assert.Equal(BenefitIssuanceType.SnapEbtCard, success.Value.BenefitIssuanceType);
    }

    [Fact]
    public async Task Handle_WhenSuppressCoLoadedCasesDisabled_KeepsApplicantCoLoadedCases()
    {
        var email = "user@example.com";
        var user = CreateUser(email, UserIalLevel.IAL1plus);
        var identifier = HouseholdIdentifier.Email(EmailNormalizer.Normalize(email));
        var householdData = new HouseholdData
        {
            Email = email,
            SummerEbtCases = new List<SummerEbtCase>
            {
                new() { SummerEBTCaseID = "SEBT-COLOADED", ChildFirstName = "A", ChildLastName = "B", IsCoLoaded = true }
            },
            Applications = new List<Application>
            {
                new() { ApplicationNumber = "APP-1", ApplicationStatus = ApplicationStatus.Pending }
            }
        };

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>()).Returns(identifier);
        _piiVisibilityService.GetVisibility(UserIalLevel.IAL1plus)
            .Returns(new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true));
        _repository.GetHouseholdByIdentifierAsync(
                Arg.Any<HouseholdIdentifier>(), Arg.Any<PiiVisibility>(),
                Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
            .Returns(householdData);

        var cohortFilter = new CoLoadedCohortFilterSettings { SuppressCoLoadedCasesForExcludedCohort = false };
        var handler = CreateHandler(cohortFilter);
        var query = new GetHouseholdDataQuery { User = user };

        var result = await handler.Handle(query, CancellationToken.None);

        var success = Assert.IsType<SuccessResult<HouseholdData>>(result);
        Assert.Equal(CoLoadedCohort.MixedOrApplicantExcluded, success.Value.CoLoadedCohort);
        Assert.Single(success.Value.SummerEbtCases);
        Assert.Equal("SEBT-COLOADED", success.Value.SummerEbtCases[0].SummerEBTCaseID);
        Assert.Single(success.Value.Applications);
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
        _piiVisibilityService.GetVisibility(UserIalLevel.IAL1plus)
            .Returns(new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true));
        _repository.GetHouseholdByIdentifierAsync(Arg.Any<HouseholdIdentifier>(), Arg.Any<PiiVisibility>(), Arg.Any<UserIalLevel>(), token)
            .Returns(householdData);

        var handler = CreateHandler();
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
