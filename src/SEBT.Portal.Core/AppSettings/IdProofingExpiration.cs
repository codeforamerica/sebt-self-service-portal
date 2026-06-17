namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Computes the legacy <c>Users.IdProofingExpiresAt</c> column from completion time and validity settings.
/// New code should treat expiration as <c>IdProofingCompletedAt</c> + <see cref="IdProofingValiditySettings.ValidityDays"/>.
/// </summary>
public static class IdProofingExpiration
{
    public static DateTime? ComputeStoredExpiration(
        DateTime? completedAt,
        int validityDays = 1826) =>
        completedAt?.AddDays(validityDays);
}
