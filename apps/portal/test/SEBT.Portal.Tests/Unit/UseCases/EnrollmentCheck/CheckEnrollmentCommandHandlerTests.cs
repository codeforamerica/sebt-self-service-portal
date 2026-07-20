using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.EnrollmentCheck;
using SEBT.Portal.Core.Services;
using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;
using SEBT.Portal.UseCases.EnrollmentCheck;

namespace SEBT.Portal.Tests.Unit.UseCases.EnrollmentCheck;

public class CheckEnrollmentCommandHandlerTests
{
    private readonly IEnrollmentCheckService _enrollmentCheckService = Substitute.For<IEnrollmentCheckService>();
    private readonly IEnrollmentCheckSubmissionLogger _submissionLogger = Substitute.For<IEnrollmentCheckSubmissionLogger>();
    private readonly ILogger<CheckEnrollmentCommandHandler> _logger = Substitute.For<ILogger<CheckEnrollmentCommandHandler>>();
    private readonly IFeatureManager _featureManager = Substitute.For<IFeatureManager>();

    private CheckEnrollmentCommandHandler CreateHandler() =>
        new(_enrollmentCheckService, _submissionLogger, _logger, _featureManager);

    [Fact]
    public async Task Handle_WhenNoChildren_ReturnsValidationFailed()
    {
        var handler = CreateHandler();
        var command = new CheckEnrollmentCommand
        {
            Children = new List<CheckEnrollmentCommand.ChildInput>(),
            IpAddress = "127.0.0.1"
        };

        var result = await handler.Handle(command);

        Assert.False(result.IsSuccess);
        Assert.IsType<Portal.Kernel.Results.ValidationFailedResult<EnrollmentCheckResult>>(result);
    }

    [Fact]
    public async Task Handle_WhenTooManyChildren_ReturnsValidationFailed()
    {
        var handler = CreateHandler();
        var children = Enumerable.Range(0, 21).Select(i => new CheckEnrollmentCommand.ChildInput
        {
            FirstName = $"Child{i}",
            LastName = "Doe",
            DateOfBirth = new DateOnly(2015, 1, 1)
        }).ToList();
        var command = new CheckEnrollmentCommand
        {
            Children = children,
            IpAddress = "127.0.0.1"
        };

        var result = await handler.Handle(command);

        Assert.False(result.IsSuccess);
        Assert.IsType<Portal.Kernel.Results.ValidationFailedResult<EnrollmentCheckResult>>(result);
    }

