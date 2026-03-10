using SEBT.Portal.StatesPlugins.Interfaces;

namespace SEBT.Portal.Api.Composition;

/// <summary>
/// Registers MEF plugins via deferred factory delegates. Plugin assemblies
/// are loaded lazily by <see cref="PluginLoader"/> on first DI resolution,
/// when IConfiguration is fully assembled (including test overrides).
/// </summary>
internal static class ServiceCollectionPluginExtensions
{
    public static IServiceCollection AddPlugins(this IServiceCollection services)
    {
        services.AddSingleton<PluginLoader>();

        services.AddSingleton<IStateAuthenticationService>(sp =>
            sp.GetRequiredService<PluginLoader>()
                .GetExport<IStateAuthenticationService>()
            ?? new Defaults.DefaultIStateAuthenticationService());

        services.AddSingleton<ISummerEbtCaseService>(sp =>
            sp.GetRequiredService<PluginLoader>()
                .GetExport<ISummerEbtCaseService>()
            ?? new Defaults.DefaultSummerEbtCaseService());

        services.AddSingleton<IEnrollmentCheckService>(sp =>
            sp.GetRequiredService<PluginLoader>()
                .GetExport<IEnrollmentCheckService>()
            ?? new Defaults.DefaultEnrollmentCheckService());

        return services;
    }
}
