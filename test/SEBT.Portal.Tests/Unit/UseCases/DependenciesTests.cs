using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.UseCases;
using SEBT.Portal.UseCases.Household;

namespace SEBT.Portal.Tests.Unit.UseCases;

/// <summary>
/// Verifies that AddUseCases() registers all command and query handlers in the DI container.
/// </summary>
public class DependenciesTests
{
    private static ServiceProvider BuildProviderWithUseCases()
    {
        var services = new ServiceCollection();
        services.AddUseCases();

        // Stub infrastructure dependencies that handlers need at construction time
        services.AddSingleton(Substitute.For<IHouseholdIdentifierResolver>());
        services.AddSingleton(Substitute.For<IHouseholdRepository>());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddUseCases_RegistersRequestCardReplacementCommandHandler()
    {
        using var provider = BuildProviderWithUseCases();

        var handler = provider.GetService<ICommandHandler<RequestCardReplacementCommand>>();

        Assert.NotNull(handler);
        Assert.IsType<RequestCardReplacementCommandHandler>(handler);
    }
}
