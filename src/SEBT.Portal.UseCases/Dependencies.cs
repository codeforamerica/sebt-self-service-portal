using Microsoft.Extensions.DependencyInjection;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Kernel;
using SEBT.Portal.UseCases.Auth;
using SEBT.Portal.UseCases.Household;
using SEBT.Portal.UseCases.IdProofing;

namespace SEBT.Portal.UseCases;

public static class Dependencies
{
    public static IServiceCollection AddUseCases(this IServiceCollection services)
    {
        services.RegisterCommandHandler<RequestOtpCommand, RequestOtpCommandHandler>();
        services.RegisterCommandHandler<ValidateOtpCommand, string, ValidateOtpCommandHandler>();
        services.RegisterCommandHandler<RefreshTokenCommand, string, RefreshTokenCommandHandler>();
        services.RegisterQueryHandler<GetHouseholdDataQuery, HouseholdData, GetHouseholdDataQueryHandler>();
        services.RegisterCommandHandler<SubmitIdProofingCommand, SubmitIdProofingResponse, SubmitIdProofingCommandHandler>();
        services.RegisterCommandHandler<StartChallengeCommand, StartChallengeResponse, StartChallengeCommandHandler>();
        services.RegisterQueryHandler<GetVerificationStatusQuery, VerificationStatusResponse, GetVerificationStatusQueryHandler>();
        services.RegisterCommandHandler<ProcessWebhookCommand, ProcessWebhookCommandHandler>();

        return services;
    }
}
