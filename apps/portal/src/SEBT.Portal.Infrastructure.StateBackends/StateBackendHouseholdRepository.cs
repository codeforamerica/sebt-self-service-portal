using System.Globalization;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Core.Utilities;

namespace SEBT.Portal.Infrastructure.StateBackends;

/// <summary>
/// Household repository over the config-driven state backend's household-lookup port —
/// the behavioral mirror of the plugin-path <c>HouseholdRepository</c> with the
/// state-specific call shape moved into the state's YAML config. Each read becomes an
/// identity-signal lookup; which signals a state actually binds (and whether a missing
/// one fails loud or is omitted) is decided by the config's request map vs mapOptional.
/// </summary>
/// <remarks>
/// The backend returns full data per its response mapping; PII masking stays a
/// portal-layer concern applied through the same shared <see cref="HouseholdPiiFilter"/>
/// the plugin path uses. Case IDs arrive already composed into opaque tokens by the
/// lookup driver (per the config's caseId fields), so no tokenization happens here.
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
    /// <paramref name="includeCardService"/> is a plugin-path fetch optimization (CO skips
    /// its FIS card-detail call when false). The adapter path has no per-request knob —
    /// card fields arrive, or not, per the state's response mapping — so it is a no-op here.
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
            // The plugin path sends isIdentityProofed=false on this call — the match runs
            // before proofing completes — so IsProofed stays at its false default.
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
            // Absent when the guardian has no Socure verification. Omitted entirely — a
            // config binding it via mapOptional drops the field instead of sending "".
            signals.Add(new IdentitySignal(SocureUuidSignal, socureReferenceId.Trim()));
        }

        var request = new HouseholdLookupRequest(signals)
        {
            IsProofed = IsProofed(userIalLevel),
            PortalUuid = portalUserId.ToString("D"),
        };

        var result = await _lookupBackend.LookupHouseholdAsync(request, cancellationToken);
        if (result.Status != HouseholdLookupStatus.Found || result.Household == null)
        {
            return null;
        }

        // Envelope email mirrors the DC connector's stamping: this lookup is keyed by
        // IC + DOB, so the login email the guardian authenticated with becomes the
        // household's contact email. Stamped before the mask so visibility still applies.
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

    // Exact mirror of the DC connector's derivation (ial >= IAL1plus). The backend never
    // sees the IAL itself — only this boolean, which gates its proofed-only lookup branches.
    private static bool IsProofed(UserIalLevel userIalLevel) =>
        userIalLevel >= UserIalLevel.IAL1plus;

    // ISO date, matching what the DC connector sends and what state configs bind.
    private static string FormatDob(DateOnly dateOfBirth) =>
        dateOfBirth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
