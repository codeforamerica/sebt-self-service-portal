using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel.Services;
using SEBT.Portal.Infrastructure.Configuration;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Repositories;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Infrastructure;

public static class Dependencies
{
    public static IServiceCollection AddPortalInfrastructureServices(this IServiceCollection services)
    {
        // Otp Services
        services.AddTransient<IOtpSenderService, EmailOtpSenderService>();
        services.AddTransient<IOtpGeneratorService, OtpGeneratorService>();
        services.AddTransient<ISmtpClientService, MailKitClientService>();

        // JWT Services
        services.AddTransient<IJwtTokenService, JwtTokenService>();

        // ID Proofing Requirements (state-specific PII visibility)
        services.AddSingleton<IIdProofingRequirementsService, IdProofingRequirementsService>();

        // Feature Flag Services
        services.AddScoped<IFeatureFlagQueryService, Services.FeatureFlagQueryService>();

        return services;
    }

    public static IServiceCollection AddPortalInfrastructureRepositories(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        services.AddTransient<IOtpRepository, InMemoryOtpRepository>();
        services.AddTransient<IUserRepository, DatabaseUserRepository>();

        // For deterministic time in seeding/mock data
        services.AddSingleton(TimeProvider.System);

        // Household data is stored in-memory (will be replaced with distributed store later)
        services.AddTransient<IHouseholdRepository, MockHouseholdRepository>();

        services.AddMemoryCache();

        return services;
    }

    /// <summary>
    /// Adds the database context for the portal application.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <param name="configureOptions">Optional action to configure DbContext options (e.g., for seeding).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPortalDbContext(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<DbContextOptionsBuilder>? configureOptions = null)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<PortalDbContext>(options =>
        {
            options.UseSqlServer(connectionString);
            configureOptions?.Invoke(options);
        });

        services.AddScoped<IDatabaseMigrator, DatabaseMigrator>();
        services.AddScoped<IDataSeeder, DataSeeder>();

        return services;
    }

    public static IServiceCollection AddPortalInfrastructureAppSettings(this IServiceCollection services, IConfiguration configuration)
    {

        services.AddOptionsWithValidateOnStart<EmailOtpSenderServiceSettings>()
            .BindConfiguration(EmailOtpSenderServiceSettings.SectionName);
        services.AddOptionsWithValidateOnStart<SmtpClientSettings>()
            .BindConfiguration(SmtpClientSettings.SectionName);
        services.AddOptionsWithValidateOnStart<OtpRateLimitSettings>()
            .BindConfiguration(OtpRateLimitSettings.SectionName);
        services.AddOptionsWithValidateOnStart<JwtSettings>()
            .BindConfiguration(JwtSettings.SectionName);
        services.AddSingleton<IValidateOptions<IdProofingRequirementsSettings>, IdProofingRequirementsSettingsValidator>();
        services.AddOptionsWithValidateOnStart<IdProofingRequirementsSettings>()
            .BindConfiguration(IdProofingRequirementsSettings.SectionName);

        services.AddOptions<FeatureManagementSettings>()
            .Bind(configuration.GetSection(FeatureManagementSettings.SectionName))
            .PostConfigure<IConfiguration>((options, config) =>
            {
                var postConfig = new FeatureManagementOptionsConfiguration(config);
                postConfig.PostConfigure(null, options);
            });

        services.AddOptions<AppConfigFeatureFlagSettings>()
            .Bind(configuration.GetSection(AppConfigFeatureFlagSettings.SectionName))
            .PostConfigure<IConfiguration, ILogger<AppConfigFeatureFlagOptionsConfiguration>>((options, config, logger) =>
            {
                var postConfig = new AppConfigFeatureFlagOptionsConfiguration(config, logger);
                postConfig.PostConfigure(null, options);
            });

        return services;
    }
}
