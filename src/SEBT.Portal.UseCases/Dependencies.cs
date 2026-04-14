using Microsoft.Extensions.DependencyInjection;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;
using SEBT.Portal.UseCases.Auth;
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
        services.RegisterCommandHandler<CompleteOidcLoginCommand, CompleteOidcLoginResult, CompleteOidcLoginCommandHandler>();
        // Override the DataAnnotations validator with our custom validator that also checks the state allowlist.
        // The custom validator wraps DataAnnotationsValidator (registered as a keyed service to break the
        // circular dependency, since both implement IValidator<CompleteOidcLoginCommand>).
        services.AddKeyedTransient<IValidator<CompleteOidcLoginCommand>, DataAnnotationsValidator<CompleteOidcLoginCommand>>("dataAnnotations");
        services.AddTransient<IValidator<CompleteOidcLoginCommand>>(sp =>
            new CompleteOidcLoginCommandValidator(
                sp.GetRequiredKeyedService<IValidator<CompleteOidcLoginCommand>>("dataAnnotations"),
                sp.GetRequiredService<IStateAllowlist>()));
        services.RegisterQueryHandler<GetHouseholdDataQuery, HouseholdData, GetHouseholdDataQueryHandler>();
        services.RegisterCommandHandler<SubmitIdProofingCommand, SubmitIdProofingResponse, SubmitIdProofingCommandHandler>();
        services.RegisterCommandHandler<StartChallengeCommand, StartChallengeResponse, StartChallengeCommandHandler>();
        services.RegisterQueryHandler<GetVerificationStatusQuery, VerificationStatusResponse, GetVerificationStatusQueryHandler>();
        services.RegisterCommandHandler<ProcessWebhookCommand, ProcessWebhookCommandHandler>();
        services.RegisterCommandHandler<CheckEnrollmentCommand, EnrollmentCheckResult, CheckEnrollmentCommandHandler>();
        services.RegisterCommandHandler<UpdateAddressCommand, Core.Services.AddressValidationResult, UpdateAddressCommandHandler>();
        services.RegisterCommandHandler<RequestCardReplacementCommand, RequestCardReplacementCommandHandler>();

        return services;
    }
}
