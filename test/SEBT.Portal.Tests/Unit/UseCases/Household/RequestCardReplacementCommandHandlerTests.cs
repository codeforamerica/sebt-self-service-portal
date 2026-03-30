using System.Security.Claims;
using Microsoft.Extensions.Logging.Abstractions;
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
    private readonly ISelfServiceEvaluator _evaluator =
        Substitute.For<ISelfServiceEvaluator>();
    private readonly NullLogger<RequestCardReplacementCommandHandler> _logger =
        NullLogger<RequestCardReplacementCommandHandler>.Instance;

    public RequestCardReplacementCommandHandlerTests()
    {
        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns(HouseholdIdentifier.Email(EmailNormalizer.Normalize("user@example.com")));

        _repository.GetHouseholdByIdentifierAsync(
                Arg.Any<HouseholdIdentifier>(), Arg.Any<PiiVisibility>(), Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
            .Returns(new HouseholdData
            {
                BenefitIssuanceType = BenefitIssuanceType.SummerEbt,
                Applications = new List<Application>
                {
                    new() { IssuanceType = IssuanceType.SummerEbt, CardStatus = CardStatus.Lost }
                }
            });

        _evaluator.Evaluate(Arg.Any<BenefitIssuanceType>(), Arg.Any<IReadOnlyList<Application>>())
            .Returns(new AllowedActions { CanUpdateAddress = true, CanRequestReplacementCard = true });
    }

    private RequestCardReplacementCommandHandler CreateHandler() =>
        new(_validator, _resolver, _repository, _evaluator, _logger);

    private static ClaimsPrincipal CreateUser(string email)
    {
        var claims = new List<Claim> { new(ClaimTypes.Email, email) };
        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }

    private static RequestCardReplacementCommand CreateValidCommand(ClaimsPrincipal? user = null) =>
        new()
        {
            User = user ?? CreateUser("user@example.com"),
            ApplicationNumber = "APP-2026-001"
        };

    // --- Validation ---

    [Fact]
    public async Task Handle_ReturnsValidationFailed_WhenApplicationNumberMissing()
    {
        var handler = CreateHandler();
        var command = new RequestCardReplacementCommand
        {
            User = CreateUser("user@example.com"),
            ApplicationNumber = ""
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<ValidationFailedResult>(result);
    }

    // --- Authorization ---

    [Fact]
    public async Task Handle_ReturnsUnauthorized_WhenIdentifierCannotBeResolved()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        _resolver.ResolveAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<CancellationToken>())
            .Returns((HouseholdIdentifier?)null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.IsType<UnauthorizedResult>(result);
    }

    // --- Self-service rules enforcement ---

    [Fact]
    public async Task Handle_ReturnsNotAllowed_WhenEvaluatorDeniesCardReplacement()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        _evaluator.Evaluate(Arg.Any<BenefitIssuanceType>(), Arg.Any<IReadOnlyList<Application>>())
            .Returns(new AllowedActions
            {
                CanRequestReplacementCard = false,
                CardReplacementDeniedMessageKey = "selfServiceUnavailable"
            });

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var preconditionFailed = Assert.IsType<PreconditionFailedResult>(result);
        Assert.Equal(PreconditionFailedReason.NotAllowed, preconditionFailed.Reason);
    }

    [Fact]
    public async Task Handle_ReturnsNotAllowed_WhenHouseholdNotFound()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        _repository.GetHouseholdByIdentifierAsync(
                Arg.Any<HouseholdIdentifier>(), Arg.Any<PiiVisibility>(), Arg.Any<UserIalLevel>(), Arg.Any<CancellationToken>())
            .Returns((HouseholdData?)null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var preconditionFailed = Assert.IsType<PreconditionFailedResult>(result);
        Assert.Equal(PreconditionFailedReason.NotAllowed, preconditionFailed.Reason);
    }

    // --- Success ---

    [Fact]
    public async Task Handle_ReturnsSuccess_WhenAllowed()
    {
        var handler = CreateHandler();
        var command = CreateValidCommand();

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.IsType<SuccessResult>(result);
    }
}
