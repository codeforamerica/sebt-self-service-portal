using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Data.Entities;
using SEBT.Portal.Infrastructure.Repositories;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.Tests.Unit.TestSupport;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Repositories;

public class DatabaseUserCaseRepositoryTests : IDisposable
{
    private static readonly IdentifierHasher Hasher = new(
        Options.Create(new IdentifierHasherSettings
        {
            SecretKey = TestPortalCryptography.TestIdentifierHasherSecretKey
        }));

    private readonly PortalDbContext _dbContext;
    private readonly DatabaseUserCaseRepository _repository;
    private static readonly Guid TestUserId = Guid.CreateVersion7();

    public DatabaseUserCaseRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new PortalDbContext(options);
        _dbContext.Users.Add(new UserEntity { Id = TestUserId });
        _dbContext.SaveChanges();
        _repository = new DatabaseUserCaseRepository(
            _dbContext,
            Hasher,
            TestPortalCryptography.PiiSymmetricEncryption,
            TimeProvider.System);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task UpsertDisplayedCasesAsync_InsertsEncryptedAndHashedIdentifiers()
    {
        await _repository.UpsertDisplayedCasesAsync(
            TestUserId,
            [new UserCaseIdentifiers("SEBT-001", "APP-9", "STU-4")]);

        var stored = Assert.Single(_dbContext.UserCases);
        Assert.Equal(TestUserId, stored.UserId);
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
    public async Task UpsertDisplayedCasesAsync_WhenApplicationIdsAbsent_StoresEmptyHashes()
    {
        await _repository.UpsertDisplayedCasesAsync(
            TestUserId,
            [new UserCaseIdentifiers("SEBT-001", null, null)]);

        var stored = Assert.Single(_dbContext.UserCases);
        Assert.Equal(string.Empty, stored.ApplicationIdHash);
        Assert.Equal(string.Empty, stored.ApplicationStudentIdHash);
        Assert.Null(stored.ApplicationId);
        Assert.Null(stored.ApplicationStudentId);
    }

    [Fact]
    public async Task UpsertDisplayedCasesAsync_WhenSameCaseIdDifferentStudentId_InsertsSeparateRows()
    {
        await _repository.UpsertDisplayedCasesAsync(
            TestUserId,
            [
                new UserCaseIdentifiers("CWIN-1", "APP-1", "CHILD-A"),
                new UserCaseIdentifiers("CWIN-1", "APP-1", "CHILD-B")
            ]);

        Assert.Equal(2, _dbContext.UserCases.Count());
    }

    [Fact]
    public async Task UpsertDisplayedCasesAsync_WhenACaseIsOmitted_LeavesTheExistingRow()
    {
        await _repository.UpsertDisplayedCasesAsync(
            TestUserId,
            [
                new UserCaseIdentifiers("SEBT-001", null, null),
                new UserCaseIdentifiers("SEBT-002", null, null)
            ]);

        await _repository.UpsertDisplayedCasesAsync(
            TestUserId,
            [new UserCaseIdentifiers("SEBT-001", null, null)]);

        Assert.Equal(2, _dbContext.UserCases.Count());
        Assert.Contains(_dbContext.UserCases, e => e.CaseIdHash == Hasher.Hash("SEBT-002"));
    }

    [Fact]
    public async Task UpsertDisplayedCasesAsync_WhenCaseIdMissing_SkipsTheRow()
    {
        await _repository.UpsertDisplayedCasesAsync(
            TestUserId,
            [new UserCaseIdentifiers("  ", "APP-9", null)]);

        Assert.Empty(_dbContext.UserCases);
    }
}
