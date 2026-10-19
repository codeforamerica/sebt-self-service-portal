// Rewrites Users.EmailHash under the configured IdentifierHasher:SecretKey.
//
// Usage (from repo root):
//   dotnet run --project scripts/RehashEmailHash -- --dry-run
//   dotnet run --project scripts/RehashEmailHash -- --yes
//
// Configure ConnectionStrings:DefaultConnection, PiiEncryption:*, and
// IdentifierHasher:SecretKey (the secret hashes should be rewritten under)
// via apps/portal appsettings / env vars before running.
//
// See scripts/RehashEmailHash/README.md.

using Microsoft.Extensions.DependencyInjection;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.Scripts.RehashEmailHash;

var dryRun = args.Any(a => string.Equals(a, "--dry-run", StringComparison.OrdinalIgnoreCase));
var skipConfirm = args.Any(a => string.Equals(a, "--yes", StringComparison.OrdinalIgnoreCase));

RehashEmailHashConsole.WriteBanner(dryRun);

if (!dryRun && !skipConfirm && !RehashEmailHashConsole.ConfirmApplyOrCancel())
{
    return;
}

try
{
    using var host = RehashEmailHashHost.Build(args);
    using var scope = host.Services.CreateScope();
    var result = await scope.ServiceProvider
        .GetRequiredService<EmailHashRehashService>()
        .ApplyAsync(dryRun);

    RehashEmailHashConsole.WriteResult(result);

    if (result.CollisionUserIds.Count > 0)
    {
        Environment.ExitCode = 2;
    }
}
catch (Exception ex)
{
    RehashEmailHashConsole.WriteError(ex);
    Environment.Exit(1);
}
