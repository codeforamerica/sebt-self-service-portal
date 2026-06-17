using System.Globalization;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;
using SEBT.Portal.Infrastructure.Data.Entities;

namespace SEBT.Portal.Infrastructure.Repositories;

internal static class UserEncryptedFieldMapper
{
    internal static User ToDomain(UserEntity entity, IPiiSymmetricEncryption crypto)
    {
        return new User
        {
            Id = entity.Id,
            Email = DecryptNormalizedEmail(entity, crypto),
            ExternalProviderId = entity.ExternalProviderId,
            IdProofingStatus = (IdProofingStatus)entity.IdProofingStatus,
            IalLevel = (UserIalLevel)entity.IalLevel,
            IdProofingSessionId = entity.IdProofingSessionId,
            IdProofingCompletedAt = entity.IdProofingCompletedAt,
#pragma warning disable CS0618 // legacy column mapping preserved until column is retired — see chore/retire-id-proofing-expires-at-column
            IdProofingExpiresAt = entity.IdProofingExpiresAt,
#pragma warning restore CS0618
            DateOfBirth = DecodeDateOnlySafe(crypto, entity.DateOfBirth),
            IsCoLoaded = entity.IsCoLoaded,
            CoLoadedLastUpdated = entity.CoLoadedLastUpdated,
            Phone = crypto.DecryptOrPassThroughLegacy(entity.Phone),
            SnapId = crypto.DecryptOrPassThroughLegacy(entity.SnapId),
            TanfId = crypto.DecryptOrPassThroughLegacy(entity.TanfId),
            Ssn = entity.Ssn,
            IdProofingAttemptCount = entity.IdProofingAttemptCount,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    /// <param name="includeEmailColumns">
    /// When false, OTP leave-behind semantics: callers already loaded the tracked entity and only want household + DOB fields encrypted.
    /// </param>
    internal static void EncryptIdentifiers(
        UserEntity entity,
        User user,
        IPiiSymmetricEncryption crypto,
        IEmailLookupHasher lookup,
        bool includeEmailColumns)
    {
        if (includeEmailColumns)
        {
            var normalizedEmail = EmailNormalizer.NormalizeOrNull(user.Email);
            entity.Email = normalizedEmail == null ? null : crypto.Encrypt(normalizedEmail);
            entity.EmailHash = lookup.NormalizeAndHash(user.Email);
        }

        entity.Phone = crypto.Encrypt(user.Phone);
        entity.SnapId = crypto.Encrypt(user.SnapId);
        entity.TanfId = crypto.Encrypt(user.TanfId);
        entity.DateOfBirth = crypto.Encrypt(
            user.DateOfBirth?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    internal static void ClearEmailColumns(UserEntity entity)
    {
        entity.Email = null;
        entity.EmailHash = null;
    }

    private static string? DecryptNormalizedEmail(UserEntity entity, IPiiSymmetricEncryption crypto)
    {
        if (string.IsNullOrEmpty(entity.Email))
        {
            return null;
        }

        var plain = crypto.DecryptOrPassThroughLegacy(entity.Email);
        return EmailNormalizer.NormalizeOrNull(plain);
    }

    private static DateOnly? DecodeDateOnlySafe(IPiiSymmetricEncryption crypto, string? stored)
    {
        if (string.IsNullOrEmpty(stored))
        {
            return null;
        }

        var plain = crypto.DecryptOrPassThroughLegacy(stored);
        if (string.IsNullOrWhiteSpace(plain))
        {
            return null;
        }

        plain = plain.Trim();
        if (DateOnly.TryParseExact(
                plain,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var exact))
        {
            return exact;
        }

        return DateOnly.TryParse(
            plain,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces,
            out var general)
            ? general
            : null;
    }
}
