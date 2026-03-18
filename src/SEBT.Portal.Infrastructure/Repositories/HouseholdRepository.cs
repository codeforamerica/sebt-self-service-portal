using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Utilities;
using ISummerEbtCaseService = SEBT.Portal.StatesPlugins.Interfaces.ISummerEbtCaseService;
using PluginIdentityAssuranceLevel = SEBT.Portal.StatesPlugins.Interfaces.Models.IdentityAssuranceLevel;
using PluginPiiVisibility = SEBT.Portal.StatesPlugins.Interfaces.Models.PiiVisibility;

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
    public Task<HouseholdData?> GetHouseholdByIdentifierAsync(
        HouseholdIdentifier identifier,
        PiiVisibility piiVisibility,
        UserIalLevel userIalLevel,
        CancellationToken cancellationToken = default)
    {
        if (identifier.Type != PreferredHouseholdIdType.Email)
        {
            _logger.LogDebug("State plugin lookup supports only email identifier; ignoring type {Type}", identifier.Type);
            return Task.FromResult<HouseholdData?>(null);
        }

        return GetHouseholdByEmailAsync(identifier.Value, piiVisibility, userIalLevel, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<HouseholdData?> GetHouseholdByEmailAsync(
        string email,
        PiiVisibility piiVisibility,
        UserIalLevel userIalLevel,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(piiVisibility);
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalizedEmail = EmailNormalizer.Normalize(email);

        _logger.LogDebug("Querying state plugin for household data by guardian email {Email}", normalizedEmail);

        var pluginPii = new PluginPiiVisibility(
            piiVisibility.IncludeAddress,
            piiVisibility.IncludeEmail,
            piiVisibility.IncludePhone);
        var pluginIal = (PluginIdentityAssuranceLevel)(int)userIalLevel;
        var pluginHousehold = await _summerEbtCaseService.GetHouseholdByGuardianEmailAsync(
            normalizedEmail,
            pluginPii,
            pluginIal,
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

        var core = PluginHouseholdDataMapper.ToCore(pluginHousehold);
        if (core == null)
        {
            return null;
        }
        return ApplyPiiVisibility(core, piiVisibility);
    }

    private static HouseholdData ApplyPiiVisibility(HouseholdData source, PiiVisibility piiVisibility)
    {
        return source with
        {
            Email = piiVisibility.IncludeEmail ? source.Email : PiiMasker.MaskEmail(source.Email),
            Phone = piiVisibility.IncludePhone ? source.Phone : PiiMasker.MaskPhone(source.Phone),
            AddressOnFile = piiVisibility.IncludeAddress && source.AddressOnFile != null
                ? new Address
                {
                    StreetAddress1 = source.AddressOnFile.StreetAddress1,
                    StreetAddress2 = source.AddressOnFile.StreetAddress2,
                    City = source.AddressOnFile.City,
                    State = source.AddressOnFile.State,
                    PostalCode = source.AddressOnFile.PostalCode
                }
                : source.AddressOnFile != null
                    ? new Address
                    {
                        StreetAddress1 = PiiMasker.MaskStreetAddress(source.AddressOnFile.StreetAddress1, source.AddressOnFile.StreetAddress2),
                        City = source.AddressOnFile.City,
                        State = source.AddressOnFile.State,
                        PostalCode = source.AddressOnFile.PostalCode
                    }
                    : null
        };
    }

    /// <inheritdoc />
    public Task UpsertHouseholdAsync(HouseholdData householdData, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "HouseholdRepository is read-only. Updating Household data from state resources is not supported.");
    }
}
