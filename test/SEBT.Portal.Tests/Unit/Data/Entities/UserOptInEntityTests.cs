using SEBT.Portal.Infrastructure.Data.Entities;

namespace SEBT.Portal.Tests.Unit.Data.Entities;

public class UserOptInEntityTests
{
    [Fact]
    public void UserOptInEntity_ShouldInitializeWithDefaultValues()
    {
        // Act
        var entity = new UserOptInEntity();

        // Assert
        Assert.Equal(0, entity.Id);
        Assert.Equal(string.Empty, entity.Email);
        Assert.False(entity.EmailOptIn);
        Assert.False(entity.DobOptIn);
        Assert.True(entity.CreatedAt <= DateTime.UtcNow);
        Assert.True(entity.UpdatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void UserOptInEntity_ShouldSetAndGetProperties()
    {
        // Arrange
        var entity = new UserOptInEntity();
        var testEmail = "test@example.com";
        var testCreatedAt = DateTime.UtcNow.AddDays(-1);
        var testUpdatedAt = DateTime.UtcNow;

        // Act
        entity.Id = 1;
        entity.Email = testEmail;
        entity.EmailOptIn = true;
        entity.DobOptIn = true;
        entity.CreatedAt = testCreatedAt;
        entity.UpdatedAt = testUpdatedAt;

        // Assert
        Assert.Equal(1, entity.Id);
        Assert.Equal(testEmail, entity.Email);
        Assert.True(entity.EmailOptIn);
        Assert.True(entity.DobOptIn);
        Assert.Equal(testCreatedAt, entity.CreatedAt);
        Assert.Equal(testUpdatedAt, entity.UpdatedAt);
    }

    [Fact]
    public void UserOptInEntity_ShouldAllowPartialOptIns()
    {
        // Arrange
        var entity = new UserOptInEntity
        {
            Email = "user@example.com",
            EmailOptIn = true,
            DobOptIn = false
        };

        // Assert
        Assert.True(entity.EmailOptIn);
        Assert.False(entity.DobOptIn);
    }

    [Fact]
    public void UserOptInEntity_ShouldAllowBothOptIns()
    {
        // Arrange
        var entity = new UserOptInEntity
        {
            Email = "user@example.com",
            EmailOptIn = true,
            DobOptIn = true
        };

        // Assert
        Assert.True(entity.EmailOptIn);
        Assert.True(entity.DobOptIn);
    }

    [Fact]
    public void UserOptInEntity_ShouldAllowNoOptIns()
    {
        // Arrange
        var entity = new UserOptInEntity
        {
            Email = "user@example.com",
            EmailOptIn = false,
            DobOptIn = false
        };

        // Assert
        Assert.False(entity.EmailOptIn);
        Assert.False(entity.DobOptIn);
    }

    [Fact]
    public void UserOptInEntity_CreatedAt_ShouldDefaultToUtcNow()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;

        // Act
        var entity = new UserOptInEntity();

        // Assert
        var afterCreation = DateTime.UtcNow;
        Assert.True(entity.CreatedAt >= beforeCreation);
        Assert.True(entity.CreatedAt <= afterCreation);
    }

    [Fact]
    public void UserOptInEntity_UpdatedAt_ShouldDefaultToUtcNow()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;

        // Act
        var entity = new UserOptInEntity();

        // Assert
        var afterCreation = DateTime.UtcNow;
        Assert.True(entity.UpdatedAt >= beforeCreation);
        Assert.True(entity.UpdatedAt <= afterCreation);
    }
}
