using Microsoft.EntityFrameworkCore;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Data.Entities;
using SEBT.Portal.Infrastructure.Repositories;

namespace SEBT.Portal.Tests.Unit.Repositories;

public class CardReplacementRequestRepositoryTests : IDisposable
{
    private readonly PortalDbContext _dbContext;
    private readonly CardReplacementRequestRepository _repository;

    // Deterministic hash values for test isolation
    private const string HouseholdHash = "A1B2C3D4E5F6A1B2C3D4E5F6A1B2C3D4E5F6A1B2C3D4E5F6A1B2C3D4E5F6A1B2";
    private const string CaseHash = "1234567890ABCDEF1234567890ABCDEF1234567890ABCDEF1234567890ABCDEF";
    private const string OtherCaseHash = "FEDCBA0987654321FEDCBA0987654321FEDCBA0987654321FEDCBA0987654321";

    private static readonly Guid TestUserId = Guid.CreateVersion7();

    public CardReplacementRequestRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new PortalDbContext(options);

        // Seed a user for FK constraint
        _dbContext.Users.Add(new UserEntity { Id = TestUserId });
        _dbContext.SaveChanges();

        _repository = new CardReplacementRequestRepository(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task HasRecentRequestAsync_ReturnsFalse_WhenNoRequests()
    {
        var result = await _repository.HasRecentRequestAsync(
            HouseholdHash, CaseHash, TimeSpan.FromDays(14));

        Assert.False(result);
    }

    [Fact]
    public async Task HasRecentRequestAsync_ReturnsTrue_WhenRequestWithinCooldown()
    {
        _dbContext.CardReplacementRequests.Add(new CardReplacementRequestEntity
        {
            HouseholdIdentifierHash = HouseholdHash,
            CaseIdHash = CaseHash,
            RequestedAt = DateTime.UtcNow.AddDays(-3),
            RequestedByUserId = TestUserId
        });
        await _dbContext.SaveChangesAsync();

        var result = await _repository.HasRecentRequestAsync(
            HouseholdHash, CaseHash, TimeSpan.FromDays(14));

        Assert.True(result);
    }

    [Fact]
    public async Task HasRecentRequestAsync_ReturnsFalse_WhenRequestOutsideCooldown()
    {
        _dbContext.CardReplacementRequests.Add(new CardReplacementRequestEntity
        {
            HouseholdIdentifierHash = HouseholdHash,
            CaseIdHash = CaseHash,
            RequestedAt = DateTime.UtcNow.AddDays(-15),
            RequestedByUserId = TestUserId
        });
        await _dbContext.SaveChangesAsync();

        var result = await _repository.HasRecentRequestAsync(
            HouseholdHash, CaseHash, TimeSpan.FromDays(14));

        Assert.False(result);
    }

    [Fact]
    public async Task HasRecentRequestAsync_ReturnsFalse_WhenDifferentCase()
    {
        _dbContext.CardReplacementRequests.Add(new CardReplacementRequestEntity
        {
            HouseholdIdentifierHash = HouseholdHash,
            CaseIdHash = OtherCaseHash,
            RequestedAt = DateTime.UtcNow.AddDays(-3),
            RequestedByUserId = TestUserId
        });
        await _dbContext.SaveChangesAsync();

        var result = await _repository.HasRecentRequestAsync(
            HouseholdHash, CaseHash, TimeSpan.FromDays(14));

        Assert.False(result);
    }

    [Fact]
    public async Task CreateAsync_PersistsRequest()
    {
        await _repository.CreateAsync(HouseholdHash, CaseHash, TestUserId);

        var stored = await _dbContext.CardReplacementRequests.SingleAsync();
        Assert.Equal(HouseholdHash, stored.HouseholdIdentifierHash);
        Assert.Equal(CaseHash, stored.CaseIdHash);
        Assert.Equal(TestUserId, stored.RequestedByUserId);
        Assert.True((DateTime.UtcNow - stored.RequestedAt).TotalSeconds < 5);
    }
}
