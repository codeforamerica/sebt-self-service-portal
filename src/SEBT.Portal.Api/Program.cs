using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Serilog;
using SEBT.Portal.Api.Middleware;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.UseCases;
using SEBT.Portal.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

// Use Serilog instead of default logger
builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.Configure<RouteOptions>(options =>
{
    options.LowercaseUrls = true;
    options.LowercaseQueryStrings = true;
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFilename = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
});

// Adds use cases (i.e., query and command handlers) for portal business logic
builder.Services.AddUseCases();
builder.Services.AddPortalInfrastructureServices();
builder.Services.AddPortalInfrastructureRepositories();
builder.Services.AddPortalInfrastructureAppSettings();

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

app.UseAuthorization();

app.MapControllers();

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
