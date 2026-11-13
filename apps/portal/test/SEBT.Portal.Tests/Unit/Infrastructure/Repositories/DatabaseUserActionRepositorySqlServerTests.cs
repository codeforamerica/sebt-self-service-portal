using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.UserActions;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Data.Entities;
using SEBT.Portal.Infrastructure.Repositories;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.Tests.Unit.Repositories;
using SEBT.Portal.Tests.Unit.TestSupport;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Repositories;

/// <seealso cref="SqlServerTestFixture"/>
[Collection("SqlServer")]
[Trait("Category", "SqlServer")]
public class DatabaseUserActionRepositorySqlServerTests : IClassFixture<SqlServerTestFixture>
{
    private static readonly IdentifierHasher Hasher = new(
        Options.Create(new IdentifierHasherSettings
        {
            SecretKey = TestPortalCryptography.TestIdentifierHasherSecretKey
        }));

    private readonly SqlServerTestFixture _fixture;
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));

    public DatabaseUserActionRepositorySqlServerTests(SqlServerTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RecordAsync_StoresActionAndOutcomeAsText()
    {
        var userId = await CreateUserAsync();

        await using (var db = _fixture.CreateContext())
        {
            await CreateRepository(db).RecordAsync(
                userId,
                UserActionType.RequestCardReplacement,
                UserActionOutcome.Rejected,
                [new("SEBT-001", null, null)]);
        }

        await using var verify = _fixture.CreateContext();
        var action = await verify.Database
            .SqlQuery<string>($"SELECT [Action] AS [Value] FROM UserActions WHERE UserId = {userId}")
            .SingleAsync();
        var outcome = await verify.Database
            .SqlQuery<string>($"SELECT [Outcome] AS [Value] FROM UserActions WHERE UserId = {userId}")
            .SingleAsync();
        Assert.Equal("RequestCardReplacement", action);
        Assert.Equal("Rejected", outcome);
    }

    [Fact]
    public async Task RecordAsync_WhenUnrelatedChangesArePending_ThrowsWithoutSavingThem()
    {
        var userId = await CreateUserAsync();

        await using var db = _fixture.CreateContext();
        var trackedUser = await db.Users.SingleAsync(u => u.Id == userId);
        trackedUser.IsCoLoaded = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateRepository(db).RecordAsync(
                userId,
                UserActionType.UpdateAddress,
                UserActionOutcome.Succeeded,
                [new("SEBT-001", null, null)]));

        await using var verify = _fixture.CreateContext();
        Assert.False((await verify.Users.SingleAsync(u => u.Id == userId)).IsCoLoaded);
        Assert.Empty(verify.UserActions.Where(a => a.UserId == userId));
    }

    private async Task<Guid> CreateUserAsync()
    {
        await using var db = _fixture.CreateContext();
        var user = new UserEntity { Id = Guid.CreateVersion7() };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private DatabaseUserActionRepository CreateRepository(PortalDbContext db) =>
        new(db, Hasher, TestPortalCryptography.PiiSymmetricEncryption, _time);
}
