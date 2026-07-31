using System.Globalization;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Core.Utilities;

namespace SEBT.Portal.Infrastructure.StateBackends;

/// <summary>
/// The behavioral mirror of the plugin-path <c>HouseholdRepository</c>, with the state-specific
/// call shape moved into YAML config: each read becomes an identity-signal lookup.
/// </summary>
/// <remarks>
/// PII masking stays portal-side via the shared <see cref="HouseholdPiiFilter"/>; case IDs arrive
/// already composed into opaque tokens by the lookup driver.
/// </remarks>
public class StateBackendHouseholdRepository : IHouseholdRepository
{
    // Signal type names form the closed vocabulary state configs bind against.
    private const string EmailSignal = "email";
    private const string IcSignal = "ic";
    private const string DobSignal = "dob";
    private const string SocureUuidSignal = "socureUuid";

    private readonly IHouseholdLookupBackend _lookupBackend;

    public StateBackendHouseholdRepository(IHouseholdLookupBackend lookupBackend)
    {
        ArgumentNullException.ThrowIfNull(lookupBackend);
        _lookupBackend = lookupBackend;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <paramref name="includeCardService"/> is a plugin-path fetch optimization; the adapter path
    /// has no per-request knob, so it is a no-op here.
    /// </remarks>
    public Task<HouseholdData?> GetHouseholdByIdentifierAsync(
        HouseholdIdentifier identifier,
        PiiVisibility piiVisibility,
        UserIalLevel userIalLevel,
        Guid? portalUserId = null,
        bool includeCardService = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identifier);

        var signalType = MapToSignalType(identifier.Type);
        if (signalType == null)
        {
            // Mirrors the plugin path: an unmapped identifier type finds nothing.
            return Task.FromResult<HouseholdData?>(null);
        }

        return LookupBySignalAsync(
            signalType,
            identifier.Value,
            normalizeAsEmail: identifier.Type == PreferredHouseholdIdType.Email,
            piiVisibility,
            userIalLevel,
            portalUserId,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<HouseholdData?> GetHouseholdByEmailAsync(
        string email,
        PiiVisibility piiVisibility,
        UserIalLevel userIalLevel,
        Guid? portalUserId = null,
        bool includeCardService = true,
        CancellationToken cancellationToken = default)
    {
        return LookupBySignalAsync(
            EmailSignal,
            email ?? string.Empty,
            normalizeAsEmail: true,
            piiVisibility,
            userIalLevel,
            portalUserId,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> TryMatchCoLoadedGuardianByBenefitIdAndDobAsync(
        string benefitIdentifierIc,
        DateOnly guardianDateOfBirth,
        Guid portalUserId,
        CancellationToken cancellationToken = default)
    {
        // The plugin path throws here too (the DC connector guards the IC).
        ArgumentException.ThrowIfNullOrWhiteSpace(benefitIdentifierIc);

        var request = new HouseholdLookupRequest(
        [
            new IdentitySignal(IcSignal, benefitIdentifierIc.Trim()),
            new IdentitySignal(DobSignal, FormatDob(guardianDateOfBirth)),
        ])
        {
            // The match runs before proofing completes, so IsProofed stays at its false default.
            PortalUuid = portalUserId.ToString("D"),
        };

        var result = await _lookupBackend.LookupHouseholdAsync(request, cancellationToken);
        return result.Status == HouseholdLookupStatus.Found;
    }

    /// <inheritdoc />
    public async Task<HouseholdData?> GetHouseholdByBenefitIdentifierAndGuardianDobAsync(
        string guardianLoginEmail,
        string benefitIdentifierIc,
        DateOnly guardianDateOfBirth,
        PiiVisibility piiVisibility,
        UserIalLevel userIalLevel,
        Guid portalUserId,
        string? socureReferenceId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(piiVisibility);
        if (string.IsNullOrWhiteSpace(guardianLoginEmail))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(benefitIdentifierIc))
        {
            return null;
        }

        var normalizedEmail = EmailNormalizer.Normalize(guardianLoginEmail);
        var signals = new List<IdentitySignal>
        {
            new(IcSignal, benefitIdentifierIc.Trim()),
            new(DobSignal, FormatDob(guardianDateOfBirth)),
            new(EmailSignal, normalizedEmail),
        };
        if (!string.IsNullOrWhiteSpace(socureReferenceId))
        {
            // Omitted when absent — a mapOptional binding drops the field instead of sending "".
            signals.Add(new IdentitySignal(SocureUuidSignal, socureReferenceId.Trim()));
        }

        var request = new HouseholdLookupRequest(signals)
        {
            IsProofed = IsProofed(userIalLevel),
            PortalUuid = portalUserId.ToString("D"),
            // The lookup is keyed by IC + DOB; the login email is the identifier writes route by.
            HouseholdIdentifier = normalizedEmail,
        };

        var result = await _lookupBackend.LookupHouseholdAsync(request, cancellationToken);
        if (result.Status != HouseholdLookupStatus.Found || result.Household == null)
        {
            return null;
        }

        // Envelope-email stamping mirrors the DC connector; applied before the mask so
        // visibility still applies.
        var household = result.Household with { Email = normalizedEmail };
        return HouseholdPiiFilter.Apply(household, piiVisibility);
    }

    /// <inheritdoc />
    public Task UpsertHouseholdAsync(HouseholdData householdData, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            "StateBackendHouseholdRepository is read-only. Updating household data from state resources is not supported.");
    }

    private async Task<HouseholdData?> LookupBySignalAsync(
        string signalType,
        string identifierValue,
        bool normalizeAsEmail,
        PiiVisibility piiVisibility,
        UserIalLevel userIalLevel,
        Guid? portalUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(piiVisibility);
        if (string.IsNullOrWhiteSpace(identifierValue))
        {
            return null;
        }

        var normalizedValue = normalizeAsEmail
            ? EmailNormalizer.Normalize(identifierValue)
            : identifierValue.Trim();

        var request = new HouseholdLookupRequest([new IdentitySignal(signalType, normalizedValue)])
        {
            IsProofed = IsProofed(userIalLevel),
            PortalUuid = portalUserId?.ToString("D"),
            // Doubles as caller context so a caseId composition can pack it when the response
            // never echoes it.
            HouseholdIdentifier = normalizedValue,
        };

        var result = await _lookupBackend.LookupHouseholdAsync(request, cancellationToken);
        if (result.Status != HouseholdLookupStatus.Found || result.Household == null)
        {
            return null;
        }

        return HouseholdPiiFilter.Apply(result.Household, piiVisibility);
    }

    private static string? MapToSignalType(PreferredHouseholdIdType type)
    {
        return type switch
        {
            PreferredHouseholdIdType.Email => EmailSignal,
            PreferredHouseholdIdType.Phone => "phone",
            PreferredHouseholdIdType.SnapId => "snapId",
            PreferredHouseholdIdType.TanfId => "tanfId",
            PreferredHouseholdIdType.Ssn => "ssn",
            _ => null
        };
    }

    // Mirrors the DC connector's derivation; the backend sees only this boolean, which gates
    // its proofed-only lookup branches.
    private static bool IsProofed(UserIalLevel userIalLevel) =>
        userIalLevel >= UserIalLevel.IAL1plus;

    // ISO date, matching what the DC connector sends and what state configs bind.
    private static string FormatDob(DateOnly dateOfBirth) =>
        dateOfBirth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
