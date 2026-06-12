using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.DocVerification;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.UseCases.IdProofing;

namespace SEBT.Portal.Tests.Unit.UseCases.IdProofing;

public class SubmitIdProofingDocvCooldownTests
{
    private readonly IUserRepository userRepository = Substitute.For<IUserRepository>();
    private readonly IHouseholdRepository householdRepository = Substitute.For<IHouseholdRepository>();
    private readonly IDocVerificationChallengeRepository challengeRepository =
        Substitute.For<IDocVerificationChallengeRepository>();
    private readonly ISocureClient socureClient = Substitute.For<ISocureClient>();
    private readonly SocureSettings socureSettings = new();
    private readonly IValidator<SubmitIdProofingCommand> validator =
        new DataAnnotationsValidator<SubmitIdProofingCommand>(null!);
    private readonly NullLogger<SubmitIdProofingCommandHandler> logger =
        NullLogger<SubmitIdProofingCommandHandler>.Instance;

    private SubmitIdProofingCommandHandler CreateHandler() =>
        new(
            userRepository,
            householdRepository,
            challengeRepository,
            socureClient,
            socureSettings,
            validator,
            Options.Create(new IdProofingEligibilitySettings { RequireQualifyingHouseholdForSocure = true }),
            logger);

    [Fact]
    public async Task Handle_ShouldBlockSubmission_WhenUserIsInDocvEgregiousReasonCooldown()
    {
        var handler = CreateHandler();
        var command = new SubmitIdProofingCommand
        {
            UserId = Guid.CreateVersion7(),
            DateOfBirth = "1990-01-01",
            IdType = "ssn",
            IdValue = "123456789"
        };

        userRepository.GetUserByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(new User
            {
                Id = command.UserId,
                Email = "test@example.com",
                IdProofingCooldownUntil = DateTime.UtcNow.AddDays(7)
            });
        challengeRepository.GetActiveByUserIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns((DocVerificationChallenge?)null);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var response = result.Value;
        Assert.Equal("failed", response.Result);
        Assert.Equal(SocureDocvEgregiousReasonCooldown.OffboardingReason, response.OffboardingReason);
        Assert.False(response.AllowIdRetry);

        await socureClient.DidNotReceive()
            .RunIdProofingAssessmentAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<Address?>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
