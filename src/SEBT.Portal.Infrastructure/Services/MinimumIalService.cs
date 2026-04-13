using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Determines the minimum IAL a user must achieve based on the co-loading
/// and streamline-certification status of their Summer EBT cases.
/// Applies state-configurable policy via <see cref="MinimumIalSettings"/>.
/// </summary>
public class MinimumIalService : IMinimumIalService
{
    private readonly MinimumIalSettings _settings;
    private readonly ILogger<MinimumIalService> _logger;

    public MinimumIalService(
        IOptionsSnapshot<MinimumIalSettings> settingsSnapshot,
        ILogger<MinimumIalService> logger)
    {
        _settings = settingsSnapshot.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public UserIalLevel GetMinimumIal(IReadOnlyList<SummerEbtCase> cases)
    {
        if (cases.Count == 0)
        {
            _logger.LogDebug("No cases found; minimum IAL is IAL1 (no elevated requirement)");
            return UserIalLevel.IAL1;
        }

        var highest = cases.Max(ClassifyCase);
        var result = ToUserIalLevel(highest);
        _logger.LogInformation(
            "Minimum IAL determined as {MinimumIal} from {CaseCount} case(s)",
            result,
            cases.Count);
        return result;
    }

    private IalLevel ClassifyCase(SummerEbtCase c)
    {
        if (!c.IsStreamlineCertified)
        {
            return _settings.ApplicationCases!.Value;
        }

        return c.IsCoLoaded
            ? _settings.CoLoadedStreamlineCases!.Value
            : _settings.NonCoLoadedStreamlineCases!.Value;
    }

    private static UserIalLevel ToUserIalLevel(IalLevel level)
    {
        return level switch
        {
            IalLevel.IAL1 => UserIalLevel.IAL1,
            IalLevel.IAL1plus => UserIalLevel.IAL1plus,
            IalLevel.IAL2 => UserIalLevel.IAL2,
            _ => UserIalLevel.IAL1
        };
    }
}
