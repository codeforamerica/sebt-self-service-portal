using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;

namespace SEBT.Portal.Core.Services;

/// <summary>
/// Determines which PII fields a user can see based on their IAL level
/// and the configured view requirements.
/// Used by the repository layer for query filtering.
/// </summary>
public interface IPiiVisibilityService
{
    PiiVisibility GetVisibility(UserIalLevel userIalLevel);
}
