using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Seeding;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Seeding.Services;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.Tests.Unit.Repositories;
using SEBT.Portal.Tests.Unit.TestSupport;

namespace SEBT.Portal.Tests.Unit.Services;

/// <summary>
/// Focused seeder regression tests kept separate from <see cref="DatabaseSeederTests"/>
/// so PII-hook tooling does not block edits to the larger fixture file.
/// </summary>
[Collection("SqlServer")]
[Trait("Category", "SqlServer")]
public class DatabaseSeederExpiredScenarioTests : IClassFixture<SqlServerTestFixture>
{
    private static readonly DateTimeOffset FixedSeedTime = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    private static readonly IIdentifierHasher TestHasher = new IdentifierHasher(
        Options.Create(new IdentifierHasherSettings { SecretKey = "TestKeyMustBeAtLeast32CharactersLong!!" }));

    private readonly SqlServerTestFixture _fixture;

    public DatabaseSeederExpiredScenarioTests(SqlServerTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SeedTestUsersAsync_WithMockHouseholdData_ExpiredUser_HasExpiredStatusNotCompleted()
    {
        const string scenarioEmail = "expired@example.com";

        using var context = _fixture.CreateContext();
        await CleanupDatabaseAsync(context);

        var settings = new SeedingSettings { EmailPattern = "{0}@example.com", State = "dc" };
        var dataSeeder = new DataSeeder(
            context,
            TestHasher,
            TestPortalCryptography.PiiSymmetricEncryption,
            TestPortalCryptography.EmailLookupHasher);
        var seeder = new DatabaseSeeder(dataSeeder, settings, timeProvider: new FakeTimeProvider(FixedSeedTime));

        await seeder.SeedTestUsersAsync(useMockHouseholdData: true);

        var fingerprint = TestPortalCryptography.FingerprintEmail(scenarioEmail);
        var normalized = TestPortalCryptography.NormalizeEmailStrict(scenarioEmail);
        var user = await context.Users.SingleOrDefaultAsync(u =>
            u.EmailHash == fingerprint || (u.EmailHash == null && u.Email == normalized));

        Assert.NotNull(user);
        // Expired is a distinct workflow state — must not be seeded as Completed even though
        // the scenario carries IAL1+ (benefit expiration, not missing verification).
        Assert.Equal((int)IdProofingStatus.Expired, user!.IdProofingStatus);
        Assert.Equal((int)UserIalLevel.IAL1plus, user.IalLevel);
        Assert.NotNull(user.IdProofingCompletedAt);
    }

    private static async Task CleanupDatabaseAsync(PortalDbContext context)
    {
        context.ChangeTracker.Clear();
        context.UserOptIns.RemoveRange(await context.UserOptIns.ToListAsync());
        context.Users.RemoveRange(await context.Users.ToListAsync());
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }
}
