using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Threading.RateLimiting;
using System.Data.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SEBT.Portal.Api;
using SEBT.Portal.Api.Composition;
using SEBT.Portal.Api.Filters;
using SEBT.Portal.Api.Telemetry;
using Serilog;
using Microsoft.FeatureManagement;
using SEBT.Portal.Api.Middleware;
using SEBT.Portal.Api.Options;
using SEBT.Portal.Api.Services;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Exceptions;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Configuration;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.Infrastructure.Seeding.Services;
using SEBT.Portal.UseCases;
using SEBT.Portal.UseCases.Auth.SessionLifetime;
using SEBT.Portal.Infrastructure;
using SEBT.Portal.Api.Startup;
using SEBT.Portal.Api.Startup.Guards;
using SEBT.Portal.Api.Startup.Setup;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog early so that configuration providers can log.
// Console sink is configured in code (not appsettings) so we can use
// human-readable text locally and structured JSON in deployed environments.
// Field names match Datadog's reserved attributes (`date`, `status`,
// `message`) so they are auto-recognized without configuring a per-service
// log pipeline. Without these names the Forwarder Lambda falls back to the
// CloudWatch event time for the timeline and tags the log with
// `service:cloudwatch`. The literal service value must match the OTEL
// ServiceName constant in OpenTelemetrySetup so traces and logs correlate
// under the same service in Datadog.
// Set LOG_FORMAT=json in ECS task definitions to enable structured output.
var useJsonLogs = string.Equals(
    Environment.GetEnvironmentVariable("LOG_FORMAT"), "json", StringComparison.OrdinalIgnoreCase);

var bootstrapConfig = new LoggerConfiguration();
SerilogSetup.Configure(bootstrapConfig, builder.Configuration, useJsonLogs);

// CreateLogger (not CreateBootstrapLogger): WebApplicationFactory builds multiple hosts in
// one process; a bootstrap/reloadable logger freezes on the first host and throws
// "The logger is already frozen" on the next. UseSerilog below replaces Log.Logger with a
// fresh config from SerilogSetup, so Console / LOG_FORMAT stay identical.
Log.Logger = bootstrapConfig.CreateLogger();

// writeToProviders forwards events to MEL providers (including OTLP). Enable only when OTLP
// log export is on; otherwise behavior matches a plain UseSerilog(). Clear default MEL
// providers *before* UseSerilog so we do not strip SerilogLoggerProvider (needed for
// ILogger<T> → Serilog → Console), while still avoiding duplicate stdout from the
// framework Console logger when writeToProviders is on.
var otlpLogExportEnabled = OpenTelemetrySetup.IsOtlpLogExportEnabled(builder.Configuration);
if (otlpLogExportEnabled)
{
    OpenTelemetrySetup.ClearDefaultLoggerProvidersForOtlp(builder);
}

builder.Host.UseSerilog(
    (context, configuration) => SerilogSetup.Configure(configuration, context.Configuration, useJsonLogs),
    writeToProviders: otlpLogExportEnabled);
builder.SetupOpenTelemetry();

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

builder.AddPortalConfigurationSources();

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

// Caching must be registered before plugins — plugins may depend on HybridCache
builder.Services.AddCaching(builder.Configuration, builder.Environment);
builder.Services.AddDistributedLocking(builder.Configuration, builder.Environment);

// Registers plugins and allows them to be constructor injected into ASP.NET controllers
builder.Services.AddPlugins(builder.Configuration, builder.Environment.ContentRootPath);

// Add services to the container.
builder.Services.AddControllers();

