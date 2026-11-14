using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Models.UserActions;

namespace SEBT.Portal.Core.Repositories;

/// <summary>
/// Records the actions users send to the state's system of record, for reporting and debugging.
/// </summary>
public interface IUserActionRepository
{
    /// <summary>
    /// Records an action sent to the state's system of record and its outcome, writing one row per case.
    /// </summary>
    Task RecordAsync(
        Guid userId,
        UserActionType action,
        UserActionOutcome outcome,
        IReadOnlyList<UserCaseIdentifiers> cases,
        CancellationToken cancellationToken = default);
}
