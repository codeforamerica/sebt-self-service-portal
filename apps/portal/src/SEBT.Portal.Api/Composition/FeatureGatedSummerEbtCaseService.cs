using Microsoft.FeatureManagement;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Models;
using PluginHouseholdData = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.HouseholdData;
using PluginHouseholdIdentifierType = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.HouseholdIdentifierType;
using PluginPiiVisibility = SEBT.Portal.StatesPlugins.Interfaces.Models.PiiVisibility;

namespace SEBT.Portal.Api.Composition;

internal sealed class FeatureGatedSummerEbtCaseService(
    IFeatureManager featureManager,
    ISummerEbtCaseService plugin,
    IHouseholdLookupBackend? adapter) : ISummerEbtCaseService
{
    public async Task<PluginHouseholdData?> GetHouseholdByIdentifierAsync(
        PluginHouseholdIdentifierType identifierType,
        string identifierValue,
        PluginPiiVisibility piiVisibility,
        IdentityAssuranceLevel identityAssuranceLevel,
        Guid? portalUserId = null,
        bool includeCardService = true,
        CancellationToken cancellationToken = default)
    {
        if (!await ConfigurableStateBackendGate.UseAdapterAsync(featureManager, adapter))
        {
            return await plugin.GetHouseholdByIdentifierAsync(
                identifierType,
                identifierValue,
                piiVisibility,
                identityAssuranceLevel,
                portalUserId,
                includeCardService,
                cancellationToken);
        }

        return await LookupAsync(
            [new IdentitySignal(ToSignalType(identifierType), identifierValue)],
            identityAssuranceLevel,
            portalUserId,
            identifierValue,
            cancellationToken);
    }

    public async Task<PluginHouseholdData?> GetHouseholdByGuardianEmailAsync(
        string guardianEmail,
        PluginPiiVisibility piiVisibility,
        IdentityAssuranceLevel identityAssuranceLevel,
        Guid? portalUserId = null,
        bool includeCardService = true,
        CancellationToken cancellationToken = default)
    {
        if (!await ConfigurableStateBackendGate.UseAdapterAsync(featureManager, adapter))
        {
            return await plugin.GetHouseholdByGuardianEmailAsync(
                guardianEmail,
                piiVisibility,
                identityAssuranceLevel,
                portalUserId,
                includeCardService,
                cancellationToken);
        }

        return await GetHouseholdByIdentifierAsync(
            PluginHouseholdIdentifierType.Email,
            guardianEmail,
            piiVisibility,
            identityAssuranceLevel,
            portalUserId,
            includeCardService,
            cancellationToken);
    }

    public async Task<bool> TryMatchCoLoadedGuardianByBenefitIdAndDobAsync(
        string benefitIdentifierIc,
        DateOnly guardianDateOfBirth,
        Guid portalUserId,
        CancellationToken cancellationToken = default)
    {
        if (!await ConfigurableStateBackendGate.UseAdapterAsync(featureManager, adapter))
        {
            return await plugin.TryMatchCoLoadedGuardianByBenefitIdAndDobAsync(
                benefitIdentifierIc,
                guardianDateOfBirth,
                portalUserId,
                cancellationToken);
        }

        HouseholdLookupResult result = await adapter!.LookupHouseholdAsync(
            new HouseholdLookupRequest(
            [
                new IdentitySignal("ic", benefitIdentifierIc),
                new IdentitySignal("dob", guardianDateOfBirth.ToString("yyyy-MM-dd")),
            ])
            {
                PortalUuid = portalUserId.ToString(),
            },
            cancellationToken);
        return result.Status == HouseholdLookupStatus.Found;
    }

    public async Task<PluginHouseholdData?> GetHouseholdByBenefitIdentifierAndDobAsync(
        string benefitIdentifierIc,
        DateOnly guardianDateOfBirth,
        string guardianLoginEmail,
        PluginPiiVisibility piiVisibility,
        IdentityAssuranceLevel identityAssuranceLevel,
        Guid portalUserId,
        string? socureReferenceId = null,
        CancellationToken cancellationToken = default)
    {
        if (!await ConfigurableStateBackendGate.UseAdapterAsync(featureManager, adapter))
        {
            return await plugin.GetHouseholdByBenefitIdentifierAndDobAsync(
                benefitIdentifierIc,
                guardianDateOfBirth,
                guardianLoginEmail,
                piiVisibility,
                identityAssuranceLevel,
                portalUserId,
                socureReferenceId,
                cancellationToken);
        }

        var signals = new List<IdentitySignal>
        {
            new("ic", benefitIdentifierIc),
            new("dob", guardianDateOfBirth.ToString("yyyy-MM-dd")),
            new("email", guardianLoginEmail),
        };
        if (!string.IsNullOrWhiteSpace(socureReferenceId))
        {
            signals.Add(new IdentitySignal("socureUuid", socureReferenceId));
        }

        return await LookupAsync(
            signals,
            identityAssuranceLevel,
            portalUserId,
            guardianLoginEmail,
            cancellationToken);
    }

    private async Task<PluginHouseholdData?> LookupAsync(
        IReadOnlyList<IdentitySignal> signals,
        IdentityAssuranceLevel identityAssuranceLevel,
        Guid? portalUserId,
        string? householdIdentifier,
        CancellationToken cancellationToken)
    {
        var request = new HouseholdLookupRequest(signals)
        {
            IsProofed = identityAssuranceLevel != IdentityAssuranceLevel.None,
            PortalUuid = portalUserId?.ToString(),
            HouseholdIdentifier = householdIdentifier,
        };

        HouseholdLookupResult result = await adapter!.LookupHouseholdAsync(request, cancellationToken);
        if (result.Status != HouseholdLookupStatus.Found || result.Household is null)
        {
            return null;
        }

        return CoreToPluginHouseholdMapper.ToPlugin(result.Household);
    }

    private static string ToSignalType(PluginHouseholdIdentifierType type) =>
        type switch
        {
            PluginHouseholdIdentifierType.Email => "email",
            PluginHouseholdIdentifierType.Phone => "phone",
            PluginHouseholdIdentifierType.SnapId => "snapId",
            PluginHouseholdIdentifierType.TanfId => "tanfId",
            PluginHouseholdIdentifierType.Ssn => "ssn",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported household identifier type."),
        };
}
