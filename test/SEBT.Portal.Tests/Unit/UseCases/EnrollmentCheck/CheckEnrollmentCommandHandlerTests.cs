using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
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

    private CheckEnrollmentCommandHandler CreateHandler() =>
        new(_enrollmentCheckService, _submissionLogger, _logger);

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
        Assert.IsType<Kernel.Results.ValidationFailedResult<EnrollmentCheckResult>>(result);
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
        Assert.IsType<Kernel.Results.ValidationFailedResult<EnrollmentCheckResult>>(result);
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
            Arg.Is<Core.Models.EnrollmentCheck.EnrollmentCheckSubmission>(s =>
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
        Assert.IsType<Kernel.Results.DependencyFailedResult<EnrollmentCheckResult>>(result);
    }
}
