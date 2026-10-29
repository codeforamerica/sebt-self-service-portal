using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Infrastructure.Configuration;
using SEBT.Portal.StatesPlugins.Interfaces;

namespace SEBT.Portal.Infrastructure.Repositories;

internal static class Dependencies
{
    internal static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddTransient<IOtpRepository, InMemoryOtpRepository>();
        services.AddTransient<IUserRepository, DatabaseUserRepository>();
        services.AddTransient<IDocVerificationChallengeRepository, DatabaseDocVerificationChallengeRepository>();
        services.AddScoped<ICardReplacementRequestRepository, CardReplacementRequestRepository>();

        // For deterministic time in seeding/mock data
        services.AddSingleton(TimeProvider.System);

        services.AddTransient<IHouseholdRepository>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var environment = sp.GetRequiredService<IHostEnvironment>();
            var logger = sp.GetService<ILoggerFactory>()?.CreateLogger(nameof(HouseholdMockGate));
            var useMockHouseholdData = HouseholdMockGate.IsEnabled(config, environment, logger);

            if (useMockHouseholdData)
            {
                logger?.LogWarning(
                    "Household request served from mock data. Feature flag {FeatureFlag} is enabled.",
                    FeatureFlags.UseMockHouseholdData);
                return sp.GetRequiredService<MockHouseholdRepository>();
            }

            var summerEbtCaseService = sp.GetService<ISummerEbtCaseService>();
            if (summerEbtCaseService != null)
            {
                return sp.GetRequiredService<HouseholdRepository>();
            }

            throw new InvalidOperationException(
                "The use_mock_household_data feature flag is off but no household plugin (ISummerEbtCaseService) is loaded. " +
                "Either enable use_mock_household_data or ensure a state plugin is loaded (e.g. PluginAssemblyPaths and the plugin DLL).");
        });
        services.AddSingleton<MockHouseholdRepository>();
        services.AddTransient<HouseholdRepository>();

        return services;
    }
}
