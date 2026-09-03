using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Extensions;

namespace SEBT.Portal.Infrastructure.Configuration;

internal static class Dependencies
{
    public static IServiceCollection AddAppSettings(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddPortalOptions<AddressValidationPolicySettings>();
        services.AddPortalOptions<AddressValidationDataSettings>();
        services.AddPortalOptions<CheckerFeaturesRateLimitSettings>();
        services.AddPortalOptions<CoLoadedCohortFilterSettings>();
        services.AddPortalOptions<EmailOtpSenderServiceSettings>();
        services.AddPortalOptions<EnrollmentCheckerSettings>();
        services.AddPortalOptions<EnrollmentCheckRateLimitSettings>();
        services.AddPortalOptions<IdentifierHasherSettings>();
        services.AddPortalOptions<IdProofingEligibilitySettings>();
        services.AddPortalOptions<IdProofingRequirementsSettings, ConfigureIdProofingRequirements>(configuration);
        services.AddPortalOptions<IdProofingValiditySettings>();
        services.AddPortalOptions<JwtSettings>();
        services.AddPortalOptions<OidcStepUpSettings>();
        services.AddPortalOptions<OidcVerificationClaimSettings>();
        services.AddPortalOptions<OtpRateLimitSettings>();
        services.AddPortalOptions<OutageScheduleSettings>();
        services.AddPortalOptions<PiiEncryptionSettings>();
        services.AddPortalOptions<RedisSettings>();
        services.AddPortalOptions<SeedingSettings>();
        services.AddPortalOptions<SelfServiceRulesSettings>();
        services.AddPortalOptions<SmartySettings>();
        services.AddPortalOptions<SmtpClientSettings>();
        services.AddPortalOptions<SocureSettings>();
        services.AddPortalOptions<StateHouseholdIdSettings>();
        services.AddPortalOptions<WebhookRateLimitSettings>();

        // Add scoped raw value for CoLoadedCohortFilterSettings
        services.AddScoped(sp => sp.GetRequiredService<IOptionsSnapshot<CoLoadedCohortFilterSettings>>().Value);

        return services;
    }
}
