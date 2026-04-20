using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Infrastructure.Data.Entities;

namespace SEBT.Portal.Tests.Unit.Data.Entities;

public class UserEntityTests
{
    [Fact]
    public void UserEntity_ShouldInitializeWithDefaultValues()
    {
        // Act
        var entity = new UserEntity();

        // Assert
        Assert.Null(entity.Email);
        Assert.Null(entity.ExternalProviderId);
        Assert.Equal(0, entity.IalLevel); // UserIalLevel.None
        Assert.Null(entity.IdProofingSessionId);
        Assert.Null(entity.IdProofingCompletedAt);
        Assert.Null(entity.IdProofingExpiresAt);
        Assert.True(entity.CreatedAt <= DateTime.UtcNow);
        Assert.True(entity.UpdatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void UserEntity_ShouldSetAndGetProperties()
    {
        // Arrange
        var entity = new UserEntity();
        var testEmail = "test@example.com";
        var testSessionId = "session-123";
        var testCompletedAt = DateTime.UtcNow.AddDays(-1);
        var testExpiresAt = DateTime.UtcNow.AddYears(1);
        var testCreatedAt = DateTime.UtcNow.AddDays(-5);
        var testUpdatedAt = DateTime.UtcNow.AddDays(-2);

        // Act
        entity.Email = testEmail;
        entity.IalLevel = (int)UserIalLevel.IAL1plus;
        entity.IdProofingSessionId = testSessionId;
        entity.IdProofingCompletedAt = testCompletedAt;
        entity.IdProofingExpiresAt = testExpiresAt;
        entity.CreatedAt = testCreatedAt;
        entity.UpdatedAt = testUpdatedAt;

        // Assert
        Assert.Equal(testEmail, entity.Email);
        Assert.Equal((int)UserIalLevel.IAL1plus, entity.IalLevel);
        Assert.Equal(testSessionId, entity.IdProofingSessionId);
        Assert.Equal(testCompletedAt, entity.IdProofingCompletedAt);
        Assert.Equal(testExpiresAt, entity.IdProofingExpiresAt);
        Assert.Equal(testCreatedAt, entity.CreatedAt);
        Assert.Equal(testUpdatedAt, entity.UpdatedAt);
    }

    [Fact]
    public void UserEntity_ShouldAllowAllIalLevels()
    {
        // Arrange & Act
        var none = new UserEntity { IalLevel = (int)UserIalLevel.None };
        var ial1 = new UserEntity { IalLevel = (int)UserIalLevel.IAL1 };
        var ial1plus = new UserEntity { IalLevel = (int)UserIalLevel.IAL1plus };
        var ial2 = new UserEntity { IalLevel = (int)UserIalLevel.IAL2 };

        // Assert
        Assert.Equal((int)UserIalLevel.None, none.IalLevel);
        Assert.Equal((int)UserIalLevel.IAL1, ial1.IalLevel);
        Assert.Equal((int)UserIalLevel.IAL1plus, ial1plus.IalLevel);
        Assert.Equal((int)UserIalLevel.IAL2, ial2.IalLevel);
    }

    [Fact]
    public void UserEntity_ShouldAllowNullSessionId()
    {
        // Arrange
        var entity = new UserEntity
        {
            Email = "user@example.com",
            IdProofingSessionId = null
        };

        // Assert
        Assert.Null(entity.IdProofingSessionId);
    }

    [Fact]
    public void UserEntity_ShouldAllowNullCompletedAt()
    {
        // Arrange
        var entity = new UserEntity
        {
            Email = "user@example.com",
            IdProofingCompletedAt = null
        };

        // Assert
        Assert.Null(entity.IdProofingCompletedAt);
    }

    [Fact]
    public void UserEntity_ShouldAllowNullExpiresAt()
    {
        // Arrange
        var entity = new UserEntity
        {
            Email = "user@example.com",
            IdProofingExpiresAt = null
        };

        // Assert
        Assert.Null(entity.IdProofingExpiresAt);
    }

    [Fact]
    public void UserEntity_CreatedAt_ShouldDefaultToUtcNow()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;

        // Act
        var entity = new UserEntity();

        // Assert
        var afterCreation = DateTime.UtcNow;
        Assert.True(entity.CreatedAt >= beforeCreation);
        Assert.True(entity.CreatedAt <= afterCreation);
    }

    [Fact]
    public void UserEntity_UpdatedAt_ShouldDefaultToUtcNow()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;

        // Act
        var entity = new UserEntity();

        // Assert
        var afterCreation = DateTime.UtcNow;
        Assert.True(entity.UpdatedAt >= beforeCreation);
        Assert.True(entity.UpdatedAt <= afterCreation);
    }
}

