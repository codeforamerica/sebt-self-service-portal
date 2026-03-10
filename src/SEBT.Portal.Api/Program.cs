using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SEBT.Portal.Api.Composition;
using SEBT.Portal.Api.Models;
using Serilog;
using Microsoft.FeatureManagement;
using SEBT.Portal.Api.Middleware;
using SEBT.Portal.Api.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Configuration;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.Infrastructure.Seeding.Services;
using SEBT.Portal.UseCases;
using SEBT.Portal.Infrastructure;
using SEBT.Portal.Api.Startup;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Utilities;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

// Configuration provider priority order (later providers override earlier ones):
// 1. appsettings.json (defaults in FeatureManagement)
// 2. AWS AppConfig Agent (if configured, injects into FeatureManagement)
// 3. State-specific JSON (appsettings.{State}.json)

// Register AWS AppConfig Agent configuration provider if configured
// We'll be replacing this with a cloud-agnostic configuration provider in the future
// --> NOTE: This must be registered BEFORE state-specific config so state config can override agent values <--
var agentSection = builder.Configuration.GetSection("AppConfig:Agent");
if (agentSection.Exists())
{
    // Logger will be created after Serilog is configured, so pass null for now
    // The provider will work without logging
    builder.Configuration.AddAppConfigAgent("AppConfig:Agent", logger: null);
}

// This loads appsettings.{State}.json files (e.g., appsettings.dc.json, appsettings.co.json)
// State config loads LAST and is the final word on feature flag values if present
var state = Environment.GetEnvironmentVariable("STATE");
if (!string.IsNullOrEmpty(state))
{
    var stateConfigFile = $"appsettings.{state.ToLowerInvariant()}.json";
    builder.Configuration.AddJsonFile(stateConfigFile, optional: true, reloadOnChange: true);
}

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
}

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

// Use Serilog instead of default logger
builder.Host.UseSerilog();

// Registers plugins and allows them to be constructor injected into ASP.NET controllers
builder.Services.AddPlugins(builder.Configuration);

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
builder.Services.AddPortalInfrastructureServices();
builder.Services.AddPortalDbContext(builder.Configuration, options => options.ConfigureDevelopmentSeeding());
builder.Services.AddPortalInfrastructureRepositories(builder.Configuration);
builder.Services.AddPortalInfrastructureAppSettings(builder.Configuration);

// Register IDatabaseSeeder for development utilities (e.g., ClearSeededData script)
builder.Services.AddScoped<IDatabaseSeeder>(sp =>
{
    var dataSeeder = sp.GetRequiredService<IDataSeeder>();
    var logger = sp.GetService<ILogger<DatabaseSeeder>>();
    var timeProvider = sp.GetRequiredService<TimeProvider>();
    var seedingSettings = sp.GetService<IOptions<SeedingSettings>>()?.Value ?? new SeedingSettings();
    return new DatabaseSeeder(dataSeeder, seedingSettings, logger, timeProvider);
});

// Configure JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer();

// Configure JWT Bearer options using IOptions<JwtSettings> pattern
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .PostConfigure<IOptions<JwtSettings>>((options, jwtSettingsOptions) =>
    {
        var jwtSettings = jwtSettingsOptions.Value;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
            ClockSkew = TimeSpan.FromMinutes(2),
            NameClaimType = "sub"
        };
        // Preserve JWT claim names (sub, email) so we can read them regardless of handler mapping.
        options.MapInboundClaims = false;
    });

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString();
        }

        var rateLimitSettings = context.HttpContext.RequestServices
            .GetRequiredService<IOptionsMonitor<OtpRateLimitSettings>>()
            .CurrentValue;

        var windowDescription = rateLimitSettings.WindowMinutes == 1.0
            ? "minute"
            : $"{rateLimitSettings.WindowMinutes} minutes";

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { Error = $"Rate limit exceeded. Maximum {rateLimitSettings.PermitLimit} OTP requests per {windowDescription} allowed." },
            cancellationToken);
    };

    // Add fixed window limiter policy for OTP requests with email-based partitioning
    options.AddPolicy("otp-policy", httpContext =>
    {
        var rateLimitOptions = httpContext.RequestServices
            .GetRequiredService<IOptionsMonitor<OtpRateLimitSettings>>()
            .CurrentValue;

        // Try to get email from HttpContext.Items (set by OtpRateLimitMiddleware)
        if (httpContext.Items.TryGetValue("RateLimitEmail", out var emailObj) &&
            emailObj is string email && !string.IsNullOrEmpty(email))
        {
            // Partition by email address
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: email,
                factory: _ => CreateOtpRateLimitOptions(rateLimitOptions));
        }

        // If email not found, use IP address as fallback
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ipAddress,
            factory: _ => CreateOtpRateLimitOptions(rateLimitOptions));
    });
});

static FixedWindowRateLimiterOptions CreateOtpRateLimitOptions(OtpRateLimitSettings settings) => new()
{
    PermitLimit = settings.PermitLimit,
    Window = TimeSpan.FromMinutes(settings.WindowMinutes),
    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
    QueueLimit = 0,
    AutoReplenishment = true
};

var app = builder.Build();

// Guard against default/placeholder IdentifierHasher key in production
if (app.Environment.IsProduction())
{
    IdentifierHasherGuard.ValidateForProduction(app.Configuration["IdentifierHasher:SecretKey"]);
}

// Apply database migrations (non-blocking: app will start even if DB is unavailable)
try
{
    await using var scope = app.Services.CreateAsyncScope();
    var databaseMigrator = scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>();
    await databaseMigrator.MigrateAsync();

    var seedingSettings = app.Configuration.GetSection(SeedingSettings.SectionName).Get<SeedingSettings>();
    if (app.Environment.IsDevelopment() || seedingSettings?.Enabled == true)
    {
        var useMockHouseholdData = app.Configuration.GetValue<bool>("UseMockHouseholdData", false);
        var databaseSeeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
        await databaseSeeder.SeedTestUsersAsync(useMockHouseholdData, CancellationToken.None);
    }
    Log.Information("Database migrations completed successfully");
}
catch (Exception ex)
{
    Log.Warning(ex, "Database migrations failed or database unavailable. App will continue to start.");
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection();
}

app.UseRouting();

app.UseMiddleware<OtpRateLimitMiddleware>();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
});

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

// Makes the implicit Program class public so WebApplicationFactory<Program> can reference it from test projects.
#pragma warning disable CS1591
public partial class Program { }
#pragma warning restore CS1591
