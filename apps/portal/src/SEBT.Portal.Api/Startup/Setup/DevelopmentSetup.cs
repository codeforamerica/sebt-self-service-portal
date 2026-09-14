using Microsoft.Extensions.Options;
using SEBT.Portal.Api.Options;
using SEBT.Portal.Api.Services;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Seeding.Services;

namespace SEBT.Portal.Api.Startup.Setup;

internal static class DevelopmentSetup
{
    /// <summary>
    /// Development-only phone override: when set, overrides JWT phone for household lookup
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> instance for dependency injection</param>
    /// <returns>The <see cref="IServiceCollection"/>instance</returns>
    public static IServiceCollection AddDevelopmentOverrides(this IServiceCollection services)
    {
        services.AddOptions<DevelopmentPhoneOverrideOptions>()
            .BindConfiguration(DevelopmentPhoneOverrideOptions.SectionName);
        services.AddSingleton<IPhoneOverrideProvider>(sp =>
        {
            var env = sp.GetRequiredService<IWebHostEnvironment>();
            var options = sp.GetRequiredService<IOptions<DevelopmentPhoneOverrideOptions>>().Value;
            if (env.IsDevelopment() && !string.IsNullOrWhiteSpace(options.Phone))
            {
                return sp.GetRequiredService<DevelopmentPhoneOverrideProvider>();
            }
            return NullPhoneOverrideProvider.Instance;
        });
        services.AddSingleton<DevelopmentPhoneOverrideProvider>();

        return services;
    }

    /// <summary>
    /// Register IDatabaseSeeder for development utilities (e.g., ClearSeededData script)
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> instance for dependency injection</param>
    /// <returns>The <see cref="IServiceCollection"/>instance</returns>
    public static IServiceCollection AddDatabaseSeeder(this IServiceCollection services)
    {
        services.AddScoped<IDatabaseSeeder>(sp =>
        {
            var dataSeeder = sp.GetRequiredService<IDataSeeder>();
            var logger = sp.GetService<ILogger<DatabaseSeeder>>();
            var timeProvider = sp.GetRequiredService<TimeProvider>();
            var seedingSettings = sp.GetService<IOptions<SeedingSettings>>()?.Value ?? new SeedingSettings();
            return new DatabaseSeeder(dataSeeder, seedingSettings, logger, timeProvider);
        });

        return services;
    }
}
