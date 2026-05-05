using Medallion.Threading;
using Medallion.Threading.Redis;
using Medallion.Threading.SqlServer;
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
using StackExchange.Redis;
using SEBT.Portal.StatesPlugins.Interfaces.Services;
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
        services.AddTransient<JwtTokenService>();
        services.AddTransient<ILocalLoginTokenService>(sp => sp.GetRequiredService<JwtTokenService>());
        services.AddTransient<IOidcTokenService>(sp => sp.GetRequiredService<JwtTokenService>());
        services.AddTransient<ISessionRefreshTokenService>(sp => sp.GetRequiredService<JwtTokenService>());

        // OIDC verification claim translation (maps IdP claims like socureIdVerificationLevel to portal IAL)
        services.AddTransient<OidcVerificationClaimTranslator>(sp =>
            new OidcVerificationClaimTranslator(
                sp.GetRequiredService<IOptions<OidcVerificationClaimSettings>>().Value,
                sp.GetRequiredService<IOptions<IdProofingValiditySettings>>().Value,
                sp.GetRequiredService<ILoggerFactory>().CreateLogger<OidcVerificationClaimTranslator>()));

        // Unified identity proofing service (PII visibility + authorization gates)
        services.AddSingleton<IdProofingService>();
        services.AddSingleton<IIdProofingService>(sp => sp.GetRequiredService<IdProofingService>());
        services.AddSingleton<IPiiVisibilityService>(sp => sp.GetRequiredService<IdProofingService>());

        // Enrollment Check logging
        services.AddScoped<IEnrollmentCheckSubmissionLogger, EnrollmentCheckSubmissionLogger>();

        // Feature Flag Services
        services.AddScoped<IFeatureFlagQueryService, Services.FeatureFlagQueryService>();

        // Household identifier resolution (state-configurable preferred household ID type)
        services.AddTransient<IHouseholdIdentifierResolver, HouseholdIdentifierResolver>();

        // Smarty address verification (or pass-through when disabled).
        // IHttpClientFactory is a singleton, so its configure delegate receives the
        // root provider — use IOptionsMonitor (singleton) instead of IOptionsSnapshot
        // (scoped). Monitor still supports live AppConfig reload.
        services.AddHttpClient("Smarty", (sp, client) =>
        {
            // IOptionsMonitor (singleton) instead of IOptionsSnapshot (scoped) — the
            // AddHttpClient delegate receives the root IServiceProvider, so scoped
            // services cannot be resolved here.
            var smarty = sp.GetRequiredService<IOptionsMonitor<SmartySettings>>().CurrentValue;
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
            var smarty = sp.GetRequiredService<IOptionsSnapshot<SmartySettings>>().Value;
            return smarty.Enabled
                ? sp.GetRequiredService<SmartyAddressUpdateService>()
                : sp.GetRequiredService<PassThroughAddressUpdateService>();
        });

        // Address validation — checks blocked addresses and street abbreviations per state config
        services.AddSingleton<IAddressValidationService, AddressValidationService>();

        // Self-service rules evaluator — evaluates per-state config against household data
        services.AddTransient<ISelfServiceEvaluator, SelfServiceEvaluator>();
        services.AddSingleton<IIdentifierHasher, IdentifierHasher>();
        services.AddSingleton<IHMACSHA256Hasher, HMACSHA256Hasher>();

        // Expose SocureSettings directly for use case injection (avoids IOptions dependency in UseCases layer).
        // Scoped so each request gets a consistent snapshot, supporting live AppConfig reload.
        services.AddScoped(sp => sp.GetRequiredService<IOptionsSnapshot<SocureSettings>>().Value);

        // Socure client — disabled, stub, or real based on configuration
        var socureEnabled = configuration.GetValue<bool>("Socure:Enabled");
        if (socureEnabled)
        {
            services.AddTransient<StubSocureClient>();
            services.AddTransient<HttpSocureClient>();
            services.AddTransient<ISocureClient>(sp =>
            {
                var settings = sp.GetRequiredService<IOptionsSnapshot<SocureSettings>>().Value;
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
    /// Registers a distributed lock provider. Uses Redis when a Redis connection
    /// string is configured; otherwise falls back to SQL Server application locks.
    /// </summary>
    public static IServiceCollection AddDistributedLocking(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var redisConnectionString = configuration.GetConnectionString("Redis");

        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            services.AddSingleton<IDistributedLockProvider>(_ =>
            {
                var connection = ConnectionMultiplexer.Connect(redisConnectionString);
                return new RedisDistributedSynchronizationProvider(connection.GetDatabase());
            });
        }
        else
        {
            var sqlConnectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Connection string 'DefaultConnection' is required for distributed locking.");
            services.AddSingleton<IDistributedLockProvider>(
                new SqlDistributedSynchronizationProvider(sqlConnectionString));
        }

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
            .BindConfiguration(EmailOtpSenderServiceSettings.SectionName)
            .ValidateDataAnnotations();
        services.AddOptionsWithValidateOnStart<SmtpClientSettings>()
            .BindConfiguration(SmtpClientSettings.SectionName);
        services.AddOptionsWithValidateOnStart<OtpRateLimitSettings>()
            .BindConfiguration(OtpRateLimitSettings.SectionName)
            .ValidateDataAnnotations();
        services.AddSingleton<IValidateOptions<JwtSettings>, JwtSettingsValidator>();
        services.AddOptionsWithValidateOnStart<JwtSettings>()
            .BindConfiguration(JwtSettings.SectionName)
            .ValidateDataAnnotations();
        services.AddOptions<StateHouseholdIdSettings>()
            .BindConfiguration(StateHouseholdIdSettings.SectionName);
        services.AddOptionsWithValidateOnStart<IdentifierHasherSettings>()
            .BindConfiguration(IdentifierHasherSettings.SectionName)
            .ValidateDataAnnotations();
        services.ConfigureOptions<ConfigureIdProofingRequirements>();
        services.AddSingleton<IOptionsChangeTokenSource<IdProofingRequirementsSettings>>(
            new ConfigurationChangeTokenSource<IdProofingRequirementsSettings>(
                configuration.GetSection(IdProofingRequirementsSettings.SectionName)));
        services.AddSingleton<IValidateOptions<IdProofingRequirementsSettings>, IdProofingRequirementsCoherenceValidator>();
        services.AddOptionsWithValidateOnStart<IdProofingRequirementsSettings>();

        services.AddSingleton<IValidateOptions<OidcStepUpSettings>, OidcStepUpSettingsValidator>();
        services.AddOptionsWithValidateOnStart<OidcStepUpSettings>()
            .BindConfiguration(OidcStepUpSettings.SectionName);

        services.AddOptions<IdProofingValiditySettings>()
            .BindConfiguration(IdProofingValiditySettings.SectionName);
        services.AddOptions<OidcVerificationClaimSettings>()
            .BindConfiguration(OidcVerificationClaimSettings.SectionName);

        services.AddOptions<FeatureManagementSettings>()
            .Bind(configuration.GetSection(FeatureManagementSettings.SectionName))
            .PostConfigure<IConfiguration>((options, config) =>
            {
                var postConfig = new FeatureManagementOptionsConfiguration(config);
                postConfig.PostConfigure(null, options);
            });

        services.AddOptionsWithValidateOnStart<EnrollmentCheckRateLimitSettings>()
            .BindConfiguration(EnrollmentCheckRateLimitSettings.SectionName)
            .ValidateDataAnnotations();

        services.AddOptionsWithValidateOnStart<WebhookRateLimitSettings>()
            .BindConfiguration(WebhookRateLimitSettings.SectionName)
            .ValidateDataAnnotations();

        services.AddOptions<SeedingSettings>()
            .BindConfiguration(SeedingSettings.SectionName);

        services.AddSingleton<IValidateOptions<SocureSettings>, SocureSettingsValidator>();
        services.AddOptionsWithValidateOnStart<SocureSettings>()
            .BindConfiguration(SocureSettings.SectionName)
            .ValidateDataAnnotations();

        services.AddSingleton<IValidateOptions<SelfServiceRulesSettings>, SelfServiceRulesSettingsValidator>();
        services.AddOptionsWithValidateOnStart<SelfServiceRulesSettings>()
            .BindConfiguration(SelfServiceRulesSettings.SectionName);

        services.AddOptions<CoLoadedCohortFilterSettings>()
            .BindConfiguration(CoLoadedCohortFilterSettings.SectionName);
        services.AddScoped(sp => sp.GetRequiredService<IOptionsSnapshot<CoLoadedCohortFilterSettings>>().Value);

        services.AddSingleton<IValidateOptions<SmartySettings>, SmartySettingsValidator>();
        services.AddOptionsWithValidateOnStart<SmartySettings>()
            .BindConfiguration(SmartySettings.SectionName)
            .ValidateDataAnnotations();
        services.AddOptions<AddressValidationPolicySettings>()
            .BindConfiguration(AddressValidationPolicySettings.SectionName);
        services.AddOptions<AddressValidationDataSettings>()
            .BindConfiguration(AddressValidationDataSettings.SectionName);

        return services;
    }
}
