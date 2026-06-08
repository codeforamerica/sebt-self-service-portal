namespace SEBT.Portal.Infrastructure.Services;

/// <summary>Shared plaintext column normalization when <see cref="Core.AppSettings.PiiEncryptionSettings.EncryptAtRest"/> is off.</summary>
internal static class PiiPlaintextColumnStorage
{
    internal static string? StorePlaintextForColumn(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return null;
        }

        var trimmed = plaintext.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
