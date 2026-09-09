using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace SEBT.Portal.Tests.Integration.Extensions;

public static class IntegrationTestServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Replaces an existing registration with a no-op NSubstitute mock.</summary>
        public void ReplaceWithMock<TService>() where TService : class
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(TService));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddScoped(_ => Substitute.For<TService>());
        }
    }
}
