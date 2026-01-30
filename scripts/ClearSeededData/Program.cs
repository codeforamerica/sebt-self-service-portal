// Simple console application to clear seeded data
// 
// To use this script:
// run dotnet run --project scripts/ClearSeededData within the base of the project

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure;
using SEBT.Portal.Infrastructure.Seeding.Services;

Console.WriteLine("Clearing seeded data from database...");
Console.WriteLine("WARNING: This will delete records!");
Console.Write("Are you sure you want to continue? (yes/no): ");
var confirmation = Console.ReadLine();

if (confirmation?.ToLowerInvariant() != "yes")
{
    Console.WriteLine("Operation cancelled.");
    return;
}

try
{
    // Build configuration
    // Find the project root (two levels up from scripts/ClearSeededData)
    var currentDir = Directory.GetCurrentDirectory();
    var projectRoot = currentDir.Contains("ClearSeededData") 
        ? Path.Combine(currentDir, "..", "..")
        : currentDir;
    
    var appsettingsPath = Path.Combine(projectRoot, "src", "SEBT.Portal.Api", "appsettings.json");
    var appsettingsDevPath = Path.Combine(projectRoot, "src", "SEBT.Portal.Api", "appsettings.Development.json");
    
    var configuration = new ConfigurationBuilder()
        .SetBasePath(projectRoot)
        .AddJsonFile(appsettingsPath, optional: false)
        .AddJsonFile(appsettingsDevPath, optional: true)
        .AddEnvironmentVariables()
        .Build();

    // Build host with services
    var host = Host.CreateDefaultBuilder()
        .ConfigureServices((context, services) =>
        {
            services.AddPortalDbContext(configuration);
            services.AddPortalInfrastructureRepositories();
            // Register IDatabaseSeeder from Seeding project
            services.AddScoped<IDatabaseSeeder>(sp =>
            {
                var dataSeeder = sp.GetRequiredService<IDataSeeder>();
                return new DatabaseSeeder(dataSeeder);
            });
        })
        .Build();

    // Get seeder service
    using var scope = host.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();

    // Clear seeded data
    await seeder.ClearSeededDataAsync();

    Console.WriteLine("Seeded data cleared successfully!");
}
catch (Exception ex)
{
    Console.WriteLine($"Error clearing seeded data: {ex.Message}");
    Console.WriteLine($"  {ex.GetType().Name}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"  Inner: {ex.InnerException.Message}");
    }
    Environment.Exit(1);
}
