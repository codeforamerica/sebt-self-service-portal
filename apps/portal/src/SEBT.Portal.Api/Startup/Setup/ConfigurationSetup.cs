using SEBT.Portal.Infrastructure.Configuration;
using Serilog;

namespace SEBT.Portal.Api.Startup.Setup;

internal static class ConfigurationSetup
{
    public static WebApplicationBuilder AddPortalConfigurationSources(this WebApplicationBuilder builder)
    {
        // Configuration provider priority order (later providers override earlier ones):
        // 1. appsettings.json / appsettings.{Environment}.json (defaults)
        // 2. User secrets (Development only)
        // 3. State-specific JSON (appsettings.{State}.json)
        // 4. Environment variables
        // 5. Command-line args
        // 6. AWS AppConfig Agent (if configured — highest priority, overrides all)
        //
        // WebApplication.CreateBuilder registers 1–2 and 4–5; AddStateOverlay inserts
        // the state JSON below the environment variable provider so env vars keep
        // their standard twelve-factor precedence over config files.

        // This loads appsettings.{State}.json files (e.g., appsettings.dc.json, appsettings.co.json)
        var state = Environment.GetEnvironmentVariable("STATE");
        if (!string.IsNullOrEmpty(state))
        {
            Log.Logger.Information("Loading state-specific config: {State}", state);
            builder.Configuration.AddStateOverlay(state);
        }

        // Register AWS AppConfig Agent configuration providers if configured.
        // Registered last so AppConfig values take highest priority.
        var agentSection = builder.Configuration.GetSection("AppConfig:Agent");
        var applicationId = agentSection["ApplicationId"];
        var environmentId = agentSection["EnvironmentId"];

        if (!string.IsNullOrEmpty(applicationId) && !string.IsNullOrEmpty(environmentId))
        {
            var baseUrl = agentSection["BaseUrl"] ?? "http://localhost:2772";

            var loggerFactory = LoggerFactory.Create(lb => lb.AddSerilog());
            var appConfigLogger = loggerFactory.CreateLogger<AppConfigAgentConfigurationProvider>();

            var featureFlagsProfileId = builder.Configuration["AppConfig:FeatureFlags:ProfileId"];
            if (!string.IsNullOrEmpty(featureFlagsProfileId))
            {
                builder.Configuration.AddAppConfigAgent(
                    baseUrl, applicationId, environmentId, featureFlagsProfileId,
                    isFeatureFlag: true, logger: appConfigLogger);
            }

            var appSettingsProfileId = builder.Configuration["AppConfig:AppSettings:ProfileId"];
            if (!string.IsNullOrEmpty(appSettingsProfileId))
            {
                builder.Configuration.AddAppConfigAgent(
                    baseUrl, applicationId, environmentId, appSettingsProfileId,
                    isFeatureFlag: false, logger: appConfigLogger);
            }

            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddHostedService<AppConfigAgentReloadService>();
        }

        return builder;
    }

    public static WebApplicationBuilder AddDatabaseConnectionStringsFromEnvironment(
        this WebApplicationBuilder builder)
    {
        // Build database connection string from environment variables when deployed
        // to ECS. Credentials are injected from Secrets Manager at container startup.
        var dbHost = Environment.GetEnvironmentVariable("DB_HOST");
        var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");
        if (!string.IsNullOrEmpty(dbHost) && !string.IsNullOrEmpty(dbPassword))
        {
            var dbPort = Environment.GetEnvironmentVariable("DB_PORT") ?? "1433";
            var dbName = Environment.GetEnvironmentVariable("DB_NAME") ?? "SebtPortal";
            var dbUser = Environment.GetEnvironmentVariable("DB_USER") ?? "admin";
            builder.Configuration["ConnectionStrings:DefaultConnection"] =
                $"Server={dbHost},{dbPort};Database={dbName};User Id={dbUser};Password={dbPassword};Encrypt=True;TrustServerCertificate=True;";

            var dcSourceDbName = Environment.GetEnvironmentVariable("DC_SOURCE_DB_NAME");
            if (!string.IsNullOrEmpty(dcSourceDbName))
            {
                builder.Configuration["DCConnector:ConnectionString"] =
                    $"Server={dbHost},{dbPort};Database={dcSourceDbName};User Id={dbUser};Password={dbPassword};Encrypt=True;TrustServerCertificate=True;";
            }
        }

        return builder;
    }
}
