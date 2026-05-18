using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Kernel;
using SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;
using SEBT.Portal.UseCases.Auth;
using SEBT.Portal.UseCases.Auth.SessionLifetime;
using SEBT.Portal.UseCases.EnrollmentCheck;
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
        services.RegisterCommandHandler<ResubmitChallengeCommand, ResubmitChallengeResponse, ResubmitChallengeCommandHandler>();
        services.RegisterQueryHandler<GetVerificationStatusQuery, VerificationStatusResponse, GetVerificationStatusQueryHandler>();
        services.RegisterCommandHandler<ProcessWebhookCommand, ProcessWebhookCommandHandler>();
        services.RegisterCommandHandler<CheckEnrollmentCommand, EnrollmentCheckResult, CheckEnrollmentCommandHandler>();
        services.RegisterCommandHandler<UpdateAddressCommand, Core.Services.AddressValidationResult, UpdateAddressCommandHandler>();
        services.RegisterCommandHandler<RequestCardReplacementCommand, RequestCardReplacementCommandHandler>();

        // SessionLifetimePolicy is invoked by the JWT bearer middleware on every authenticated
        // request. TryAdd lets a host (e.g., tests) substitute a different TimeProvider.
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<SessionLifetimePolicy>();

        return services;
    }
}
