using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Core.Services;

/// <summary>
/// Authorization gate: evaluates whether a user meets the IAL requirement
/// for a resource+action, resolved against their household case types.
/// </summary>
public interface IIdProofingService
{
    /// <summary>
    /// Evaluates whether the user meets the IAL requirement for the
    /// requested resource+action, resolved against their case types.
    /// </summary>
    IdProofingDecision Evaluate(
        ProtectedResource resource,
        ProtectedAction action,
        UserIalLevel userIalLevel,
        IReadOnlyList<SummerEbtCase> cases);
}
