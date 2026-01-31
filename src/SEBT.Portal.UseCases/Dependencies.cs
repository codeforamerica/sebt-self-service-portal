using Microsoft.Extensions.DependencyInjection;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Kernel;
using SEBT.Portal.UseCases.Auth;
using SEBT.Portal.UseCases.Household;

namespace SEBT.Portal.UseCases;

public static class Dependencies
{
    public static IServiceCollection AddUseCases(this IServiceCollection services)
    {
        services.RegisterCommandHandler<RequestOtpCommand, RequestOtpCommandHandler>();
        services.RegisterCommandHandler<ValidateOtpCommand, string, ValidateOtpCommandHandler>();
        services.RegisterCommandHandler<RefreshTokenCommand, string, RefreshTokenCommandHandler>();
        services.RegisterQueryHandler<GetHouseholdDataQuery, HouseholdData, GetHouseholdDataQueryHandler>();

        return services;
    }
}
