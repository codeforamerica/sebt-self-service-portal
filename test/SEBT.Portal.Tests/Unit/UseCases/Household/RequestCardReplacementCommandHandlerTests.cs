using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.UseCases.Household;

namespace SEBT.Portal.Tests.Unit.UseCases.Household;

public class RequestCardReplacementCommandHandlerTests
{
    private readonly IValidator<RequestCardReplacementCommand> _validator =
        new DataAnnotationsValidator<RequestCardReplacementCommand>(null!);
    private readonly IHouseholdIdentifierResolver _resolver =
        Substitute.For<IHouseholdIdentifierResolver>();
    private readonly IHouseholdRepository _repository =
        Substitute.For<IHouseholdRepository>();
    private readonly NullLogger<RequestCardReplacementCommandHandler> _logger =
        NullLogger<RequestCardReplacementCommandHandler>.Instance;

    private RequestCardReplacementCommandHandler CreateHandler(TimeProvider? timeProvider = null) =>
        new(_validator, _resolver, _repository, timeProvider ?? TimeProvider.System, _logger);

    private static ClaimsPrincipal CreateUser(string email, string? ialClaim = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.Email, email) };
        if (ialClaim != null)
            claims.Add(new Claim(JwtClaimTypes.Ial, ialClaim));
        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }

    private static RequestCardReplacementCommand CreateValidCommand(
        ClaimsPrincipal? user = null,
        List<string>? applicationNumbers = null) =>
        new()
        {
            User = user ?? CreateUser("user@example.com"),
            ApplicationNumbers = applicationNumbers ?? new List<string> { "APP-2026-001" }
        };

    private static HouseholdData CreateHouseholdWithApplications(params Application[] applications) =>
        new()
        {
            Applications = applications.ToList()
        };

    private void SetupResolverSuccess()
    {
        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(HouseholdIdentifier.Email(EmailNormalizer.Normalize("user@example.com")));
    }

    private void SetupRepositoryReturns(HouseholdData householdData)
    {
        _repository.GetHouseholdByIdentifierAsync(
            Arg.Any<HouseholdIdentifier>(),
            Arg.Any<PiiVisibility>(),
            Arg.Any<UserIalLevel>(),
            Arg.Any<CancellationToken>()
        ).Returns(householdData);
    }

    // --- Validation tests ---

    [Fact]
    public async Task Handle_ReturnsValidationFailed_WhenApplicationNumbersIsEmpty()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand(applicationNumbers: new List<string>());

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<ValidationFailedResult>(result);
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

    [Fact]
    public async Task Handle_DoesNotCallResolver_WhenValidationFails()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand(applicationNumbers: new List<string>());

        await handler.Handle(command, CancellationToken.None);

        await _resolver.DidNotReceive()
            .ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsPreconditionFailed_WhenHouseholdDataNotFound()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();
        SetupResolverSuccess();
        // Repository returns null by default (NSubstitute unconfigured)

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<PreconditionFailedResult>(result);
    }

    // --- Cooldown tests ---

    [Fact]
    public async Task Handle_ReturnsValidationFailed_WhenApplicationIsWithinCooldownPeriod()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();
        SetupResolverSuccess();
        SetupRepositoryReturns(CreateHouseholdWithApplications(
            new Application
            {
                ApplicationNumber = "APP-2026-001",
                CardStatus = CardStatus.Requested,
                CardRequestedAt = DateTime.UtcNow.AddDays(-3)
            }
        ));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<ValidationFailedResult>(result);
    }

    [Fact]
    public async Task Handle_ReturnsValidationFailed_WhenMailedCardIsStillWithinCooldown()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();
        SetupResolverSuccess();
        SetupRepositoryReturns(CreateHouseholdWithApplications(
            new Application
            {
                ApplicationNumber = "APP-2026-001",
                CardStatus = CardStatus.Mailed,
                CardRequestedAt = DateTime.UtcNow.AddDays(-10)
            }
        ));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<ValidationFailedResult>(result);
    }

    [Fact]
    public async Task Handle_ReturnsSuccess_WhenApplicationIsOutsideCooldownPeriod()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();
        SetupResolverSuccess();
        SetupRepositoryReturns(CreateHouseholdWithApplications(
            new Application
            {
                ApplicationNumber = "APP-2026-001",
                CardStatus = CardStatus.Active,
                CardRequestedAt = DateTime.UtcNow.AddDays(-30)
            }
        ));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.IsType<SuccessResult>(result);
    }

    [Fact]
    public async Task Handle_ReturnsSuccess_WhenCardRequestedAtIsNull()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();
        SetupResolverSuccess();
        SetupRepositoryReturns(CreateHouseholdWithApplications(
            new Application
            {
                ApplicationNumber = "APP-2026-001",
                CardStatus = CardStatus.Active,
                CardRequestedAt = null
            }
        ));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_ReturnsValidationFailed_WhenApplicationNumberNotInHousehold()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand(applicationNumbers: new List<string> { "APP-UNKNOWN" });
        SetupResolverSuccess();
        SetupRepositoryReturns(CreateHouseholdWithApplications(
            new Application
            {
                ApplicationNumber = "APP-2026-001",
                CardStatus = CardStatus.Active,
                CardRequestedAt = DateTime.UtcNow.AddDays(-30)
            }
        ));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<ValidationFailedResult>(result);
    }

    // --- Success tests ---

    [Fact]
    public async Task Handle_ReturnsSuccess_WhenValidCommandAndNoActiveCooldown()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand(applicationNumbers: new List<string> { "APP-001", "APP-002" });
        SetupResolverSuccess();
        SetupRepositoryReturns(CreateHouseholdWithApplications(
            new Application
            {
                ApplicationNumber = "APP-001",
                CardStatus = CardStatus.Active,
                CardRequestedAt = DateTime.UtcNow.AddDays(-30)
            },
            new Application
            {
                ApplicationNumber = "APP-002",
                CardStatus = CardStatus.Active,
                CardRequestedAt = DateTime.UtcNow.AddDays(-20)
            }
        ));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.IsType<SuccessResult>(result);
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

        SetupRepositoryReturns(CreateHouseholdWithApplications(
            new Application
            {
                ApplicationNumber = "APP-2026-001",
                CardRequestedAt = DateTime.UtcNow.AddDays(-30)
            }
        ));

        await handler.Handle(command, token);

        await _resolver.Received(1).ResolveAsync(Arg.Any<ClaimsPrincipal>(), token);
    }

    // --- IAL propagation tests ---

    [Fact]
    public async Task Handle_PassesUserIalLevelToRepository()
    {
        var handler = CreateHandler();
        var user = CreateUser("user@example.com", ialClaim: "1plus");
        var command = CreateValidCommand(user: user);
        SetupResolverSuccess();
        SetupRepositoryReturns(CreateHouseholdWithApplications(
            new Application
            {
                ApplicationNumber = "APP-2026-001",
                CardRequestedAt = DateTime.UtcNow.AddDays(-30)
            }
        ));

        await handler.Handle(command, CancellationToken.None);

        await _repository.Received(1).GetHouseholdByIdentifierAsync(
            Arg.Any<HouseholdIdentifier>(),
            Arg.Any<PiiVisibility>(),
            UserIalLevel.IAL1plus,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesIalNone_WhenNoIalClaim()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();
        SetupResolverSuccess();
        SetupRepositoryReturns(CreateHouseholdWithApplications(
            new Application
            {
                ApplicationNumber = "APP-2026-001",
                CardRequestedAt = DateTime.UtcNow.AddDays(-30)
            }
        ));

        await handler.Handle(command, CancellationToken.None);

        await _repository.Received(1).GetHouseholdByIdentifierAsync(
            Arg.Any<HouseholdIdentifier>(),
            Arg.Any<PiiVisibility>(),
            UserIalLevel.None,
            Arg.Any<CancellationToken>());
    }

    // --- Cooldown boundary tests (using FakeTimeProvider) ---

    [Fact]
    public async Task Handle_ReturnsValidationFailed_WhenCardRequestedExactly13DaysAgo()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero));
        var handler = CreateHandler(fakeTime);
        var command = CreateValidCommand();
        SetupResolverSuccess();
        SetupRepositoryReturns(CreateHouseholdWithApplications(
            new Application
            {
                ApplicationNumber = "APP-2026-001",
                CardStatus = CardStatus.Requested,
                CardRequestedAt = new DateTime(2026, 6, 2, 12, 0, 0, DateTimeKind.Utc)
            }
        ));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<ValidationFailedResult>(result);
    }

    [Fact]
    public async Task Handle_ReturnsSuccess_WhenCardRequestedExactly14DaysAgo()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero));
        var handler = CreateHandler(fakeTime);
        var command = CreateValidCommand();
        SetupResolverSuccess();
        SetupRepositoryReturns(CreateHouseholdWithApplications(
            new Application
            {
                ApplicationNumber = "APP-2026-001",
                CardStatus = CardStatus.Active,
                CardRequestedAt = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc)
            }
        ));

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.IsType<SuccessResult>(result);
    }
}
