using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.EnrollmentCheck;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.UseCases.EnrollmentCheck;

namespace SEBT.Portal.Tests.Unit.UseCases.EnrollmentCheck;

public class CheckEnrollmentCommandHandlerTests
{
    private readonly IEnrollmentCheckBackend _enrollmentCheckBackend = Substitute.For<IEnrollmentCheckBackend>();
    private readonly IEnrollmentCheckSubmissionLogger _submissionLogger = Substitute.For<IEnrollmentCheckSubmissionLogger>();
    private readonly ILogger<CheckEnrollmentCommandHandler> _logger = Substitute.For<ILogger<CheckEnrollmentCommandHandler>>();
    private readonly IFeatureManager _featureManager = Substitute.For<IFeatureManager>();

    private CheckEnrollmentCommandHandler CreateHandler() =>
        new(_enrollmentCheckBackend, _submissionLogger, _logger, _featureManager);

    private static CheckEnrollmentCommand CreateCommand(params CheckEnrollmentCommand.ChildInput[] children) =>
        new()
        {
            Children = children.ToList(),
            IpAddress = "127.0.0.1"
        };

    private static CheckEnrollmentCommand.ChildInput JaneDoe(
        string? schoolName = null, string? schoolCode = null) =>
        new()
        {
            FirstName = "Jane",
            LastName = "Doe",
            DateOfBirth = new DateOnly(2015, 3, 12),
            SchoolName = schoolName,
            SchoolCode = schoolCode
        };

