using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Infrastructure.StateBackendAdapters;
using SEBT.Portal.StatesPlugins.Interfaces;
using PluginChildCheckResult = SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck.ChildCheckResult;
using PluginEnrollmentCheckRequest = SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck.EnrollmentCheckRequest;
using PluginEnrollmentCheckResult = SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck.EnrollmentCheckResult;
using PluginEnrollmentStatus = SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck.EnrollmentStatus;

namespace SEBT.Portal.Tests.Unit.Infrastructure.StateBackendAdapters;

/// <summary>
/// Pins the adapter boundary: the contract request is a faithful build of the Core batch
/// (school identifier fanned out to BOTH contract school fields, AdditionalFields left
/// empty), the flag-gated exact-match guard drops fuzzy candidates before mapping, and
/// the rich contract results map down to lean verdicts — Match becomes IsMatch, everything
/// else does not, with confidence and status text riding along.
/// </summary>
public class PluginEnrollmentCheckBackendTests
{
    private static readonly Guid CheckId = Guid.NewGuid();

    private readonly IEnrollmentCheckService _contractService =
        Substitute.For<IEnrollmentCheckService>();

    private PluginEnrollmentCheckBackend CreateBackend(bool exactMatchFlag = false)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"FeatureManagement:{FeatureFlags.EnrollmentCheckRequiresAtLeastOneExactMatchedField}"] =
                    exactMatchFlag.ToString()
            })
            .Build();

        return new PluginEnrollmentCheckBackend(
            _contractService, configuration, NullLogger<PluginEnrollmentCheckBackend>.Instance);
    }

    private static EnrollmentCheckRequest CreateRequest(string? schoolIdentifier = null) =>
        new([
            new EnrollmentChild(
                CheckId.ToString(), "Jane", "Doe", new DateOnly(2015, 3, 12), schoolIdentifier)
        ]);

    private void StubConnector(params PluginChildCheckResult[] results) =>
        StubConnector(responseMessage: null, results);

    private void StubConnector(string? responseMessage, params PluginChildCheckResult[] results) =>
        _contractService
            .CheckEnrollmentAsync(Arg.Any<PluginEnrollmentCheckRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PluginEnrollmentCheckResult
            {
                Results = results.ToList(),
                ResponseMessage = responseMessage
            });

    private static PluginChildCheckResult ConnectorResult(
        PluginEnrollmentStatus status,
        string firstName = "Jane",
        string lastName = "Doe",
        DateOnly? dateOfBirth = null,
        double? matchConfidence = null,
        string? statusMessage = null) =>
        new()
        {
            CheckId = CheckId,
            FirstName = firstName,
            LastName = lastName,
            DateOfBirth = dateOfBirth ?? new DateOnly(2015, 3, 12),
            Status = status,
            MatchConfidence = matchConfidence,
            StatusMessage = statusMessage
        };

    [Fact]
    public async Task CheckEnrollmentAsync_BuildsFaithfulContractRequest_WithSchoolFanOut()
    {
        PluginEnrollmentCheckRequest? seenRequest = null;
        _contractService
            .CheckEnrollmentAsync(
                Arg.Do<PluginEnrollmentCheckRequest>(r => seenRequest = r), Arg.Any<CancellationToken>())
            .Returns(new PluginEnrollmentCheckResult { Results = [] });
        var backend = CreateBackend();

        await backend.CheckEnrollmentAsync(CreateRequest(schoolIdentifier: "SCH-042"));

        var child = Assert.Single(Assert.IsAssignableFrom<PluginEnrollmentCheckRequest>(seenRequest).Children);
        Assert.Equal(CheckId, child.CheckId);
        Assert.Equal("Jane", child.FirstName);
        Assert.Equal("Doe", child.LastName);
        Assert.Equal(new DateOnly(2015, 3, 12), child.DateOfBirth);
        // The single school identifier reaches BOTH contract fields: DC reads SchoolName,
        // CO reads SchoolCode.
        Assert.Equal("SCH-042", child.SchoolName);
        Assert.Equal("SCH-042", child.SchoolCode);
        // No connector reads AdditionalFields; the adapter leaves it empty.
        Assert.Empty(child.AdditionalFields);
    }

    [Fact]
    public async Task CheckEnrollmentAsync_MapsMatchToIsMatch_WithConfidenceAndStatusTextThrough()
    {
        StubConnector(
            "Success",
            ConnectorResult(PluginEnrollmentStatus.Match, matchConfidence: 97.5, statusMessage: "SEBT ELIGIBLE"));
        var backend = CreateBackend();

        var result = await backend.CheckEnrollmentAsync(CreateRequest());

        var child = Assert.Single(result.Results);
        Assert.Equal(CheckId.ToString(), child.CheckId);
        Assert.True(child.IsMatch);
        Assert.Equal(97.5, child.MatchConfidence);
        Assert.Equal("SEBT ELIGIBLE", child.StatusMessage);
        Assert.Equal("Success", result.Message);
    }

    [Theory]
    [InlineData(PluginEnrollmentStatus.PossibleMatch)]
    [InlineData(PluginEnrollmentStatus.NonMatch)]
    [InlineData(PluginEnrollmentStatus.Error)]
    public async Task CheckEnrollmentAsync_MapsEveryNonMatchStatusToIsMatchFalse(
        PluginEnrollmentStatus status)
    {
        StubConnector(ConnectorResult(status, statusMessage: "STATUS TEXT"));
        var backend = CreateBackend();

        var result = await backend.CheckEnrollmentAsync(CreateRequest());

        var child = Assert.Single(result.Results);
        Assert.False(child.IsMatch);
        // The status text still rides along on non-matches (e.g. CBMS eligibility text).
        Assert.Equal("STATUS TEXT", child.StatusMessage);
    }

    [Fact]
    public async Task CheckEnrollmentAsync_FlagOn_GuardDropsFuzzyCandidate()
    {
        // The connector fuzzy-matched a candidate whose identity exact-matches on NO field:
        // the guard drops it entirely — its confidence and status text vanish with it.
        StubConnector(ConnectorResult(
            PluginEnrollmentStatus.Match,
            firstName: "Roberta", lastName: "Smith", dateOfBirth: new DateOnly(2015, 6, 1),
            matchConfidence: 88.0, statusMessage: "POSSIBLE MATCH"));
        var backend = CreateBackend(exactMatchFlag: true);

        var result = await backend.CheckEnrollmentAsync(CreateRequest());

        Assert.Empty(result.Results);
    }

    [Fact]
    public async Task CheckEnrollmentAsync_FlagOff_FuzzyCandidatePassesThrough()
    {
        StubConnector(ConnectorResult(
            PluginEnrollmentStatus.Match,
            firstName: "Roberta", lastName: "Smith", dateOfBirth: new DateOnly(2015, 6, 1),
            matchConfidence: 88.0, statusMessage: "POSSIBLE MATCH"));
        var backend = CreateBackend(exactMatchFlag: false);

        var result = await backend.CheckEnrollmentAsync(CreateRequest());

        var child = Assert.Single(result.Results);
        Assert.True(child.IsMatch);
        Assert.Equal(88.0, child.MatchConfidence);
    }

    [Fact]
    public async Task CheckEnrollmentAsync_FlagOn_ExactDobMatchSurvivesTheGuard()
    {
        StubConnector(ConnectorResult(
            PluginEnrollmentStatus.Match,
            firstName: "JANE", lastName: "DOE", matchConfidence: 97.5));
        var backend = CreateBackend(exactMatchFlag: true);

        var result = await backend.CheckEnrollmentAsync(CreateRequest());

        var child = Assert.Single(result.Results);
        Assert.True(child.IsMatch);
    }

    [Fact]
    public async Task CheckEnrollmentAsync_NullRequest_Throws()
    {
        var backend = CreateBackend();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => backend.CheckEnrollmentAsync(null!));
    }
}
