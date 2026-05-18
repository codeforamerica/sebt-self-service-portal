using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.UseCases.IdProofing;

/// <summary>
/// Resolves Socure eligibility from household data without enforcing the dashboard
/// <see cref="IIdProofingService"/> view gate — IAL may still be IAL1 at this routing step.
/// </summary>
public class GetSocureEligibilityQueryHandler(
    IHouseholdIdentifierResolver resolver,
    IHouseholdRepository householdRepository,
    IOptions<IdProofingEligibilitySettings> eligibilityOptions,
    ILogger<GetSocureEligibilityQueryHandler> logger)
    : IQueryHandler<GetSocureEligibilityQuery, SocureEligibilityResponse>
{
    public async Task<Result<SocureEligibilityResponse>> Handle(
        GetSocureEligibilityQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!eligibilityOptions.Value.RequireQualifyingHouseholdForSocure)
        {
            return Result<SocureEligibilityResponse>.Success(new SocureEligibilityResponse(CanProceedToSocure: true));
        }

        var identifier = await resolver.ResolveAsync(query.User, cancellationToken);
        if (identifier == null)
        {
            logger.LogInformation(
                "Socure eligibility: household identifier unresolved; proceeding to Socure is blocked.");
            return Result<SocureEligibilityResponse>.Success(new SocureEligibilityResponse(CanProceedToSocure: false));
        }

        HouseholdData? household = null;

        try
        {
            var userIal = UserIalLevelExtensions.FromClaimsPrincipal(query.User);
            var warehouseIal = PreSocureHouseholdWarehouseIal.ForEmailLinkedHouseholdRead(
                userIal,
                eligibilityOptions.Value.RequireQualifyingHouseholdForSocure);

            household = await householdRepository.GetHouseholdByIdentifierAsync(
                identifier,
                new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false),
                warehouseIal,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Socure eligibility: household lookup failed; denying Socure routing until verified.");
            return Result<SocureEligibilityResponse>.Success(new SocureEligibilityResponse(CanProceedToSocure: false));
        }

        var canProceed = HouseholdSocureEligibility.HasQualifyingHouseholdForSocure(household);
        if (!canProceed)
        {
            logger.LogInformation(
                "Socure eligibility: user has no qualifying household (missing or no cases and no applications).");
        }

        return Result<SocureEligibilityResponse>.Success(new SocureEligibilityResponse(canProceed));
    }
}
