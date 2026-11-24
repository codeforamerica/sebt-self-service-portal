using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Models.UserActions;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Data.Entities;
using SEBT.Portal.Infrastructure.Repositories;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.Tests.Unit.TestSupport;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Repositories;

public class DatabaseUserActionRepositoryTests : IDisposable
{
    private static readonly IdentifierHasher Hasher = new(
        Options.Create(new IdentifierHasherSettings
        {
            SecretKey = TestPortalCryptography.TestIdentifierHasherSecretKey
        }));

    private static readonly Guid TestUserId = Guid.CreateVersion7();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly PortalDbContext _dbContext;
    private readonly DatabaseUserActionRepository _repository;

    public DatabaseUserActionRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new PortalDbContext(options);
        _dbContext.Users.Add(new UserEntity { Id = TestUserId });
        _dbContext.SaveChanges();
        _repository = new DatabaseUserActionRepository(
            _dbContext,
            Hasher,
            TestPortalCryptography.PiiSymmetricEncryption,
            _time);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task RecordAsync_WritesOneRowPerCaseWithActionOutcomeAndTimestamp()
    {
        UserCaseIdentifiers[] cases =
        [
            new("SEBT-001", "APP-9", "STU-4"),
            new("SEBT-002", "APP-10", "STU-5")
        ];

        await _repository.RecordAsync(
            TestUserId,
            UserActionType.RequestCardReplacement,
            UserActionOutcome.Failed,
            cases);

        var rows = _dbContext.UserActions.ToList();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.Equal(TestUserId, row.UserId);
            Assert.Equal(UserActionType.RequestCardReplacement, row.Action);
            Assert.Equal(UserActionOutcome.Failed, row.Outcome);
            Assert.Equal(_time.GetUtcNow().UtcDateTime, row.CreatedAt);
        });
    }

    [Fact]
    public async Task RecordAsync_StoresEncryptedAndHashedIdentifiers()
    {
        await _repository.RecordAsync(
            TestUserId,
            UserActionType.UpdateAddress,
            UserActionOutcome.Succeeded,
            [new UserCaseIdentifiers("SEBT-001", "APP-9", "STU-4")]);

        var stored = Assert.Single(_dbContext.UserActions);
        Assert.Equal(Hasher.Hash("SEBT-001"), stored.CaseIdHash);
        Assert.Equal(Hasher.Hash("APP-9"), stored.ApplicationIdHash);
        Assert.Equal(Hasher.Hash("STU-4"), stored.ApplicationStudentIdHash);
        Assert.Equal(
            "SEBT-001",
            TestPortalCryptography.PiiSymmetricEncryption.Decrypt(stored.CaseId));
        Assert.Equal(
            "APP-9",
            TestPortalCryptography.PiiSymmetricEncryption.Decrypt(stored.ApplicationId!));
        Assert.Equal(
            "STU-4",
            TestPortalCryptography.PiiSymmetricEncryption.Decrypt(stored.ApplicationStudentId!));
        Assert.NotEqual("SEBT-001", stored.CaseId);
    }

    [Fact]
    public async Task RecordAsync_WhenApplicationIdsAbsent_StoresEmptyHashes()
    {
        await _repository.RecordAsync(
            TestUserId,
            UserActionType.UpdateAddress,
            UserActionOutcome.Succeeded,
            [new UserCaseIdentifiers("SEBT-001", null, null)]);

        var stored = Assert.Single(_dbContext.UserActions);
        Assert.Equal(string.Empty, stored.ApplicationIdHash);
        Assert.Equal(string.Empty, stored.ApplicationStudentIdHash);
        Assert.Null(stored.ApplicationId);
        Assert.Null(stored.ApplicationStudentId);
    }

    [Fact]
    public async Task RecordAsync_WhenActivityIsActive_StoresSpanAndTraceIds()
    {
        using var activity = new Activity("test").Start();

        await _repository.RecordAsync(
            TestUserId,
            UserActionType.UpdateAddress,
            UserActionOutcome.Failed,
            [new UserCaseIdentifiers("SEBT-001", null, null)]);

        var stored = Assert.Single(_dbContext.UserActions);
        Assert.Equal(activity.SpanId.ToHexString(), stored.SpanId);
        Assert.Equal(activity.TraceId.ToHexString(), stored.TraceId);
    }

    [Fact]
    public async Task RecordAsync_WhenNoActivity_StoresNullTraceIds()
    {
        await _repository.RecordAsync(
            TestUserId,
            UserActionType.UpdateAddress,
            UserActionOutcome.Failed,
            [new UserCaseIdentifiers("SEBT-001", null, null)]);

        var stored = Assert.Single(_dbContext.UserActions);
        Assert.Null(stored.SpanId);
        Assert.Null(stored.TraceId);
    }

    [Fact]
    public async Task RecordAsync_WhenNoCases_WritesNothing()
    {
        await _repository.RecordAsync(
            TestUserId,
            UserActionType.RequestCardReplacement,
            UserActionOutcome.Rejected,
            []);

        Assert.Empty(_dbContext.UserActions);
    }

    [Fact]
    public async Task RecordAsync_WhenCaseIdMissing_SkipsTheRow()
    {
        await _repository.RecordAsync(
            TestUserId,
            UserActionType.RequestCardReplacement,
            UserActionOutcome.Succeeded,
            [new UserCaseIdentifiers("  ", "APP-9", null)]);

        Assert.Empty(_dbContext.UserActions);
    }
}
