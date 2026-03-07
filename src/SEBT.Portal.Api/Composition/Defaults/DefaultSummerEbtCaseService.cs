using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

namespace SEBT.Portal.Api.Composition.Defaults;

/// <summary>
/// No-op implementation when no state plugin provides <see cref="ISummerEbtCaseService"/> (e.g. CO-only with OIDC).
/// Allows the app to start with a single plugin; household lookups return no data.
/// Replace with a plugin-side no-op when the CO plugin implements this interface.
/// </summary>
internal sealed class DefaultSummerEbtCaseService : ISummerEbtCaseService
{
    /// <inheritdoc />
    public Task<HouseholdData?> GetHouseholdByGuardianEmailAsync(
        string guardianEmail,
        PiiVisibility piiVisibility,
        IdentityAssuranceLevel identityAssuranceLevel,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<HouseholdData?>(null);
    }
}
