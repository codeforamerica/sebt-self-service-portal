using System.Data.Common;
using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Exceptions;

namespace SEBT.Portal.Api.Startup;

/// <summary>
/// Gates post-migration <see cref="Infrastructure.Services.PiiPlaintextEncryptionBackfill"/> on
/// <see cref="PiiEncryptionSettings.RunStartupBackfill"/> and applies startup-safe error handling.
/// </summary>
public static class PiiEncryptionStartupBackfill
{
    internal const string SkippedBecauseDisabledMessage =
        "PII ciphertext backfill skipped (PiiEncryption:RunStartupBackfill is false).";

    /// <summary>Whether startup should invoke plaintext-to-ciphertext backfill after EF migrations.</summary>
    public static bool ShouldRunStartupBackfill(PiiEncryptionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.RunStartupBackfill && settings.EncryptAtRest;
    }

    /// <summary>
    /// Runs <paramref name="applyBackfillAsync"/> when enabled; logs and swallows failures so the host can continue starting.
    /// </summary>
    public static async Task RunIfEnabledAsync(
        PiiEncryptionSettings settings,
        Func<CancellationToken, Task> applyBackfillAsync,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(applyBackfillAsync);
        ArgumentNullException.ThrowIfNull(logger);

        if (!ShouldRunStartupBackfill(settings))
        {
            logger.LogInformation(SkippedBecauseDisabledMessage);
            return;
        }

        try
        {
            await applyBackfillAsync(cancellationToken);
        }
        catch (PiiDecryptException backfillEx)
        {
            logger.LogError(
                backfillEx,
                "PII ciphertext backfill failed due to decryption/authentication error. " +
                "Startup continues, but legacy plaintext may remain until this is resolved.");
        }
        catch (DbException backfillEx)
        {
            logger.LogWarning(
                backfillEx,
                "PII ciphertext backfill hit a database error (likely transient). " +
                "Startup continues; backfill should be retried.");
        }
        catch (Exception backfillEx)
        {
            logger.LogError(backfillEx, "PII ciphertext backfill step failed.");
        }
    }
}
