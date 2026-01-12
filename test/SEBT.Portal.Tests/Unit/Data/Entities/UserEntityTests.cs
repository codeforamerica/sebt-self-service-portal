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
        Assert.Equal(string.Empty, entity.Email);
        Assert.Equal(0, entity.IdProofingStatus); // NotStarted
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
        entity.IdProofingStatus = (int)IdProofingStatus.Completed;
        entity.IdProofingSessionId = testSessionId;
        entity.IdProofingCompletedAt = testCompletedAt;
        entity.IdProofingExpiresAt = testExpiresAt;
        entity.CreatedAt = testCreatedAt;
        entity.UpdatedAt = testUpdatedAt;

        // Assert
        Assert.Equal(testEmail, entity.Email);
        Assert.Equal((int)IdProofingStatus.Completed, entity.IdProofingStatus);
        Assert.Equal(testSessionId, entity.IdProofingSessionId);
        Assert.Equal(testCompletedAt, entity.IdProofingCompletedAt);
        Assert.Equal(testExpiresAt, entity.IdProofingExpiresAt);
        Assert.Equal(testCreatedAt, entity.CreatedAt);
        Assert.Equal(testUpdatedAt, entity.UpdatedAt);
    }

    [Fact]
    public void UserEntity_ShouldAllowAllIdProofingStatuses()
    {
        // Arrange & Act
        var notStarted = new UserEntity { IdProofingStatus = (int)IdProofingStatus.NotStarted };
        var inProgress = new UserEntity { IdProofingStatus = (int)IdProofingStatus.InProgress };
        var completed = new UserEntity { IdProofingStatus = (int)IdProofingStatus.Completed };
        var failed = new UserEntity { IdProofingStatus = (int)IdProofingStatus.Failed };
        var expired = new UserEntity { IdProofingStatus = (int)IdProofingStatus.Expired };

        // Assert
        Assert.Equal((int)IdProofingStatus.NotStarted, notStarted.IdProofingStatus);
        Assert.Equal((int)IdProofingStatus.InProgress, inProgress.IdProofingStatus);
        Assert.Equal((int)IdProofingStatus.Completed, completed.IdProofingStatus);
        Assert.Equal((int)IdProofingStatus.Failed, failed.IdProofingStatus);
        Assert.Equal((int)IdProofingStatus.Expired, expired.IdProofingStatus);
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

