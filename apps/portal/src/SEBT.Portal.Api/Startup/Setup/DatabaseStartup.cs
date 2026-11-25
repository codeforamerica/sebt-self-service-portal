using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Configuration;
using SEBT.Portal.Infrastructure.Services;
using Serilog;

namespace SEBT.Portal.Api.Startup.Setup;

internal static class DatabaseStartup
{
    public static async Task MigrateAndSeedDatabaseAsync(this WebApplication app)
    {
        var seedingSettings = app.Configuration.GetSection(SeedingSettings.SectionName).Get<SeedingSettings>();
        // Evaluated outside the try so a misconfigured Production start fails instead of being swallowed.
        var dataReset = SeededDataResetPolicy.Decide(seedingSettings, app.Environment);

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

            if (app.Environment.IsDevelopment() || seedingSettings?.Enabled == true)
            {
                var seedLogger = scope.ServiceProvider
                    .GetService<ILoggerFactory>()
                    ?.CreateLogger(nameof(HouseholdMockGate));
                var useMockHouseholdData = HouseholdMockGate.IsEnabled(
                    app.Configuration,
                    app.Environment,
                    seedLogger);
                var databaseSeeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();

                // Development clears so scenario changes (e.g. IAL levels) always reach the database;
                // a demo clears everyone so each start is a fresh demo.
                if (dataReset == SeededDataReset.AllUsers)
                {
                    await databaseSeeder.ClearAllUserDataAsync(CancellationToken.None);
                }
                else if (dataReset == SeededDataReset.SeededUsers)
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
