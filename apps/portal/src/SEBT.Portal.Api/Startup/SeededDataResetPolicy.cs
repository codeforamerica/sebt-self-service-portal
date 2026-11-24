using Microsoft.Extensions.Hosting;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Api.Startup;

/// <summary>
/// Which data, if any, startup clears before re-seeding.
/// </summary>
public enum SeededDataReset
{
    /// <summary>Leave existing data alone.</summary>
    None,

    /// <summary>Delete the seed-scenario users matched by email.</summary>
    SeededUsers,

    /// <summary>Delete every user, including ones adopted by an identity provider.</summary>
    AllUsers
}

/// <summary>
/// Decides what startup clears before re-seeding.
/// </summary>
public static class SeededDataResetPolicy
{
    /// <summary>
    /// Returns <see cref="SeededDataReset.AllUsers"/> when <see cref="SeedingSettings.ClearDataOnStartup"/> is set,
    /// otherwise <see cref="SeededDataReset.SeededUsers"/> in Development and <see cref="SeededDataReset.None"/> elsewhere.
    /// Throws when the setting is enabled in Production, so real data is never wiped.
    /// </summary>
    /// <param name="settings">The seeding settings, or null when the section is absent.</param>
    /// <param name="environment">The host environment.</param>
    public static SeededDataReset Decide(SeedingSettings? settings, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        if (settings?.ClearDataOnStartup == true)
        {
            if (environment.IsProduction())
            {
                throw new InvalidOperationException(
                    "Seeding:ClearDataOnStartup must not be enabled in Production.");
            }

            return SeededDataReset.AllUsers;
        }

        return environment.IsDevelopment() ? SeededDataReset.SeededUsers : SeededDataReset.None;
    }
}
