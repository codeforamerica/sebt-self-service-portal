using System.Composition;
using System.Composition.Convention;
using System.Composition.Hosting;
using Microsoft.AspNetCore.Mvc.Controllers;
using SEBT.Portal.StatesPlugins.Interfaces;

namespace SEBT.Portal.Api.Composition;

internal static class ServiceCollectionPluginExtensions
{
    public static IServiceCollection AddPlugins(this IServiceCollection services)
    {
        services.AddSingleton<CompositionContext>(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var pluginAssemblyPaths = configuration
                .GetSection("PluginAssemblyPaths")
                .Get<string[]>()
                ?? throw new InvalidOperationException("PluginAssemblyPaths missing from configuration.");
            var containerConfiguration = CreateContainerConfiguration(pluginAssemblyPaths);
            var container = containerConfiguration.CreateContainer();
            return container;
        });

        var defaultControllerActivatorDescriptor = services
            .Single(sd => sd.ServiceType == typeof(IControllerActivator));
        var defaultControllerActivatorCtor = defaultControllerActivatorDescriptor.ImplementationType!
            .GetConstructors()
            .Single();
        services.Remove(defaultControllerActivatorDescriptor);

        return services.AddSingleton<IControllerActivator>(sp =>
        {
            var parameters = defaultControllerActivatorCtor.GetParameters()
                .Select(parameter => parameter.ParameterType)
                .Select(sp.GetRequiredService)
                .ToArray();
            var defaultControllerActivator = (IControllerActivator)defaultControllerActivatorCtor.Invoke(parameters);

            return new CompositionBridgingControllerActivator(defaultControllerActivator);
        });
    }

    private static ContainerConfiguration CreateContainerConfiguration(string[] assemblyPaths)
    {
        var conventions = new ConventionBuilder();

        conventions
            .ForTypesDerivedFrom<IStateMetadataService>()
            .Export<IStateMetadataService>()
            .Shared();

        conventions
            .ForTypesDerivedFrom<IStateAuthenticationService>()
            .Export<IStateAuthenticationService>()
            .Shared();

        conventions
            .ForTypesDerivedFrom<ISummerEbtCaseService>()
            .Export<ISummerEbtCaseService>()
            .Shared();

        return new ContainerConfiguration().WithAssembliesInPath(assemblyPaths, conventions);
    }
}
