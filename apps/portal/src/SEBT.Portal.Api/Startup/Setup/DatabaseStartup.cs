using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Services;
using Serilog;

namespace SEBT.Portal.Api.Startup.Setup;

internal static class DatabaseStartup
{
    public static async Task MigrateAndSeedDatabaseAsync(this WebApplication app)
    {
        // Apply database migrations (non-blocking: app will start even if DB is unavailable)
        try
        {
            await using var scope = app.Services.CreateAsyncScope();
            var databaseMigrator = scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>();
            await databaseMigrator.MigrateAsync();

            var piiEncryptionOptions = app.Configuration.GetSection(PiiEncryptionSettings.SectionName)
                .Get<PiiEncryptionSettings>() ?? new PiiEncryptionSettings();
            var piiBackfillLogger = scope.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(nameof(PiiEncryptionStartupBackfill));
            await PiiEncryptionStartupBackfill.RunIfEnabledAsync(
                piiEncryptionOptions,
                async ct =>
                {
                    var piiBackfill = scope.ServiceProvider.GetRequiredService<PiiPlaintextEncryptionBackfill>();
                    await piiBackfill.ApplyAsync(ct);
                },
                piiBackfillLogger,
                CancellationToken.None);

            var seedingSettings = app.Configuration.GetSection(SeedingSettings.SectionName).Get<SeedingSettings>();
            if (app.Environment.IsDevelopment() || seedingSettings?.Enabled == true)
            {
                var useMockHouseholdData = app.Configuration.GetValue<bool>("UseMockHouseholdData", false);
                var databaseSeeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();

                // In Development, clear stale seed data before re-seeding so that scenario
                // definition changes (e.g. IAL levels) are always reflected in the database.
                if (app.Environment.IsDevelopment())
                {
                    await databaseSeeder.ClearSeededDataAsync(CancellationToken.None);
                }

                await databaseSeeder.SeedTestUsersAsync(useMockHouseholdData, CancellationToken.None);
            }
            Log.Information("Database migrations completed successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Database migrations failed or database unavailable. App will continue to start.");
        }
    }
}