    [Fact]
    public async Task Handle_WithValidChild_CallsPluginAndReturnsResults()
    {
        var handler = CreateHandler();
        var command = new CheckEnrollmentCommand
        {
            Children = new List<CheckEnrollmentCommand.ChildInput>
            {
                new()
                {
                    FirstName = "Jane",
                    LastName = "Doe",
                    DateOfBirth = new DateOnly(2015, 3, 12),
                    SchoolName = "Lincoln Elementary"
                }
            },
            IpAddress = "127.0.0.1"
        };

        _enrollmentCheckService
            .CheckEnrollmentAsync(Arg.Any<EnrollmentCheckRequest>(), Arg.Any<CancellationToken>())
            .Returns(new EnrollmentCheckResult
            {
                Results = new List<ChildCheckResult>
                {
                    new()
                    {
                        CheckId = Guid.NewGuid(),
                        FirstName = "Jane",
                        LastName = "Doe",
                        DateOfBirth = new DateOnly(2015, 3, 12),
                        Status = EnrollmentStatus.Match,
                        SchoolName = "Lincoln Elementary"
                    }
                }
            });

        var result = await handler.Handle(command);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Results);
        Assert.Equal(EnrollmentStatus.Match, result.Value.Results[0].Status);
    }

    [Fact]
    public async Task Handle_LogsDeidentifiedSubmission()
    {
        var handler = CreateHandler();
        var command = new CheckEnrollmentCommand
        {
            Children = new List<CheckEnrollmentCommand.ChildInput>
            {
                new()
                {
                    FirstName = "Jane",
                    LastName = "Doe",
                    DateOfBirth = new DateOnly(2015, 3, 12),
                    SchoolName = "Lincoln Elementary"
                }
            },
            IpAddress = "127.0.0.1"
        };

        _enrollmentCheckService
            .CheckEnrollmentAsync(Arg.Any<EnrollmentCheckRequest>(), Arg.Any<CancellationToken>())
            .Returns(new EnrollmentCheckResult
            {
                Results = new List<ChildCheckResult>
                {
                    new()
                    {
                        CheckId = Guid.NewGuid(),
                        FirstName = "Jane",
                        LastName = "Doe",
                        DateOfBirth = new DateOnly(2015, 3, 12),
                        Status = EnrollmentStatus.Match,
                        SchoolName = "Lincoln Elementary",
                        EligibilityType = EligibilityType.Snap
                    }
                }
            });

        await handler.Handle(command);

        await _submissionLogger.Received(1).LogSubmissionAsync(
            Arg.Is<EnrollmentCheckSubmission>(s =>
                s.ChildrenChecked == 1 &&
                s.ChildResults[0].BirthYear == 2015 &&
                s.ChildResults[0].Status == "Match" &&
                s.ChildResults[0].SchoolName == "Lincoln Elementary"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenPluginThrows_ReturnsDependencyFailed()
    {
        var handler = CreateHandler();
        var command = new CheckEnrollmentCommand
        {
            Children = new List<CheckEnrollmentCommand.ChildInput>
            {
                new()
                {
                    FirstName = "Jane",
                    LastName = "Doe",
                    DateOfBirth = new DateOnly(2015, 3, 12),
                    SchoolName = "Lincoln Elementary"
                }
            },
            IpAddress = "127.0.0.1"
        };

        _enrollmentCheckService
            .CheckEnrollmentAsync(Arg.Any<EnrollmentCheckRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Plugin error"));

        var result = await handler.Handle(command);

        Assert.False(result.IsSuccess);
        Assert.IsType<Portal.Kernel.Results.DependencyFailedResult<EnrollmentCheckResult>>(result);
    }

    [Fact]
    public async Task Handle_AlwaysReplacesConnectorNameAndDobWithSubmittedValues()
    {
        // The connector now returns CBMS values (not submitted ones). The handler must
        // replace FirstName/LastName/DateOfBirth with the submitted values before
        // returning to the API — regardless of flag state — so no state-system PII
        // is ever surfaced to the UI.
        _featureManager
            .IsEnabledAsync(FeatureFlags.EnrollmentCheckRequiresAtLeastOneExactMatchedField)
            .Returns(false);

        var handler = CreateHandler();
        var command = new CheckEnrollmentCommand
        {
            Children =
            [
                new() { FirstName = "Jane", LastName = "Doe", DateOfBirth = new DateOnly(2015, 3, 12) }
            ],
            IpAddress = "127.0.0.1"
        };

        // Connector returns CBMS-normalized values — different name, different month/day
        _enrollmentCheckService
            .CheckEnrollmentAsync(Arg.Any<EnrollmentCheckRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => new EnrollmentCheckResult
            {
                Results =
                [
                    new()
                    {
                        CheckId = call.Arg<EnrollmentCheckRequest>().Children[0].CheckId,
                        FirstName = "JANE",
                        LastName = "DOE",
                        DateOfBirth = new DateOnly(2015, 3, 12),
                        Status = EnrollmentStatus.Match
                    }
                ]
            });

        var result = await handler.Handle(command);

        Assert.True(result.IsSuccess);
        var returned = Assert.Single(result.Value.Results);
        Assert.Equal("Jane", returned.FirstName);
        Assert.Equal("Doe", returned.LastName);
        Assert.Equal(new DateOnly(2015, 3, 12), returned.DateOfBirth);
    }

    [Fact]
    public async Task Handle_WhenAllCandidatesDroppedByFilter_InsertsSyntheticNonMatch()
    {
        _featureManager
            .IsEnabledAsync(FeatureFlags.EnrollmentCheckRequiresAtLeastOneExactMatchedField)
            .Returns(true);

        var handler = CreateHandler();
        var command = new CheckEnrollmentCommand
        {
            Children =
            [
                new() { FirstName = "Jane", LastName = "Doe", DateOfBirth = new DateOnly(2015, 3, 12) }
            ],
            IpAddress = "127.0.0.1"
        };

        // Connector returns a candidate with a wrong year — filter drops it, leaving no result for this child
        _enrollmentCheckService
            .CheckEnrollmentAsync(Arg.Any<EnrollmentCheckRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => new EnrollmentCheckResult
            {
                Results =
                [
                    new()
                    {
                        CheckId = call.Arg<EnrollmentCheckRequest>().Children[0].CheckId,
                        FirstName = "Jane",
                        LastName = "Doe",
                        DateOfBirth = new DateOnly(2014, 3, 12),
                        Status = EnrollmentStatus.Match
                    }
                ]
            });

        var result = await handler.Handle(command);

        Assert.True(result.IsSuccess);
        var returned = Assert.Single(result.Value.Results);
        Assert.Equal(EnrollmentStatus.NonMatch, returned.Status);
        Assert.Equal("Jane", returned.FirstName);
        Assert.Equal("Doe", returned.LastName);
        Assert.Equal(new DateOnly(2015, 3, 12), returned.DateOfBirth);
    }

    [Fact]
    public async Task Handle_WhenExactMatchFlagEnabled_DropsResultWithNoExactMatch()
    {
        _featureManager
            .IsEnabledAsync(FeatureFlags.EnrollmentCheckRequiresAtLeastOneExactMatchedField)
            .Returns(true);

        var handler = CreateHandler();
        var command = new CheckEnrollmentCommand
        {
            Children =
            [
                new() { FirstName = "Jane", LastName = "Doe", DateOfBirth = new DateOnly(2015, 3, 12) }
            ],
            IpAddress = "127.0.0.1"
        };

        // Connector returns a candidate whose name and DOB don't match the submission
        _enrollmentCheckService
            .CheckEnrollmentAsync(Arg.Any<EnrollmentCheckRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => new EnrollmentCheckResult
            {
                Results =
                [
                    new()
                    {
                        CheckId = call.Arg<EnrollmentCheckRequest>().Children[0].CheckId,
                        FirstName = "Robert",
                        LastName = "Smith",
                        DateOfBirth = new DateOnly(2010, 6, 1),
                        Status = EnrollmentStatus.PossibleMatch
                    }
                ]
            });

        var result = await handler.Handle(command);

        Assert.True(result.IsSuccess);
        var returned = Assert.Single(result.Value.Results);
        Assert.Equal(EnrollmentStatus.NonMatch, returned.Status);
    }

    [Fact]
    public async Task Handle_WhenExactMatchFlagDisabled_KeepsResultRegardlessOfMatch()
    {
        _featureManager
            .IsEnabledAsync(FeatureFlags.EnrollmentCheckRequiresAtLeastOneExactMatchedField)
            .Returns(false);

        var handler = CreateHandler();
        var command = new CheckEnrollmentCommand
        {
            Children =
            [
                new() { FirstName = "Jane", LastName = "Doe", DateOfBirth = new DateOnly(2015, 3, 12) }
            ],
            IpAddress = "127.0.0.1"
        };

        // Same non-matching candidate as above — should be kept because flag is off
        _enrollmentCheckService
            .CheckEnrollmentAsync(Arg.Any<EnrollmentCheckRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => new EnrollmentCheckResult
            {
                Results =
                [
                    new()
                    {
                        CheckId = call.Arg<EnrollmentCheckRequest>().Children[0].CheckId,
                        FirstName = "Robert",
                        LastName = "Smith",
                        DateOfBirth = new DateOnly(2010, 6, 1),
                        Status = EnrollmentStatus.PossibleMatch
                    }
                ]
            });

        var result = await handler.Handle(command);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Results);
    }
}
