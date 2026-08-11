using Microsoft.Extensions.Options;
using SEBT.Portal.Api.Composition;
using SEBT.Portal.Api.Filters;
using Serilog;
using Microsoft.FeatureManagement;
using SEBT.Portal.Api.Options;
using SEBT.Portal.Api.Services;
using SEBT.Portal.Api.Telemetry;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Seeding.Services;
using SEBT.Portal.UseCases;
using SEBT.Portal.Infrastructure;
using SEBT.Portal.Api.Startup;
using SEBT.Portal.Api.Startup.Guards;
using SEBT.Portal.Api.Startup.Setup;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog early so that configuration providers can log.
builder.SetupSerilog();
builder.SetupOpenTelemetry();

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

builder.AddPortalConfigurationSources();
builder.AddDatabaseConnectionStringsFromEnvironment();

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

builder.Services.AddOidcServices();

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

await app.MigrateAndSeedDatabaseAsync();

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
