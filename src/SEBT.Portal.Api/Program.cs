using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SEBT.Portal.Api;
using SEBT.Portal.Api.Composition;
using SEBT.Portal.Api.Filters;
using SEBT.Portal.Api.Models;
using Serilog;
using Serilog.Templates;
using Microsoft.FeatureManagement;
using SEBT.Portal.Api.Middleware;
using SEBT.Portal.Api.Options;
using SEBT.Portal.Api.Services;
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

// Configure Serilog early so that configuration providers can log.
// Console sink is configured in code (not appsettings) so we can use
// human-readable text locally and structured JSON in deployed environments.
// The JSON format uses field names that Datadog auto-recognizes (level,
// message, timestamp) so log severity maps correctly without custom pipelines.
// Set LOG_FORMAT=json in ECS task definitions to enable structured output.
var useJsonLogs = string.Equals(
    Environment.GetEnvironmentVariable("LOG_FORMAT"), "json", StringComparison.OrdinalIgnoreCase);

var logConfig = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext();

if (useJsonLogs)
{
    logConfig.WriteTo.Console(new ExpressionTemplate(
        "{ {timestamp: @t, level: @l, message: @m, exception: @x, ..@p} }\n"));
}
else
{
    logConfig.WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
}

Log.Logger = logConfig.CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

// Configuration provider priority order (later providers override earlier ones):
// 1. appsettings.json (defaults)
// 2. State-specific JSON (appsettings.{State}.json)
// 3. AWS AppConfig Agent (if configured — highest priority, overrides all)

// This loads appsettings.{State}.json files (e.g., appsettings.dc.json, appsettings.co.json)
var state = Environment.GetEnvironmentVariable("STATE");
if (!string.IsNullOrEmpty(state))
{
    var stateConfigFile = $"appsettings.{state.ToLowerInvariant()}.json";
    builder.Configuration.AddJsonFile(stateConfigFile, optional: true, reloadOnChange: true);
}

// Register AWS AppConfig Agent configuration providers if configured.
// Registered last so AppConfig values take highest priority.
var agentSection = builder.Configuration.GetSection("AppConfig:Agent");
var applicationId = agentSection["ApplicationId"];
var environmentId = agentSection["EnvironmentId"];