builder.Services.Configure<RouteOptions>(options =>
{
    options.LowercaseUrls = true;
    options.LowercaseQueryStrings = true;
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.ConfigureOptions<ConfigureSwaggerGenOptions>();
builder.Services.AddSwaggerGen(); // Configured by ConfigureSwaggerGenOptions, which delegates to the state plugin

// Add Feature Management
builder.Services.AddFeatureManagement(builder.Configuration.GetSection("FeatureManagement"));

// Adds use cases (i.e., query and command handlers) for portal business logic
builder.Services.AddUseCases();
builder.Services.AddPortalInfrastructureServices(builder.Configuration);
builder.Services.AddPortalDbContext(builder.Configuration, options => options.ConfigureDevelopmentSeeding());
builder.Services.AddPortalDbHealthCheck(builder.Configuration);
builder.Services.AddPortalInfrastructureRepositories(builder.Configuration);
builder.Services.AddPortalInfrastructureAppSettings(builder.Configuration);

// Action filters
builder.Services.AddScoped<ResolveUserFilter>();

// OIDC token exchange (replaces the Next.js /api/auth/oidc/callback route)
builder.Services.AddScoped<IOidcExchangeService, OidcExchangeService>();
builder.Services.AddScoped<IOidcCallbackFailureLogger, OidcCallbackFailureLogger>();
// pre-auth session store (HybridCache-backed, 15 min TTL)
builder.Services.AddSingleton<IPreAuthSessionStore, PreAuthSessionStore>();
builder.Services.AddSingleton<ITokenDenylist, TokenDenylist>();

// Register IDatabaseSeeder for development utilities (e.g., ClearSeededData script)
builder.Services.AddScoped<IDatabaseSeeder>(sp =>
{
    var dataSeeder = sp.GetRequiredService<IDataSeeder>();
    var logger = sp.GetService<ILogger<DatabaseSeeder>>();
    var timeProvider = sp.GetRequiredService<TimeProvider>();
    var seedingSettings = sp.GetService<IOptions<SeedingSettings>>()?.Value ?? new SeedingSettings();
    return new DatabaseSeeder(dataSeeder, seedingSettings, logger, timeProvider);
});

builder.Services.AddPortalAuthentication(builder.Configuration);

// Development-only phone override: when set, overrides JWT phone for household lookup
builder.Services.AddOptions<DevelopmentPhoneOverrideOptions>()
    .BindConfiguration(DevelopmentPhoneOverrideOptions.SectionName);
builder.Services.AddSingleton<IPhoneOverrideProvider>(sp =>
{
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    var options = sp.GetRequiredService<IOptions<DevelopmentPhoneOverrideOptions>>().Value;
    if (env.IsDevelopment() && !string.IsNullOrWhiteSpace(options.Phone))
    {
        return sp.GetRequiredService<DevelopmentPhoneOverrideProvider>();
    }
    return NullPhoneOverrideProvider.Instance;
});
builder.Services.AddSingleton<DevelopmentPhoneOverrideProvider>();

builder.Services.AddPortalRateLimiting();

var app = builder.Build();

// Guard against default/placeholder IdentifierHasher key in production
if (app.Environment.IsProduction())
{
    IdentifierHasherGuard.ValidateForProduction(app.Configuration["IdentifierHasher:SecretKey"]);

    var piiEncryptionSettings = app.Configuration.GetSection(PiiEncryptionSettings.SectionName)
        .Get<PiiEncryptionSettings>();
    PiiEncryptionGuard.ValidateForProduction(piiEncryptionSettings);
}

// HMAC-SHA256 requires ≥256-bit (32-byte) key. Fail fast if configured but too short.
var oidcSigningKey = app.Configuration["Oidc:CompleteLoginSigningKey"];
if (!string.IsNullOrEmpty(oidcSigningKey) && oidcSigningKey.Length < 32)
{
    throw new InvalidOperationException(
        $"Oidc:CompleteLoginSigningKey must be at least 32 characters (got {oidcSigningKey.Length}). " +
        "HMAC-SHA256 requires a 256-bit key for full security.");
}

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

// Configure the HTTP request pipeline.
app.UsePortalRequestPipeline();

try
{
    var resolvedHouseholdIdTypes = app.Configuration
        .GetSection("StateHouseholdId:PreferredHouseholdIdTypes")
        .GetChildren()
        .Select(c => c.Value)
        .ToList();
    Log.Information(
        "Resolved StateHouseholdId:PreferredHouseholdIdTypes: [{Types}]",
        string.Join(", ", resolvedHouseholdIdTypes));

    Log.Information("SEBT Portal API started successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "SEBT Portal API terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>
/// Required for WebApplicationFactory&lt;Program&gt; in integration tests.
/// Top-level statements generate an implicit internal Program class;
/// this partial declaration makes it public so the test assembly can reference it.
/// </summary>
public partial class Program { }
