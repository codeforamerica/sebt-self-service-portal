using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Core.Repositories;

/// <summary>
/// Persists the Summer EBT cases displayed to an authenticated portal user so later
/// operations (card replacement, address update, enrollment check) can join back to that user.
/// </summary>
public interface IUserCaseRepository
{
    /// <summary>
    /// Inserts a row for each newly seen case and periodically refreshes the last-seen timestamp on
    /// stored cases, rather than on every call. Cases omitted from the list are left in place.
    /// </summary>
    Task UpsertDisplayedCasesAsync(
        Guid userId,
        IReadOnlyList<UserCaseIdentifiers> cases,
        CancellationToken cancellationToken = default);
}
