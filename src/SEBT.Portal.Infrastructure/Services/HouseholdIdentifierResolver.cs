using System.Security.Claims;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Resolves the preferred household identifier from the authenticated user's server-side record.
/// </summary>
public class HouseholdIdentifierResolver : IHouseholdIdentifierResolver
{
    private readonly StateHouseholdIdSettings _settings;
    private readonly IUserRepository _userRepository;

    public HouseholdIdentifierResolver(
        IOptions<StateHouseholdIdSettings> settings,
        IUserRepository userRepository)
    {
        _settings = settings.Value;
        _userRepository = userRepository;
    }

    /// <inheritdoc />
    public async Task<HouseholdIdentifier?> ResolveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var email = GetEmailFromClaims(principal);
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalizedEmail = EmailNormalizer.NormalizeOrNull(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return null;
        }

        var user = await _userRepository.GetUserByEmailAsync(normalizedEmail, cancellationToken);
        if (user == null)
        {
            return null;
        }

        var preferredTypes = _settings.PreferredHouseholdIdTypes;
        if (preferredTypes == null || preferredTypes.Count == 0)
        {
            preferredTypes = [PreferredHouseholdIdType.Email];
        }

        foreach (var preferredType in preferredTypes)
        {
            var value = GetValueFromUser(user, preferredType);
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
    /// Gets the user's email from JWT claims (the only identity we need in the token).
    /// </summary>
    private static string? GetEmailFromClaims(ClaimsPrincipal principal)
    {
        var email = principal.FindFirst(ClaimTypes.Email)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.Identity?.Name;
        return string.IsNullOrWhiteSpace(email) ? null : email.Trim();
    }

    /// <summary>
    /// Gets the household identifier value from the user record (server-side only; never from JWT).
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
