using Microsoft.Extensions.DependencyInjection;
using SEBT.Portal.Kernel;
using SEBT.Portal.UseCases.Auth;

namespace SEBT.Portal.UseCases;

public static class Dependencies
{
    public static IServiceCollection AddUseCases(this IServiceCollection services)
    {
        services.RegisterCommandHandler<RequestOtpCommand, RequestOtpCommandHandler>();
        services.RegisterCommandHandler<ValidateOtpCommand, string, ValidateOtpCommandHandler>();

        return services;
    }
}
