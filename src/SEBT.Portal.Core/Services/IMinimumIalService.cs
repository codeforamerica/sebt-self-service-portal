using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Core.Services;

/// <summary>
/// Determines the minimum Identity Assurance Level a user must achieve
/// based on the co-loading and streamline-certification status of their cases.
/// </summary>
public interface IMinimumIalService
{
    /// <summary>
    /// Computes the minimum IAL required across all of a user's Summer EBT cases.
    /// The "highest wins" rule applies: if any case requires a higher IAL,
    /// the user must meet that level.
    /// </summary>
    /// <param name="cases">The user's Summer EBT cases.</param>
    /// <returns>The minimum IAL the user must achieve. Returns <see cref="UserIalLevel.IAL1"/>
    /// when <paramref name="cases"/> is empty (no elevated requirement).</returns>
    UserIalLevel GetMinimumIal(IReadOnlyList<SummerEbtCase> cases);
}
