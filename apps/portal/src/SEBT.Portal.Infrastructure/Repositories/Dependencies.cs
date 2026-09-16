using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SEBT.Portal.Core.Repositories;
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
            var useMockHouseholdData = config.GetValue<bool>("UseMockHouseholdData", false);

            if (useMockHouseholdData)
            {
                return sp.GetRequiredService<MockHouseholdRepository>();
            }

            var summerEbtCaseService = sp.GetService<ISummerEbtCaseService>();
            if (summerEbtCaseService != null)
            {
                return sp.GetRequiredService<HouseholdRepository>();
            }

            throw new InvalidOperationException(
                "UseMockHouseholdData is false but no household plugin (ISummerEbtCaseService) is loaded. " +
                "Either set UseMockHouseholdData to true in configuration or ensure a state plugin is loaded (e.g. PluginAssemblyPaths and the plugin DLL).");
        });
        services.AddSingleton<MockHouseholdRepository>();
        services.AddTransient<HouseholdRepository>();

        return services;
    }
}
