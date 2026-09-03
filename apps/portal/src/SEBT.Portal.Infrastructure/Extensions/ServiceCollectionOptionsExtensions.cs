using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Infrastructure.Extensions;

internal static class ServiceCollectionOptionsExtensions
{
    // At class load time, scans *only* the current assembly for implementations
    // of IValidateOptions<> and builds a lookup keyed by settings type.
    // Used in the below extensions for service collection (DI) registration.
    private static readonly ILookup<Type, Type> ValidatorsByOptionsType =
        typeof(Dependencies).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidateOptions<>))
                .Select(i => (OptionsType: i.GetGenericArguments()[0], ValidatorType: t)))
            .ToLookup(x => x.OptionsType, x => x.ValidatorType);

    extension(IServiceCollection services)
    {
        public OptionsBuilder<TOptions> AddPortalOptions<TOptions>()
            where TOptions : class, IHaveConfigSectionName
        {
            foreach (var validatorType in ValidatorsByOptionsType[typeof(TOptions)])
            {
                services.AddSingleton(typeof(IValidateOptions<TOptions>), validatorType);
            }

            return services.AddOptionsWithValidateOnStart<TOptions>()
                .BindConfiguration(TOptions.SectionName)
                .ValidateDataAnnotations();
        }

        public OptionsBuilder<TOptions> AddPortalOptions<TOptions, TConfigureOptions>(
            IConfiguration configuration)
            where TOptions : class, IHaveConfigSectionName
            where TConfigureOptions : class, IConfigureOptions<TOptions>
        {
            foreach (var validatorType in ValidatorsByOptionsType[typeof(TOptions)])
            {
                services.AddSingleton(typeof(IValidateOptions<TOptions>), validatorType);
            }

            services.ConfigureOptions<TConfigureOptions>();

            // ConfigurationChangeTokenSource required for updates when using IConfigureOptions<T>
            services.AddSingleton<IOptionsChangeTokenSource<TOptions>>(
                new ConfigurationChangeTokenSource<TOptions>(
                    configuration.GetSection(TOptions.SectionName)));

            return services.AddOptionsWithValidateOnStart<TOptions>()
                .ValidateDataAnnotations();
        }
    }
}
