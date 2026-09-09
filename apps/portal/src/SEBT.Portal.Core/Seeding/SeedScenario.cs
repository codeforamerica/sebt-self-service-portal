using SEBT.Portal.Core.Models.Auth;

namespace SEBT.Portal.Core.Seeding;

/// <summary>
/// Defines a seed scenario with its name and IAL level.
/// The name is used with <see cref="AppSettings.SeedingSettings.BuildEmail"/> to construct emails.
/// </summary>
public record SeedScenario(string Name, UserIalLevel IalLevel);