if (!string.IsNullOrEmpty(applicationId) && !string.IsNullOrEmpty(environmentId))
{
    var baseUrl = agentSection["BaseUrl"] ?? "http://localhost:2772";
    var reloadAfterSeconds = agentSection.GetValue<int?>("ReloadAfterSeconds") ?? 90;

    using var loggerFactory = LoggerFactory.Create(lb => lb.AddSerilog());
    var appConfigLogger = loggerFactory.CreateLogger<AppConfigAgentConfigurationProvider>();

    var featureFlagsProfileId = builder.Configuration["AppConfig:FeatureFlags:ProfileId"];
    if (!string.IsNullOrEmpty(featureFlagsProfileId))
    {
        builder.Configuration.AddAppConfigAgent(
            baseUrl, applicationId, environmentId, featureFlagsProfileId,
            reloadAfterSeconds, isFeatureFlag: true, logger: appConfigLogger);
    }

    var appSettingsProfileId = builder.Configuration["AppConfig:AppSettings:ProfileId"];
    if (!string.IsNullOrEmpty(appSettingsProfileId))
    {
        builder.Configuration.AddAppConfigAgent(
            baseUrl, applicationId, environmentId, appSettingsProfileId,
            reloadAfterSeconds, isFeatureFlag: false, logger: appConfigLogger);
    }
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

// Caching must be registered before plugins — plugins may depend on HybridCache
builder.Services.AddCaching(builder.Configuration);

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
builder.Services.AddPortalInfrastructureRepositories(builder.Configuration);
builder.Services.AddPortalInfrastructureAppSettings(builder.Configuration);

// Action filters
builder.Services.AddScoped<ResolveUserFilter>();

// OIDC token exchange (replaces the Next.js /api/auth/oidc/callback route)
builder.Services.AddScoped<IOidcExchangeService, OidcExchangeService>();
// pre-auth session store (HybridCache-backed, 15 min TTL)
builder.Services.AddSingleton<IPreAuthSessionStore, PreAuthSessionStore>();

// Register IDatabaseSeeder for development utilities (e.g., ClearSeededData script)
builder.Services.AddScoped<IDatabaseSeeder>(sp =>
{
    var dataSeeder = sp.GetRequiredService<IDataSeeder>();
    var logger = sp.GetService<ILogger<DatabaseSeeder>>();
    var timeProvider = sp.GetRequiredService<TimeProvider>();
    var seedingSettings = sp.GetService<IOptions<SeedingSettings>>()?.Value ?? new SeedingSettings();
    return new DatabaseSeeder(dataSeeder, seedingSettings, logger, timeProvider);
});

// State allowlist for OIDC login endpoints. An instance is considered a
// configured OIDC tenant if its loaded config overlay has Oidc:DiscoveryEndpoint
// set. The current STATE env var is the only allowed state when that's true;
// everything else (no STATE, no Oidc block) produces an empty allowlist and all
// OIDC routes reject all stateCode inputs. This prevents the route parameter
// from being used as a tenant escape.
var allowedOidcStates = new List<string>();
if (!string.IsNullOrWhiteSpace(builder.Configuration["Oidc:DiscoveryEndpoint"]))
{
    var currentState = Environment.GetEnvironmentVariable("STATE");
    if (!string.IsNullOrWhiteSpace(currentState))
        allowedOidcStates.Add(currentState);
}
builder.Services.AddSingleton<IStateAllowlist>(new StateAllowlist(allowedOidcStates));

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

        // DC-242: portal session JWT lives in an HttpOnly cookie. Fall back to the cookie
        // when no Authorization header is present so the SPA never handles the raw token.
        // The Authorization header path is preserved for service-to-service callers.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (string.IsNullOrEmpty(context.Token))
                {
                    var cookieToken = context.Request.Cookies[AuthCookies.AuthCookieName];
                    if (!string.IsNullOrEmpty(cookieToken))
                    {
                        context.Token = cookieToken;
                    }
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

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

builder.Services.AddRateLimiter(options =>
{
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString();
        }

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        // Determine which rate-limit policy rejected the request to show an appropriate message
        var endpoint = context.HttpContext.GetEndpoint();
        var rateLimitAttribute = endpoint?.Metadata
            .OfType<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>()
            .FirstOrDefault();

        if (rateLimitAttribute?.PolicyName == RateLimitPolicies.EnrollmentCheck)
        {
            var enrollmentSettings = context.HttpContext.RequestServices
                .GetRequiredService<IOptionsMonitor<EnrollmentCheckRateLimitSettings>>()
                .CurrentValue;
            var windowDescription = enrollmentSettings.WindowMinutes == 1.0
                ? "minute"
                : $"{enrollmentSettings.WindowMinutes} minutes";
            await context.HttpContext.Response.WriteAsJsonAsync(
                new { Error = $"Rate limit exceeded. Maximum {enrollmentSettings.PermitLimit} enrollment checks per {windowDescription} allowed." },
                cancellationToken);
        }
        else if (rateLimitAttribute?.PolicyName == RateLimitPolicies.Webhook)
        {
            // Webhook callers (Socure) don't need a friendly message, but log for observability
            await context.HttpContext.Response.WriteAsJsonAsync(
                new { Error = "Rate limit exceeded." },
                cancellationToken);
        }
        else
        {
            var otpSettings = context.HttpContext.RequestServices
                .GetRequiredService<IOptionsMonitor<OtpRateLimitSettings>>()
                .CurrentValue;
            var windowDescription = otpSettings.WindowMinutes == 1.0
                ? "minute"
                : $"{otpSettings.WindowMinutes} minutes";
            await context.HttpContext.Response.WriteAsJsonAsync(
                new { Error = $"Rate limit exceeded. Maximum {otpSettings.PermitLimit} OTP requests per {windowDescription} allowed." },
                cancellationToken);
        }
    };

    // Add fixed window limiter policy for OTP requests with email-based partitioning
    options.AddPolicy(RateLimitPolicies.Otp, httpContext =>
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

    // Add fixed window limiter policy for enrollment check requests with IP-based partitioning
    options.AddPolicy(RateLimitPolicies.EnrollmentCheck, httpContext =>
    {
        var rateLimitOptions = httpContext.RequestServices
            .GetRequiredService<IOptionsMonitor<EnrollmentCheckRateLimitSettings>>()
            .CurrentValue;

        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"enrollment-check:{ipAddress}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitOptions.PermitLimit,
                Window = TimeSpan.FromMinutes(rateLimitOptions.WindowMinutes),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });

    // Add fixed window limiter policy for Socure webhook endpoint with IP-based partitioning
    // TODO: Confirm appropriate thresholds with Socure and the team
    options.AddPolicy(RateLimitPolicies.Webhook, httpContext =>
    {
        var webhookRateLimitOptions = httpContext.RequestServices
            .GetRequiredService<IOptionsMonitor<WebhookRateLimitSettings>>()
            .CurrentValue;

        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"webhook:{ipAddress}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = webhookRateLimitOptions.PermitLimit,
                Window = TimeSpan.FromMinutes(webhookRateLimitOptions.WindowMinutes),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = true
            });
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

// Map X-Forwarded-For to HttpContext.Connection.RemoteIpAddress so that
// IP-based rate limiting identifies distinct clients correctly.
//
// In production the .NET API runs on a private network behind the Next.js
// server, which proxies all requests and forwards the real client IP via
// X-Forwarded-For. Without this middleware every request appears to come
// from the Next.js server's single private IP, collapsing all clients into
// one rate-limit bucket.
//
// Current configuration uses open trust (cleared KnownProxies/KnownIPNetworks)
// which is acceptable because the API is not directly reachable from the
// public internet. ForwardLimit = 1 ensures only the last proxy hop is read,
// preventing clients from prepending fake entries.
//
// TODO: For defense-in-depth, consider restricting trust to the VPC CIDR:
//   options.KnownIPNetworks.Add(IPNetwork.Parse("10.0.0.0/8"));
// This would reject forwarded headers from any source outside the private
// network, guarding against future topology changes that might expose the API.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor,
    ForwardLimit = 1,
};
// Open trust: accept X-Forwarded-For from any source. Safe here because
// the API is on a private network with no public ingress. Clear the defaults
// (loopback) so the middleware processes headers from all sources.
forwardedHeadersOptions.KnownProxies.Clear();
forwardedHeadersOptions.KnownIPNetworks.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

app.UseRouting();

// reject OIDC POST requests with missing or disallowed Origin header.
// Runs before authentication so replay attempts from rogue origins fail early.
app.UseMiddleware<OidcOriginValidationMiddleware>();

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
