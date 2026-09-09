using SEBT.Portal.Api.Composition;
using SEBT.Portal.Api.Filters;
using Serilog;
using Microsoft.FeatureManagement;
using SEBT.Portal.Api.Options;
using SEBT.Portal.Api.Telemetry;
using SEBT.Portal.UseCases;
using SEBT.Portal.Infrastructure;
using SEBT.Portal.Api.Startup;
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

// Registers plugins and allows them to be constructor injected into ASP.NET controllers
builder.Services.AddPlugins(builder.Configuration, builder.Environment.ContentRootPath);

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
builder.Services.AddPortalInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddPortalDbHealthCheck(builder.Configuration);

builder.Services.AddScoped<ResolveUserFilter>();

builder.Services.AddOidcServices();
builder.Services.AddPortalAuthentication(builder.Configuration);
builder.Services.AddPortalRateLimiting();

builder.Services.AddDatabaseSeeder();
builder.Services.AddDevelopmentOverrides();

var app = builder.Build();

await app.MigrateAndSeedDatabaseAsync();

// Configure the HTTP request pipeline.
app.UsePortalRequestPipeline();

try
{
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
