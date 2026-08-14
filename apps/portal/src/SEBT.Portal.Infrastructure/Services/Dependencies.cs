using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel.Services;
using SEBT.Portal.StatesPlugins.Interfaces.Services;

namespace SEBT.Portal.Infrastructure.Services;

internal static class Dependencies
{
    internal static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
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

        services.AddTransient<SmartyAddressUpdateService>();
        services.AddTransient<PassThroughAddressUpdateService>();
        services.AddTransient<IAddressUpdateService>(sp =>
        {
            var smarty = sp.GetRequiredService<IOptionsSnapshot<SmartySettings>>().Value;
            return smarty.Enabled
                ? sp.GetRequiredService<SmartyAddressUpdateService>()
                : sp.GetRequiredService<PassThroughAddressUpdateService>();
        });

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
}
