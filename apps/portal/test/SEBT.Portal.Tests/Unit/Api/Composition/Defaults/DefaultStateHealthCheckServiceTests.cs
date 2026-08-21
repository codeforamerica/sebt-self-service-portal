using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SEBT.Portal.Api.Composition.Defaults;

namespace SEBT.Portal.Tests.Unit.Api.Composition.Defaults;

public class DefaultStateHealthCheckServiceTests
{
    [Fact]
    public void ConfigureHealthChecks_RegistersNothing()
    {
        var service = new DefaultStateHealthCheckService();
        var builder = Substitute.For<IHealthChecksBuilder>();

        service.ConfigureHealthChecks(builder);

        Assert.Empty(builder.ReceivedCalls());
    }
}
