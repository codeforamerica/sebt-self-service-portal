using Medallion.Threading;
using Medallion.Threading.Redis;
using Medallion.Threading.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.StateConnector;
using SEBT.Portal.Kernel.Services;
using SEBT.Portal.Infrastructure.Configuration;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Repositories;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.Infrastructure.StateConnector;
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

        // Reads the configured outage windows and answers whether one is currently active.
        services.AddSingleton<IOutageScheduleEvaluator, Services.OutageScheduleEvaluator>();

        // The single authority on outage page state: combines the schedule with the per-surface
        // manual flag ("schedule wins when windows target the surface, else the manual flag
        // decides"). Both the features endpoints and FeatureFlagQueryService go through it, so the
        // rule lives in one place. Scoped because it consumes the scoped IFeatureManager.
        services.AddScoped<IOutagePageStateResolver, Services.OutagePageStateResolver>();

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
                ? "https://us-street.api.smarty.com"
                : smarty.BaseUrl.TrimEnd('/');
            client.BaseAddress = new Uri(baseUrl + "/");
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(smarty.TimeoutSeconds, 1, 120));
        });

        services.AddTransient<SmartyAddressVerificationService>();
        services.AddTransient<PassThroughAddressVerificationService>();
        services.AddTransient<IAddressVerificationService>(sp =>
        {
            var smarty = sp.GetRequiredService<IOptionsSnapshot<SmartySettings>>().Value;
            return smarty.Enabled
                ? sp.GetRequiredService<SmartyAddressVerificationService>()
                : sp.GetRequiredService<PassThroughAddressVerificationService>();
        });

        // Diagnostics-only: exercises the Smarty verification error paths with canned responses.
        services.AddScoped<IAddressVerificationDiagnostics, SmartyAddressVerificationDiagnostics>();

        // Per-state blocked-address data file. CO ships a CSV
        // (county/government office addresses) embedded in this assembly; other
        // states fall back to the empty source and rely on the inline list in
        // AddressValidationData:BlockedAddresses for any small hand-curated entries.
        services.AddSingleton<IBlockedAddressDataSource>(_ =>
        {
            var state = Environment.GetEnvironmentVariable("STATE")?.ToLowerInvariant();
            return state switch
            {
                "co" => new CsvBlockedAddressDataSource(
                    typeof(CsvBlockedAddressDataSource).Assembly,
                    "SEBT.Portal.Infrastructure.BlockedAddresses.co-undeliverable-addresses.csv"),
                _ => new EmptyBlockedAddressDataSource()
            };
        });

        // Address validation — checks blocked addresses and street abbreviations per state config
        services.AddSingleton<IAddressValidationService, AddressValidationService>();

        // Self-service rules evaluator — evaluates per-state config against household data
        services.AddTransient<ISelfServiceEvaluator, SelfServiceEvaluator>();
        services.AddSingleton<IIdentifierHasher, IdentifierHasher>();
        services.AddSingleton<IHMACSHA256Hasher, HMACSHA256Hasher>();
        services.AddSingleton<IPiiSymmetricEncryption>(sp =>
            PiiSymmetricEncryptionFactory.Create(sp.GetRequiredService<IOptions<PiiEncryptionSettings>>()));
        services.AddSingleton<IEmailLookupHasher, EmailLookupHasher>();

        // State connector ports: adapters that map Core models to the plugin contract.
        // The plugin interfaces they wrap always resolve because the Api composition
        // layer registers defaults for any service the loaded plugin does not export.
        services.AddScoped<IStateEnrollmentCheckService, PluginEnrollmentCheckService>();
        services.AddScoped<IStateAddressUpdateService, PluginAddressUpdateService>();
        services.AddScoped<IStateCardReplacementService, PluginCardReplacementService>();

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
    /// Registers caching services. When Redis is configured (via structured settings
    /// or legacy connection string), uses Redis as the distributed cache (L2) backing
    /// HybridCache. Otherwise, falls back to in-memory caching only — except in
    /// non-Development environments with OIDC configured, where Redis is required for
    /// cross-container session lookup and startup fails fast.
    /// Call this before AddPlugins — plugins may depend on HybridCache.
    /// </summary>
    public static IServiceCollection AddCaching(
        this IServiceCollection services,
        IConfiguration? configuration,
        IHostEnvironment environment)
    {
        var redisOptions = ResolveRedisConfigurationOptions(configuration, environment);

        if (redisOptions != null)
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.ConfigurationOptions = redisOptions;
            });
        }
        else if (!environment.IsDevelopment()
            && !string.IsNullOrEmpty(configuration?["Oidc:DiscoveryEndpoint"]))
        {
            // Outside Development, OIDC + no Redis is misconfiguration: pre-auth sessions
            // live in a per-container in-memory cache, so callbacks landing on a different
            // container than the authorize-redirect see missing_session or replay errors.
            // Fail fast at startup instead of silently shipping a broken login flow.
            throw new InvalidOperationException(
                "Redis is required when OIDC is configured outside Development: " +
                "set Redis:Host (or legacy ConnectionStrings:Redis). " +
                "Cross-container session lookup depends on a shared distributed cache.");
        }
        else
        {
            // Fallback so IDistributedCache is always resolvable (PreAuthSessionStore
            // depends on it). Used for local dev without Redis and for integration tests
            // that omit Redis config.
            services.AddDistributedMemoryCache();
        }

        // HybridCache provides an L1 in-memory cache with optional L2 distributed backing.
        // When Redis is registered above, HybridCache automatically uses it as L2.
        // When Redis is not configured, HybridCache operates as in-memory only.
        services.AddHybridCache();
        services.AddMemoryCache();

        return services;
    }

    /// <summary>
    /// Registers a distributed lock provider. Uses Redis when Redis is configured
    /// (via structured settings or legacy connection string); otherwise falls back
    /// to SQL Server application locks.
    /// </summary>
    public static IServiceCollection AddDistributedLocking(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var redisOptions = ResolveRedisConfigurationOptions(configuration, environment);

        if (redisOptions != null)
        {
            services.AddSingleton<IDistributedLockProvider>(_ =>
            {
                var connection = ConnectionMultiplexer.Connect(redisOptions);
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
    /// Resolves Redis configuration from settings. Structured Redis:* settings take
    /// precedence; falls back to the legacy ConnectionStrings:Redis connection string.
    /// Returns null when neither is configured.
    /// </summary>
    internal static ConfigurationOptions? ResolveRedisConfigurationOptions(
        IConfiguration? configuration,
        IHostEnvironment environment)
    {
        var settings = configuration?.GetSection(RedisSettings.SectionName).Get<RedisSettings>();
        if (settings?.IsConfigured == true)
        {
            if (settings.AcceptSelfSignedCertificates && !environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "Redis:AcceptSelfSignedCertificates must only be true " +
                    "when ASPNETCORE_ENVIRONMENT == Development. " +
                    "Remove it from configuration — Elasticache presents an AWS-signed cert " +
                    "that .NET trusts natively.");
            }

            var options = new ConfigurationOptions();
            options.EndPoints.Add(settings.Host!, settings.Port);
            if (!string.IsNullOrEmpty(settings.Password))
            {
                options.Password = settings.Password;
            }
            options.Ssl = settings.Ssl;
            if (!string.IsNullOrEmpty(settings.SslHost))
            {
                options.SslHost = settings.SslHost;
            }
            if (settings.AcceptSelfSignedCertificates)
            {
                // Bypasses TLS cert validation for local dev with self-signed certs.
                // In production, Elasticache presents an AWS-signed cert that .NET
                // trusts without this — AcceptSelfSignedCertificates must be false.
                options.CertificateValidation += (_, _, _, _) => true;
            }
            return options;
        }

        var legacyConnectionString = configuration?.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(legacyConnectionString))
        {
            return ConfigurationOptions.Parse(legacyConnectionString);
        }

        return null;
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

        services.AddScoped<PiiPlaintextEncryptionBackfill>();
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
        services.AddSingleton<IValidateOptions<PiiEncryptionSettings>, PiiEncryptionSettingsValidator>();
        services.AddOptionsWithValidateOnStart<PiiEncryptionSettings>()
            .BindConfiguration(PiiEncryptionSettings.SectionName);
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
        services.AddOptions<IdProofingEligibilitySettings>()
            .BindConfiguration(IdProofingEligibilitySettings.SectionName);
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

        services.AddOptionsWithValidateOnStart<CheckerFeaturesRateLimitSettings>()
            .BindConfiguration(CheckerFeaturesRateLimitSettings.SectionName)
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

        services.AddOptions<EnrollmentCheckerSettings>()
            .BindConfiguration(EnrollmentCheckerSettings.SectionName);

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

        services.AddOptions<RedisSettings>()
            .BindConfiguration(RedisSettings.SectionName);

        // Outage schedule windows. IOptionsMonitor so AppConfig updates hot-reload without a redeploy.
        // ValidateOnStart refuses to boot on a malformed schedule, so a mistyped window cannot ship.
        // A malformed window pushed to AppConfig after boot is rejected on the reload thread instead;
        // AppConfigAgentReloadService logs it Critical and keeps the host alive.
        services.AddSingleton<IValidateOptions<OutageScheduleSettings>, OutageScheduleSettingsValidator>();
        services.AddOptionsWithValidateOnStart<OutageScheduleSettings>()
            .BindConfiguration(OutageScheduleSettings.SectionName);

        return services;
    }
}
