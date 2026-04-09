using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel.Services;
using SEBT.Portal.Infrastructure.Configuration;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Repositories;
using SEBT.Portal.Infrastructure.Services;
using ISummerEbtCaseService = SEBT.Portal.StatesPlugins.Interfaces.ISummerEbtCaseService;

namespace SEBT.Portal.Infrastructure;

public static class Dependencies
{
    public static IServiceCollection AddPortalInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Otp Services
        services.AddTransient<IOtpSenderService, EmailOtpSenderService>();
        services.AddTransient<IOtpGeneratorService, OtpGeneratorService>();
        services.AddTransient<ISmtpClientService, SmtpClientService>();

        // JWT Services
        services.AddTransient<IJwtTokenService, JwtTokenService>();

        // ID Proofing Requirements (state-specific PII visibility)
        services.AddSingleton<IIdProofingRequirementsService, IdProofingRequirementsService>();

        // Enrollment Check logging
        services.AddScoped<IEnrollmentCheckSubmissionLogger, EnrollmentCheckSubmissionLogger>();

        // Feature Flag Services
        services.AddScoped<IFeatureFlagQueryService, Services.FeatureFlagQueryService>();

        // Household identifier resolution (state-configurable preferred household ID type)
        services.AddTransient<IHouseholdIdentifierResolver, HouseholdIdentifierResolver>();

        // Smarty address verification (or pass-through when disabled)
        services.AddHttpClient("Smarty", (sp, client) =>
        {
            var smarty = sp.GetRequiredService<IOptions<SmartySettings>>().Value;
            var baseUrl = string.IsNullOrWhiteSpace(smarty.BaseUrl)
                ? "https://us-street.api.smartystreets.com"
                : smarty.BaseUrl.TrimEnd('/');
            client.BaseAddress = new Uri(baseUrl + "/");
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(smarty.TimeoutSeconds, 1, 120));
        });

        services.AddTransient<SmartyAddressUpdateService>();
        services.AddTransient<PassThroughAddressUpdateService>();
        services.AddTransient<IAddressUpdateService>(sp =>
        {
            var smarty = sp.GetRequiredService<IOptions<SmartySettings>>().Value;
            return smarty.Enabled
                ? sp.GetRequiredService<SmartyAddressUpdateService>()
                : sp.GetRequiredService<PassThroughAddressUpdateService>();
        });

        // Address validation — checks blocked addresses and street abbreviations per state config
        services.AddSingleton<IAddressValidationService, AddressValidationService>();
        services.AddSingleton<IIdentifierHasher, IdentifierHasher>();

        // Expose SocureSettings directly for use case injection (avoids IOptions dependency in UseCases layer)
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<SocureSettings>>().Value);

        // Socure client — disabled, stub, or real based on configuration
        var socureEnabled = configuration.GetValue<bool>("Socure:Enabled");
        if (socureEnabled)
        {
            services.AddTransient<StubSocureClient>();
            services.AddTransient<HttpSocureClient>();
            services.AddTransient<ISocureClient>(sp =>
            {
                var settings = sp.GetRequiredService<IOptions<SocureSettings>>().Value;
                if (settings.UseStub)
                    return sp.GetRequiredService<StubSocureClient>();

                return sp.GetRequiredService<HttpSocureClient>();
            });
        }
        else
        {
            services.AddTransient<ISocureClient, DisabledSocureClient>();
        }

        return services;
    }

    public static IServiceCollection AddPortalInfrastructureRepositories(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        services.AddTransient<IOtpRepository, InMemoryOtpRepository>();
        services.AddTransient<IUserRepository, DatabaseUserRepository>();
        services.AddTransient<IDocVerificationChallengeRepository, DatabaseDocVerificationChallengeRepository>();

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

    /// <summary>
    /// Registers caching services. When a Redis connection string is configured,
    /// uses Redis as the distributed cache (L2) backing HybridCache.
    /// Otherwise, falls back to in-memory caching only.
    /// Call this before AddPlugins — plugins may depend on HybridCache.
    /// </summary>
    public static IServiceCollection AddCaching(this IServiceCollection services, IConfiguration? configuration)
    {
        var redisConnectionString = configuration?.GetConnectionString("Redis");

        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
            });
        }

        // HybridCache provides an L1 in-memory cache with optional L2 distributed backing.
        // When Redis is registered above, HybridCache automatically uses it as L2.
        // When Redis is not configured, HybridCache operates as in-memory only.
        services.AddHybridCache();
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
        services.AddOptions<StateHouseholdIdSettings>()
            .BindConfiguration(StateHouseholdIdSettings.SectionName);
        services.AddOptionsWithValidateOnStart<IdentifierHasherSettings>()
            .BindConfiguration(IdentifierHasherSettings.SectionName);
        services.AddSingleton<IValidateOptions<IdProofingRequirementsSettings>, IdProofingRequirementsSettingsValidator>();
        services.AddOptionsWithValidateOnStart<IdProofingRequirementsSettings>()
            .BindConfiguration(IdProofingRequirementsSettings.SectionName);

        services.AddSingleton<IValidateOptions<OidcStepUpSettings>, OidcStepUpSettingsValidator>();
        services.AddOptionsWithValidateOnStart<OidcStepUpSettings>()
            .BindConfiguration(OidcStepUpSettings.SectionName);

        services.AddOptions<FeatureManagementSettings>()
            .Bind(configuration.GetSection(FeatureManagementSettings.SectionName))
            .PostConfigure<IConfiguration>((options, config) =>
            {
                var postConfig = new FeatureManagementOptionsConfiguration(config);
                postConfig.PostConfigure(null, options);
            });

        services.AddOptionsWithValidateOnStart<EnrollmentCheckRateLimitSettings>()
            .BindConfiguration(EnrollmentCheckRateLimitSettings.SectionName);

        services.AddOptions<SeedingSettings>()
            .BindConfiguration(SeedingSettings.SectionName);

        services.AddSingleton<IValidateOptions<SocureSettings>, SocureSettingsValidator>();
        services.AddOptionsWithValidateOnStart<SocureSettings>()
            .BindConfiguration(SocureSettings.SectionName);

        services.AddSingleton<IValidateOptions<SmartySettings>, SmartySettingsValidator>();
        services.AddOptionsWithValidateOnStart<SmartySettings>()
            .BindConfiguration(SmartySettings.SectionName);
        services.AddOptions<AddressValidationPolicySettings>()
            .BindConfiguration(AddressValidationPolicySettings.SectionName);
        services.AddOptions<AddressValidationDataSettings>()
            .BindConfiguration(AddressValidationDataSettings.SectionName);

        return services;
    }
}
