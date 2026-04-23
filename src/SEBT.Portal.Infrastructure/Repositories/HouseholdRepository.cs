using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Utilities;
using ISummerEbtCaseService = SEBT.Portal.StatesPlugins.Interfaces.ISummerEbtCaseService;
using PluginHouseholdIdentifierType = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.HouseholdIdentifierType;
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
        var pluginType = MapToPluginIdentifierType(identifier.Type);
        if (pluginType == null)
        {
            _logger.LogDebug("State plugin does not support the provided identifier type.");
            return Task.FromResult<HouseholdData?>(null);
        }

        return GetHouseholdByIdentifierInternalAsync(
            pluginType.Value,
            identifier.Value,
            piiVisibility,
            userIalLevel,
            cancellationToken);
    }

    private async Task<HouseholdData?> GetHouseholdByIdentifierInternalAsync(
        PluginHouseholdIdentifierType identifierType,
        string identifierValue,
        PiiVisibility piiVisibility,
        UserIalLevel userIalLevel,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(piiVisibility);
        if (string.IsNullOrWhiteSpace(identifierValue))
        {
            return null;
        }

        var normalizedValue = identifierType == PluginHouseholdIdentifierType.Email
            ? EmailNormalizer.Normalize(identifierValue)
            : identifierValue.Trim();

        _logger.LogDebug(
            "Querying state plugin for household data by identifier type {Type}",
            identifierType);

        var pluginPii = new PluginPiiVisibility(
            piiVisibility.IncludeAddress,
            piiVisibility.IncludeEmail,
            piiVisibility.IncludePhone);
        var pluginIal = (PluginIdentityAssuranceLevel)(int)userIalLevel;
        var pluginHousehold = await _summerEbtCaseService.GetHouseholdByIdentifierAsync(
            identifierType,
            normalizedValue,
            pluginPii,
            pluginIal,
            cancellationToken);

        if (pluginHousehold == null)
        {
            _logger.LogInformation(
                "No household data found for identifier type {Type}",
                identifierType);
            return null;
        }

        _logger.LogInformation(
            "Retrieved household data for identifier type {Type} with {ApplicationCount} application(s)",
            identifierType,
            pluginHousehold.Applications.Count);

        var core = PluginHouseholdDataMapper.ToCore(pluginHousehold);
        if (core == null)
        {
            return null;
        }
        return ApplyPiiVisibility(core, piiVisibility);
    }

    private static PluginHouseholdIdentifierType? MapToPluginIdentifierType(PreferredHouseholdIdType type)
    {
        return type switch
        {
            PreferredHouseholdIdType.Email => PluginHouseholdIdentifierType.Email,
            PreferredHouseholdIdType.Phone => PluginHouseholdIdentifierType.Phone,
            PreferredHouseholdIdType.SnapId => PluginHouseholdIdentifierType.SnapId,
            PreferredHouseholdIdType.TanfId => PluginHouseholdIdentifierType.TanfId,
            PreferredHouseholdIdType.Ssn => PluginHouseholdIdentifierType.Ssn,
            _ => null
        };
    }

    /// <inheritdoc />
    public Task<HouseholdData?> GetHouseholdByEmailAsync(
        string email,
        PiiVisibility piiVisibility,
        UserIalLevel userIalLevel,
        CancellationToken cancellationToken = default)
    {
        return GetHouseholdByIdentifierInternalAsync(
            PluginHouseholdIdentifierType.Email,
            email ?? string.Empty,
            piiVisibility,
            userIalLevel,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> TryMatchCoLoadedGuardianByBenefitIdAndDobAsync(
        string benefitIdentifierIc,
        DateOnly guardianDateOfBirth,
        CancellationToken cancellationToken = default)
    {
        return _summerEbtCaseService.TryMatchCoLoadedGuardianByBenefitIdAndDobAsync(
            benefitIdentifierIc,
            guardianDateOfBirth,
            cancellationToken);
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
