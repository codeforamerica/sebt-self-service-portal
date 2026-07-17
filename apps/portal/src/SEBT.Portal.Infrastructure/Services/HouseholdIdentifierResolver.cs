using System.Linq;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Resolves the preferred household identifier from the authenticated user.
/// For states that do not persist PII, phone can be resolved from the JWT only.
/// In Development, a configured phone override (DevelopmentPhoneOverride:Phone) takes precedence over JWT/user.
/// </summary>
public class HouseholdIdentifierResolver : IHouseholdIdentifierResolver
{
    private readonly StateHouseholdIdSettings _settings;
    private readonly IUserRepository _userRepository;
    private readonly IPhoneOverrideProvider _phoneOverrideProvider;
    private readonly ILogger<HouseholdIdentifierResolver>? _logger;

    public HouseholdIdentifierResolver(
        IOptionsSnapshot<StateHouseholdIdSettings> settingsSnapshot,
        IUserRepository userRepository,
        IPhoneOverrideProvider phoneOverrideProvider,
        ILogger<HouseholdIdentifierResolver>? logger = null)
    {
        _settings = settingsSnapshot.Value;
        _userRepository = userRepository;
        _phoneOverrideProvider = phoneOverrideProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<HouseholdIdentifier?> ResolveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var userId = principal.GetUserId();

        if (userId is null)
        {
            return null;
        }

        var user = await _userRepository.GetUserByIdAsync(userId.Value, cancellationToken);
        if (user == null)
        {
            return null;
        }

        var preferredTypes = _settings.PreferredHouseholdIdTypes;
        if (preferredTypes == null)
        {
            throw new InvalidOperationException(
                "StateHouseholdId:PreferredHouseholdIdTypes is null. Configure StateHouseholdId:PreferredHouseholdIdTypes in appsettings.json");
        }
        if (preferredTypes.Count == 0)
        {
            throw new InvalidOperationException(
                "StateHouseholdId:PreferredHouseholdIdTypes is empty. Configure at least one preferred type");
        }

        _logger?.LogInformation(
            "Resolving household identifier; preferred types: [{Types}]",
            string.Join(", ", preferredTypes.Select(t => t.ToString())));

        if (preferredTypes.Contains(PreferredHouseholdIdType.Phone))
        {
            var overridePhone = _phoneOverrideProvider.GetOverridePhone();
            if (!string.IsNullOrWhiteSpace(overridePhone))
            {
                _logger?.LogInformation("Using development phone override for household lookup");
                return new HouseholdIdentifier(PreferredHouseholdIdType.Phone, overridePhone);
            }

            var phoneFromClaims = GetValueFromClaims(principal, PreferredHouseholdIdType.Phone);
            if (!string.IsNullOrWhiteSpace(phoneFromClaims))
            {
                var normalized = phoneFromClaims.Trim();
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    _logger?.LogInformation("Using phone from JWT claims for household lookup");
                    return new HouseholdIdentifier(PreferredHouseholdIdType.Phone, normalized);
                }
            }

            _logger?.LogWarning("Failed to resolve phone number from JWT claims for household lookup");
        }

        foreach (var preferredType in preferredTypes)
        {
            var value = GetValueFromUser(user, preferredType)
                ?? GetValueFromClaims(principal, preferredType);
            if (!string.IsNullOrWhiteSpace(value))
            {
                var normalized = Normalize(preferredType, value);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    return new HouseholdIdentifier(preferredType, normalized);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the household identifier value from JWT claims when not persisted.
    /// Only supports types that IdPs commonly put in the token.
    /// </summary>
    private static string? GetValueFromClaims(ClaimsPrincipal principal, PreferredHouseholdIdType type)
    {
        if (type == PreferredHouseholdIdType.Phone)
        {
            var phone = principal.FindFirst("phone")?.Value
                ?? principal.FindFirst("phone_number")?.Value;
            return string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        }
        return null;
    }

    /// <summary>
    /// Gets the household identifier value from the user record
    /// </summary>
    private static string? GetValueFromUser(Core.Models.Auth.User user, PreferredHouseholdIdType type)
    {
        return type switch
        {
            PreferredHouseholdIdType.Email => user.Email,
            PreferredHouseholdIdType.Phone => user.Phone,
            PreferredHouseholdIdType.SnapId => user.SnapId,
            PreferredHouseholdIdType.TanfId => user.TanfId,
            PreferredHouseholdIdType.Ssn => user.Ssn,
            _ => null
        };
    }

    private static string? Normalize(PreferredHouseholdIdType type, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return type switch
        {
            PreferredHouseholdIdType.Email => EmailNormalizer.NormalizeOrNull(value),
            PreferredHouseholdIdType.Phone => value.Trim(),
            PreferredHouseholdIdType.SnapId => value.Trim(),
            PreferredHouseholdIdType.TanfId => value.Trim(),
            PreferredHouseholdIdType.Ssn => value.Trim(),
            _ => value.Trim()
        };
    }
}
