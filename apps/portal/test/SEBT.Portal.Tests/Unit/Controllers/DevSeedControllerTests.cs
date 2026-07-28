using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Api.Controllers.Diagnostics;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Tests.Unit.Controllers;

public class DevSeedControllerTests
{
    private readonly IDatabaseSeeder _databaseSeeder = Substitute.For<IDatabaseSeeder>();
    private readonly IConfiguration _configuration = new ConfigurationBuilder().Build();

    [Fact]
    public async Task ReseedScenario_WhenDevEndpointsDisabled_ReturnsNotFound()
    {
        var controller = CreateController(enableDevEndpoints: false);

        var result = await controller.ReseedScenario("verified", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        await _databaseSeeder.DidNotReceiveWithAnyArgs()
            .ReseedUserScenarioAsync(default!, default, default);
    }

    [Fact]
    public async Task ReseedScenario_WhenDevEndpointsEnabled_ReturnsNoContent()
    {
        var controller = CreateController(enableDevEndpoints: true);

        var result = await controller.ReseedScenario("verified", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        await _databaseSeeder.Received(1).ReseedUserScenarioAsync(
            "verified",
            false,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReseedScenario_WhenScenarioInvalid_ReturnsBadRequest()
    {
        _databaseSeeder
            .ReseedUserScenarioAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new ArgumentException("Unknown scenario")));

        var controller = CreateController(enableDevEndpoints: true);

        var result = await controller.ReseedScenario("not-a-scenario", CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    private DevSeedController CreateController(bool enableDevEndpoints)
    {
        var settings = Options.Create(new SeedingSettings { EnableDevEndpoints = enableDevEndpoints });
        return new DevSeedController(_databaseSeeder, settings, _configuration);
    }
}
