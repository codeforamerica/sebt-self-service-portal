using System.Security.Claims;
using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.UseCases.Household;

/// <summary>
/// Handles retrieval of household data for an authenticated user.
/// Resolves the household identifier from claims (state-configurable), determines whether to include address based on ID proofing status, and fetches household data.
/// </summary>
public class GetHouseholdDataQueryHandler(
    IHouseholdIdentifierResolver resolver,
    IHouseholdRepository repository,
    ILogger<GetHouseholdDataQueryHandler> logger)
    : IQueryHandler<GetHouseholdDataQuery, HouseholdData>
{
    public async Task<Result<HouseholdData>> Handle(GetHouseholdDataQuery query, CancellationToken cancellationToken = default)
    {
        var identifier = await resolver.ResolveAsync(query.User, cancellationToken);

        if (identifier == null)
        {
            logger.LogWarning("Household data request attempted but no household identifier could be resolved from claims");
            return Result<HouseholdData>.Unauthorized("Unable to identify user from token.");
        }

        logger.LogDebug("Household data request received for identifier {Type}={Value}", identifier.Type, identifier.Value);

        var idProofingStatus = GetIdProofingStatus(query.User);
        var includeAddress = idProofingStatus == IdProofingStatus.Completed;

        if (includeAddress)
        {
            logger.LogDebug("Including address data for ID verified user");
        }

        var householdData = await repository.GetHouseholdByIdentifierAsync(
            identifier,
            includeAddress: includeAddress,
            cancellationToken);

        if (householdData == null)
        {
            logger.LogWarning("Household data not found for authenticated user");
            return Result<HouseholdData>.PreconditionFailed(PreconditionFailedReason.NotFound, "Household data not found.");
        }

        logger.LogDebug("Household data retrieved successfully for identifier {Type}={Value}", identifier.Type, identifier.Value);
        return Result<HouseholdData>.Success(householdData);
    }

    private static IdProofingStatus GetIdProofingStatus(ClaimsPrincipal user)
    {
        var statusClaim = user.FindFirst(JwtClaimTypes.IdProofingStatus)?.Value;

        if (string.IsNullOrWhiteSpace(statusClaim))
        {
            return IdProofingStatus.NotStarted;
        }

        if (int.TryParse(statusClaim, out var statusValue) &&
            Enum.IsDefined(typeof(IdProofingStatus), statusValue))
        {
            return (IdProofingStatus)statusValue;
        }

        return IdProofingStatus.NotStarted;
    }
}
