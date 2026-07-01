using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.UseCases.IdProofing;

namespace SEBT.Portal.Tests.Unit.UseCases.IdProofing;

public class IdProofingHouseholdLookupTests
{
    private readonly IHouseholdRepository householdRepository = Substitute.For<IHouseholdRepository>();
    private readonly ILogger logger = Substitute.For<ILogger>();
    private readonly User user = new()
    {
        Id = Guid.CreateVersion7(),
        Email = "user@example.com"
    };
    private readonly Guid portalUserId = Guid.CreateVersion7();

    [Fact]
    public async Task TryGetByEmailForCohortCheckAsync_ReturnsFound_WhenHouseholdExists()
    {
        var household = new HouseholdData
        {
            SummerEbtCases =
            [
                new SummerEbtCase
                {
                    SummerEBTCaseID = "S1",
                    ChildFirstName = "A",
                    ChildLastName = "B",
                    IsCoLoaded = true
                }
            ]
        };

        householdRepository.GetHouseholdByEmailAsync(
                user.Email!,
                Arg.Any<PiiVisibility>(),
                Arg.Any<UserIalLevel>(),
                portalUserId,
                includeCardService: false,
                Arg.Any<CancellationToken>())
            .Returns(household);

        var result = await IdProofingHouseholdLookup.TryGetByEmailForCohortCheckAsync(
            householdRepository,
            logger,
            user,
            UserIalLevel.IAL1plus,
            portalUserId,
            CancellationToken.None);

        Assert.Equal(IdProofingHouseholdLookupOutcome.Found, result.Outcome);
        Assert.Same(household, result.Household);
    }

    [Fact]
    public async Task TryGetByEmailForCohortCheckAsync_ReturnsNotFound_AndLogsInformation_WhenHouseholdIsMissing()
    {
        householdRepository.GetHouseholdByEmailAsync(
                user.Email!,
                Arg.Any<PiiVisibility>(),
                Arg.Any<UserIalLevel>(),
                portalUserId,
                includeCardService: false,
                Arg.Any<CancellationToken>())
            .Returns((HouseholdData?)null);

        var result = await IdProofingHouseholdLookup.TryGetByEmailForCohortCheckAsync(
            householdRepository,
            logger,
            user,
            UserIalLevel.IAL1plus,
            portalUserId,
            CancellationToken.None);

        Assert.Equal(IdProofingHouseholdLookupOutcome.NotFound, result.Outcome);
        Assert.Null(result.Household);
        logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("No household found")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task TryGetByEmailForCohortCheckAsync_ReturnsFailed_AndLogsError_WhenRepositoryThrows()
    {
        var exception = new InvalidOperationException("upstream unavailable");
        householdRepository.GetHouseholdByEmailAsync(
                user.Email!,
                Arg.Any<PiiVisibility>(),
                Arg.Any<UserIalLevel>(),
                portalUserId,
                includeCardService: false,
                Arg.Any<CancellationToken>())
            .Throws(exception);

        var result = await IdProofingHouseholdLookup.TryGetByEmailForCohortCheckAsync(
            householdRepository,
            logger,
            user,
            UserIalLevel.IAL1plus,
            portalUserId,
            CancellationToken.None);

        Assert.Equal(IdProofingHouseholdLookupOutcome.Failed, result.Outcome);
        Assert.Null(result.Household);
        logger.Received().Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Household lookup failed")),
            exception,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ResolveOffboardingReasonAsync_ReturnsDefaultReason_WhenLookupFails()
    {
        householdRepository.GetHouseholdByEmailAsync(
                user.Email!,
                Arg.Any<PiiVisibility>(),
                Arg.Any<UserIalLevel>(),
                portalUserId,
                includeCardService: false,
                Arg.Any<CancellationToken>())
            .Throws(new TimeoutException("warehouse timeout"));

        var reason = await IdProofingHouseholdLookup.ResolveOffboardingReasonAsync(
            householdRepository,
            logger,
            user,
            UserIalLevel.IAL1plus,
            portalUserId,
            "idProofingFailed",
            CancellationToken.None);

        Assert.Equal("idProofingFailed", reason);
    }

    [Fact]
    public async Task ResolveOffboardingReasonAsync_ReturnsCoLoadedOnly_WhenHouseholdIsCoLoadedOnly()
    {
        householdRepository.GetHouseholdByEmailAsync(
                user.Email!,
                Arg.Any<PiiVisibility>(),
                Arg.Any<UserIalLevel>(),
                portalUserId,
                includeCardService: false,
                Arg.Any<CancellationToken>())
            .Returns(new HouseholdData
            {
                SummerEbtCases =
                [
                    new SummerEbtCase
                    {
                        SummerEBTCaseID = "S1",
                        ChildFirstName = "A",
                        ChildLastName = "B",
                        IsCoLoaded = true
                    }
                ]
            });

        var reason = await IdProofingHouseholdLookup.ResolveOffboardingReasonAsync(
            householdRepository,
            logger,
            user,
            UserIalLevel.IAL1plus,
            portalUserId,
            "idProofingFailed",
            CancellationToken.None);

        Assert.Equal("coLoadedOnly", reason);
    }
}
