extern alias statePlugin;

using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Utilities;
using ISummerEbtCaseService = statePlugin::SEBT.Portal.StatesPlugins.Interfaces.ISummerEbtCaseService;

namespace SEBT.Portal.Infrastructure.Repositories;

/// <summary>
/// Household repository that delegates to the loaded state plugin (ISummerEbtCaseService).
/// Maps plugin HouseholdData to Core HouseholdData at the boundary.
/// </summary>
public class HouseholdRepository : IHouseholdRepository
{
    private readonly ISummerEbtCaseService _summerEbtCaseService;
    private readonly ILogger<HouseholdRepository> _logger;

    public HouseholdRepository(
        ISummerEbtCaseService summerEbtCaseService,
        ILogger<HouseholdRepository> logger)
    {
        _summerEbtCaseService = summerEbtCaseService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<HouseholdData?> GetHouseholdByEmailAsync(
        string email,
        bool includeAddress = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalizedEmail = EmailNormalizer.Normalize(email);

        _logger.LogDebug("Querying state plugin for household data by guardian email {Email}", normalizedEmail);

        var pluginHousehold = await _summerEbtCaseService.GetHouseholdByGuardianEmailAsync(
            normalizedEmail,
            includeAddress,
            cancellationToken);

        if (pluginHousehold == null)
        {
            _logger.LogInformation("No household data found for guardian email {Email}", normalizedEmail);
            return null;
        }

        _logger.LogInformation(
            "Retrieved household data for guardian {Email} with {ApplicationCount} application(s)",
            normalizedEmail,
            pluginHousehold.Applications.Count);

        return PluginHouseholdDataMapper.ToCore(pluginHousehold);
    }

    /// <inheritdoc />
    public Task UpsertHouseholdAsync(HouseholdData householdData, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "HouseholdRepository is read-only. Updating Household data from state resources is not supported.");
    }
}
