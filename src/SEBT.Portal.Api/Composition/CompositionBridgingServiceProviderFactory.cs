using System.Composition;
using System.Diagnostics;

namespace SEBT.Portal.Api.Composition;

internal class CompositionBridgingServiceProviderFactory : IServiceProviderFactory<IServiceCollection>
{
    public IServiceCollection CreateBuilder(IServiceCollection services) => services;

    public IServiceProvider CreateServiceProvider(IServiceCollection containerBuilder)
    {
        var sp = containerBuilder.BuildServiceProvider();
        return new CompositionBridgingServiceProvider(sp);
    }

    private class CompositionBridgingServiceProvider(IServiceProvider wrapped) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            var compositionContext = wrapped.GetRequiredService<CompositionContext>();

            if (compositionContext.TryGetExport(serviceType, out var service))
            {
                return service;
            }

            service = wrapped.GetService(serviceType);

            if (service is not null)
            {
                compositionContext.SatisfyImports(service);
            }

            return service;
        }
    }
}
