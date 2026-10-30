using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Scripts.RehashEmailHash;

internal static class RehashEmailHashConsole
{
    public static void WriteBanner(bool dryRun)
    {
        Console.WriteLine("EmailHash rehash");
        Console.WriteLine(
            dryRun
                ? "Mode: DRY RUN (no database writes)."
                : "Mode: APPLY (will update Users.EmailHash).");
        Console.WriteLine(
            "Uses IdentifierHasher:SecretKey from config as the target MAC secret, " +
            "and PiiEncryption keys to decrypt Email.");
        Console.WriteLine();
    }

    public static bool ConfirmApplyOrCancel()
    {
        Console.Write("Type yes to continue: ");
        var confirmation = Console.ReadLine();
        if (string.Equals(confirmation, "yes", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        Console.WriteLine("Cancelled.");
        return false;
    }

    public static void WriteResult(EmailHashRehashResult result)
    {
        Console.WriteLine();
        Console.WriteLine($"Examined:          {result.Examined}");
        Console.WriteLine($"Already current:   {result.AlreadyCurrent}");
        Console.WriteLine(result.DryRun
            ? $"Would update:      {result.WouldUpdate}"
            : $"Updated:           {result.Updated}");
        Console.WriteLine($"Decrypt failures:  {result.SkippedDecryptFailure}");
        Console.WriteLine($"Collision skips:   {result.SkippedCollision}");

        if (result.CollisionUserIds.Count == 0)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine(
            "Collision: multiple Users rows decrypt to the same normalized email " +
            "(would share one EmailHash). Skipped these user Ids; dedupe before retrying:");
        foreach (var id in result.CollisionUserIds)
        {
            Console.WriteLine($"  {id}");
        }
    }

    public static void WriteError(Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        if (ex.InnerException != null)
        {
            Console.WriteLine($"  Inner: {ex.InnerException.Message}");
        }
    }
}
