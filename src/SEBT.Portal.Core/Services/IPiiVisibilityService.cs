using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Core.Services;

/// <summary>
/// Determines which PII fields a user can see based on their IAL level
/// and the configured view requirements.
/// Used by the repository layer for query filtering.
/// </summary>
public interface IPiiVisibilityService
{
    /// <summary>
    /// Resolves visibility for a user without case context. Per-case-type
    /// view requirements degrade to IAL1 here (no cases = no case-derived
    /// reason to require elevated IAL), so callers that have access to
    /// the user's cases should prefer the overload that accepts them.
    /// </summary>
    PiiVisibility GetVisibility(UserIalLevel userIalLevel);

    /// <summary>
    /// Resolves visibility against the user's actual cases. Required for
    /// per-case-type view requirements (e.g. <c>address+view</c>) to apply
    /// correctly — without the cases, the "highest wins" rule has nothing
    /// to resolve against.
    /// </summary>
    PiiVisibility GetVisibility(UserIalLevel userIalLevel, IReadOnlyList<SummerEbtCase> cases);
}
