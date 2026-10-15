using Microsoft.FeatureManagement;
using NSubstitute;
using SEBT.Portal.Api.Composition;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.StatesPlugins.Interfaces;
using PluginEnrollmentCheckRequest = SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck.EnrollmentCheckRequest;
using PluginEnrollmentCheckResult = SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck.EnrollmentCheckResult;
using PluginChildCheckRequest = SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck.ChildCheckRequest;
using PluginEnrollmentStatus = SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck.EnrollmentStatus;
using CoreEnrollmentCheckRequest = SEBT.Portal.Core.StateBackends.EnrollmentCheckRequest;
using CoreEnrollmentCheckResult = SEBT.Portal.Core.StateBackends.EnrollmentCheckResult;
using CoreEnrollmentChildResult = SEBT.Portal.Core.StateBackends.EnrollmentChildResult;

namespace SEBT.Portal.Tests.Unit.Api.Composition;

public class FeatureGatedEnrollmentCheckServiceTests
{
    private readonly IFeatureManager _features = Substitute.For<IFeatureManager>();
    private readonly IEnrollmentCheckService _plugin = Substitute.For<IEnrollmentCheckService>();
    private readonly IEnrollmentCheckBackend _adapter = Substitute.For<IEnrollmentCheckBackend>();

    [Fact]
    public async Task CheckEnrollmentAsync_WhenFlagOn_MapsMatchVerdictAndIdentity()
    {
        var checkId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        _features.IsEnabledAsync(FeatureFlags.UseConfigurableStateBackend).Returns(true);
        _adapter.CheckEnrollmentAsync(Arg.Any<CoreEnrollmentCheckRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CoreEnrollmentCheckResult(
                [new CoreEnrollmentChildResult(checkId.ToString(), IsMatch: true, MatchConfidence: 0.9, StatusMessage: "ok")],
                "batch-ok"));
        var sut = new FeatureGatedEnrollmentCheckService(_features, _plugin, _adapter);
        var request = new PluginEnrollmentCheckRequest
        {
            Children =
            [
                new PluginChildCheckRequest
                {
                    CheckId = checkId,
                    FirstName = "Ada",
                    LastName = "Lovelace",
                    DateOfBirth = new DateOnly(1815, 12, 10),
                    SchoolName = "STEM",
                },
            ],
        };

        PluginEnrollmentCheckResult result = await sut.CheckEnrollmentAsync(request);

        Assert.Equal("batch-ok", result.ResponseMessage);
        var child = Assert.Single(result.Results);
        Assert.Equal(checkId, child.CheckId);
        Assert.Equal("Ada", child.FirstName);
        Assert.Equal(PluginEnrollmentStatus.Match, child.Status);
        Assert.Equal(0.9, child.MatchConfidence);
        await _plugin.DidNotReceive().CheckEnrollmentAsync(
            Arg.Any<PluginEnrollmentCheckRequest>(), Arg.Any<CancellationToken>());
    }
}
