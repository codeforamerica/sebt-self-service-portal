using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.DocVerification;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.TestUtilities.Helpers;
using SEBT.Portal.UseCases.IdProofing;

namespace SEBT.Portal.Tests.Unit.UseCases.IdProofing;

public class StartChallengeUserLookupTests
{
    private readonly IDocVerificationChallengeRepository challengeRepository =
        Substitute.For<IDocVerificationChallengeRepository>();
    private readonly IUserRepository userRepository = Substitute.For<IUserRepository>();
    private readonly IHouseholdRepository householdRepository = Substitute.For<IHouseholdRepository>();
    private readonly ISocureClient socureClient = Substitute.For<ISocureClient>();
    private readonly SocureSettings socureSettings = new();
    private readonly IValidator<StartChallengeCommand> validator =
        new DataAnnotationsValidator<StartChallengeCommand>(null!);
    private readonly NullLogger<StartChallengeCommandHandler> logger =
        NullLogger<StartChallengeCommandHandler>.Instance;

    private StartChallengeCommandHandler CreateHandler() =>
        new(
            challengeRepository,
            userRepository,
            householdRepository,
            socureClient,
            socureSettings,
            Options.Create(new IdProofingEligibilitySettings()),
            validator,
            logger);

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenUserDoesNotExist()
    {
        var handler = CreateHandler();
        var userId = Guid.CreateVersion7();
        var challenge = DocVerificationChallengeFactory.CreateChallengeForUser(userId);
        var command = new StartChallengeCommand
        {
            ChallengeId = challenge.PublicId,
            UserId = userId
        };

        challengeRepository.GetByPublicIdAsync(command.ChallengeId, command.UserId, Arg.Any<CancellationToken>())
            .Returns(challenge);
        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var preconditionFailed = Assert.IsType<PreconditionFailedResult<StartChallengeResponse>>(result);
        Assert.Equal(PreconditionFailedReason.NotFound, preconditionFailed.Reason);
        Assert.Contains("User not found", preconditionFailed.Message, StringComparison.OrdinalIgnoreCase);

        await socureClient.DidNotReceive()
            .StartDocvSessionAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await socureClient.DidNotReceive()
            .RunIdProofingAssessmentAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(), Arg.Any<string?>(),
                Arg.Any<CancellationToken>());
        await challengeRepository.DidNotReceive()
            .UpdateAsync(Arg.Any<DocVerificationChallenge>(), Arg.Any<CancellationToken>());
    }
}
