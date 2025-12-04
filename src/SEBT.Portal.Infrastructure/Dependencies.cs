using Microsoft.Extensions.DependencyInjection;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Infrastructure;

public static class Dependencies
{
    public static IServiceCollection AddPortalInfrastructureServices(this IServiceCollection services)
    {
        // Otp Services
        services.AddTransient<IOtpSenderService, EmailOtpSenderService>();
        services.AddTransient<IOtpGeneratorService, OtpGeneratorService>();
        services.AddTransient<ISmtpClientService, SmtpClientService>();

        return services;
    }

    public static IServiceCollection AddPortalInfrastructureRepositories(this IServiceCollection services)
    {
        services.AddTransient<IOtpRepository, InMemoryOtpRepository>();
        services.AddMemoryCache();

        return services;
    }

    public static IServiceCollection AddPortalInfrastructureAppSettings(this IServiceCollection services)
    {

        services.AddOptionsWithValidateOnStart<EmailOtpSenderServiceSettings>(EmailOtpSenderServiceSettings.SectionName);
        services.AddOptionsWithValidateOnStart<SmtpClientSettings>(SmtpClientSettings.SectionName);

        return services;
    }
}