    /// <summary>
    /// Stubs the backend to return one lean result per submitted child, echoing the
    /// handler-minted CheckIds so results correlate back to the submission.
    /// </summary>
    private void StubBackend(
        string? message,
        params Func<string, EnrollmentChildResult>?[] resultBuilders) =>
        _enrollmentCheckBackend
            .CheckEnrollmentAsync(Arg.Any<EnrollmentCheckRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<EnrollmentCheckRequest>();
                var results = new List<EnrollmentChildResult>();
                for (var i = 0; i < request.Children.Count; i++)
                {
                    if (resultBuilders[i] is { } build)
                    {
                        results.Add(build(request.Children[i].CheckId));
                    }
                }

                return new EnrollmentCheckResult(results, message);
            });

    [Fact]
    public async Task Handle_WhenNoChildren_ReturnsValidationFailed()
    {
        var handler = CreateHandler();

        var result = await handler.Handle(CreateCommand());

        Assert.False(result.IsSuccess);
        Assert.IsType<Portal.Kernel.Results.ValidationFailedResult<EnrollmentCheckOutcome>>(result);
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
        }).ToArray();

        var result = await handler.Handle(CreateCommand(children));

        Assert.False(result.IsSuccess);
        Assert.IsType<Portal.Kernel.Results.ValidationFailedResult<EnrollmentCheckOutcome>>(result);
    }

    [Fact]
    public async Task Handle_WithValidChild_CallsBackendAndReturnsResults()
    {
        StubBackend(null, checkId => new EnrollmentChildResult(checkId, IsMatch: true));
        var handler = CreateHandler();

        var result = await handler.Handle(CreateCommand(JaneDoe()));

        Assert.True(result.IsSuccess);
        var returned = Assert.Single(result.Value.Results);
        Assert.True(returned.IsMatch);
    }

    [Fact]
    public async Task Handle_IdentityAlwaysComesFromTheSubmission()
    {
        // The lean backend result carries no identity at all — the outcome's name and DOB
        // can only come from the submitted command, so no state-system PII can surface.
        StubBackend(null, checkId => new EnrollmentChildResult(
            checkId, IsMatch: true, MatchConfidence: 97.5, StatusMessage: "SEBT ELIGIBLE"));
        var handler = CreateHandler();

        var result = await handler.Handle(CreateCommand(JaneDoe()));

        Assert.True(result.IsSuccess);
        var returned = Assert.Single(result.Value.Results);
        Assert.Equal("Jane", returned.FirstName);
        Assert.Equal("Doe", returned.LastName);
        Assert.Equal(new DateOnly(2015, 3, 12), returned.DateOfBirth);
        Assert.Equal(97.5, returned.MatchConfidence);
        Assert.Equal("SEBT ELIGIBLE", returned.StatusMessage);
    }

    [Fact]
    public async Task Handle_SendsCoalescedSchoolIdentifier_SchoolCodeWins()
    {
        EnrollmentCheckRequest? seenRequest = null;
        _enrollmentCheckBackend
            .CheckEnrollmentAsync(
                Arg.Do<EnrollmentCheckRequest>(r => seenRequest = r), Arg.Any<CancellationToken>())
            .Returns(new EnrollmentCheckResult([]));
        var handler = CreateHandler();

        await handler.Handle(CreateCommand(JaneDoe(schoolName: "Lincoln Elementary", schoolCode: "SCH-042")));

        var child = Assert.Single(Assert.IsAssignableFrom<EnrollmentCheckRequest>(seenRequest).Children);
        Assert.Equal("SCH-042", child.SchoolIdentifier);
    }

    [Fact]
    public async Task Handle_LogsDeidentifiedSubmission()
    {
        StubBackend(null, checkId => new EnrollmentChildResult(checkId, IsMatch: true));
        var handler = CreateHandler();

        await handler.Handle(CreateCommand(JaneDoe(schoolName: "Lincoln Elementary")));

        await _submissionLogger.Received(1).LogSubmissionAsync(
            Arg.Is<EnrollmentCheckSubmission>(s =>
                s.ChildrenChecked == 1 &&
                s.ChildResults[0].BirthYear == 2015 &&
                s.ChildResults[0].Status == "Match" &&
                s.ChildResults[0].EligibilityType == null &&
                s.ChildResults[0].SchoolName == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenBackendThrows_ReturnsDependencyFailed()
    {
        _enrollmentCheckBackend
            .CheckEnrollmentAsync(Arg.Any<EnrollmentCheckRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Backend error"));
        var handler = CreateHandler();

        var result = await handler.Handle(CreateCommand(JaneDoe()));

        Assert.False(result.IsSuccess);
        Assert.IsType<Portal.Kernel.Results.DependencyFailedResult<EnrollmentCheckOutcome>>(result);
    }

    [Fact]
    public async Task Handle_FlagOn_BackendOmitsChild_InsertsSyntheticNonMatch()
    {
        // The backend returned no result for the child (e.g. the exact-match guard dropped
        // it). With the flag on, the outcome still carries one entry per submitted child.
        _featureManager
            .IsEnabledAsync(FeatureFlags.EnrollmentCheckRequiresAtLeastOneExactMatchedField)
            .Returns(true);
        StubBackend(null, new Func<string, EnrollmentChildResult>?[] { null });
        var handler = CreateHandler();

        var result = await handler.Handle(CreateCommand(JaneDoe()));

        Assert.True(result.IsSuccess);
        var returned = Assert.Single(result.Value.Results);
        Assert.False(returned.IsMatch);
        Assert.Equal("Jane", returned.FirstName);
        Assert.Equal("Doe", returned.LastName);
        Assert.Equal(new DateOnly(2015, 3, 12), returned.DateOfBirth);
        Assert.Null(returned.MatchConfidence);
        Assert.Null(returned.StatusMessage);
    }

    [Fact]
    public async Task Handle_FlagOff_BackendOmitsChild_NoSyntheticInserted()
    {
        _featureManager
            .IsEnabledAsync(FeatureFlags.EnrollmentCheckRequiresAtLeastOneExactMatchedField)
            .Returns(false);
        StubBackend("Processed", new Func<string, EnrollmentChildResult>?[] { null });
        var handler = CreateHandler();

        var result = await handler.Handle(CreateCommand(JaneDoe()));

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Results);
        Assert.Equal("Processed", result.Value.Message);
    }

    [Fact]
    public async Task Handle_BackendResultWithUnknownCheckId_IsDropped()
    {
        // A result that correlates to no submitted child has no identity to surface.
        StubBackend(null, _ => new EnrollmentChildResult(Guid.NewGuid().ToString(), IsMatch: true));
        var handler = CreateHandler();

        var result = await handler.Handle(CreateCommand(JaneDoe()));

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Results);
    }
}
