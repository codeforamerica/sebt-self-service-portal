using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Scripts.RehashEmailHash;

internal static class RehashEmailHashHost
{
    public static IHost Build(string[] args)
    {
        var configuration = LoadConfiguration();

        return Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((_, config) =>
            {
                config.Sources.Clear();
                config.AddConfiguration(configuration);
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddSimpleConsole(o =>
                {
                    o.SingleLine = true;
                    o.TimestampFormat = "HH:mm:ss ";
                });
            })
            .ConfigureServices((_, services) => RegisterServices(services, configuration))
            .Build();
    }

    private static void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Minimal DI: avoid full API option validation (JWT, SMTP, and similar).
        services.AddOptions<PiiEncryptionSettings>()
            .Bind(configuration.GetSection(PiiEncryptionSettings.SectionName));
        services.AddOptions<IdentifierHasherSettings>()
            .Bind(configuration.GetSection(IdentifierHasherSettings.SectionName));

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        services.AddDbContext<PortalDbContext>(options => options.UseSqlServer(connectionString));

        services.AddSingleton<IPiiSymmetricEncryption>(sp =>
            PiiSymmetricEncryptionFactory.Create(sp.GetRequiredService<IOptions<PiiEncryptionSettings>>()));
        services.AddSingleton<IEmailLookupHasher, EmailLookupHasher>();
        services.AddTransient<EmailHashRehashService>();
    }

    private static IConfigurationRoot LoadConfiguration()
    {
        var projectRoot = ResolveProjectRoot();
        var apiDir = Path.Combine(projectRoot, "apps", "portal", "src", "SEBT.Portal.Api");

        return new ConfigurationBuilder()
            .SetBasePath(apiDir)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddJsonFile("appsettings.dc.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private static string ResolveProjectRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var marker = Path.Combine(dir.FullName, "SEBT.slnx");
            if (File.Exists(marker))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Could not find repo root (SEBT.slnx). Run from the repository root or scripts/RehashEmailHash.");
    }
}
