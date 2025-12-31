using Microsoft.EntityFrameworkCore;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Data.Entities;

namespace SEBT.Portal.Tests.Unit.Data;

public class PortalDbContextTests
{
    [Fact]
    public void UserOptIns_ShouldBeConfiguredWithCorrectTableName()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new PortalDbContext(options);

        // Act
        var entityType = context.Model.FindEntityType(typeof(UserOptInEntity));

        // Assert
        Assert.NotNull(entityType);
        Assert.Equal("UserOptIns", entityType!.GetTableName());
    }

    [Fact]
    public void UserOptIns_ShouldHavePrimaryKeyOnId()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new PortalDbContext(options);

        // Act
        var entityType = context.Model.FindEntityType(typeof(UserOptInEntity));
        var primaryKey = entityType!.FindPrimaryKey();

        // Assert
        Assert.NotNull(primaryKey);
        Assert.Single(primaryKey!.Properties);
        Assert.Equal("Id", primaryKey.Properties[0].Name);
    }

    [Fact]
    public void UserOptIns_Email_ShouldHaveUniqueIndex()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new PortalDbContext(options);

        // Act
        var entityType = context.Model.FindEntityType(typeof(UserOptInEntity));
        var indexes = entityType!.GetIndexes();

        // Assert
        var emailIndex = indexes.FirstOrDefault(i => i.Properties.Any(p => p.Name == "Email"));
        Assert.NotNull(emailIndex);
        Assert.True(emailIndex!.IsUnique);
    }

    [Fact]
    public void UserOptIns_Email_ShouldBeRequired()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new PortalDbContext(options);

        // Act
        var entityType = context.Model.FindEntityType(typeof(UserOptInEntity));
        var emailProperty = entityType!.FindProperty("Email");

        // Assert
        Assert.NotNull(emailProperty);
        Assert.False(emailProperty!.IsNullable);
    }

    [Fact]
    public void UserOptIns_Email_ShouldHaveMaxLength255()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new PortalDbContext(options);

        // Act
        var entityType = context.Model.FindEntityType(typeof(UserOptInEntity));
        var emailProperty = entityType!.FindProperty("Email");

        // Assert
        Assert.NotNull(emailProperty);
        Assert.Equal(255, emailProperty!.GetMaxLength());
    }

    [Fact]
    public void UserOptIns_EmailOptIn_ShouldBeRequired()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new PortalDbContext(options);

        // Act
        var entityType = context.Model.FindEntityType(typeof(UserOptInEntity));
        var emailOptInProperty = entityType!.FindProperty("EmailOptIn");

        // Assert
        Assert.NotNull(emailOptInProperty);
        Assert.False(emailOptInProperty!.IsNullable);
    }

    [Fact]
    public void UserOptIns_DobOptIn_ShouldBeRequired()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new PortalDbContext(options);

        // Act
        var entityType = context.Model.FindEntityType(typeof(UserOptInEntity));
        var dobOptInProperty = entityType!.FindProperty("DobOptIn");

        // Assert
        Assert.NotNull(dobOptInProperty);
        Assert.False(dobOptInProperty!.IsNullable);
    }

    [Fact]
    public void UserOptIns_CreatedAt_ShouldBeRequired()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new PortalDbContext(options);

        // Act
        var entityType = context.Model.FindEntityType(typeof(UserOptInEntity));
        var createdAtProperty = entityType!.FindProperty("CreatedAt");

        // Assert
        Assert.NotNull(createdAtProperty);
        Assert.False(createdAtProperty!.IsNullable);
    }

    [Fact]
    public void UserOptIns_UpdatedAt_ShouldBeRequired()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new PortalDbContext(options);

        // Act
        var entityType = context.Model.FindEntityType(typeof(UserOptInEntity));
        var updatedAtProperty = entityType!.FindProperty("UpdatedAt");

        // Assert
        Assert.NotNull(updatedAtProperty);
        Assert.False(updatedAtProperty!.IsNullable);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenEmailOptInChanges_ShouldUpdateUpdatedAt()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new PortalDbContext(options);
        var entity = new UserOptInEntity
        {
            Email = "test@example.com",
            EmailOptIn = false,
            DobOptIn = false
        };

        context.UserOptIns.Add(entity);
        await context.SaveChangesAsync();

        var originalUpdatedAt = entity.UpdatedAt;
        await Task.Delay(10); // Small delay to ensure timestamp difference

        // Act
        entity.EmailOptIn = true;
        await context.SaveChangesAsync();

        // Assert
        Assert.True(entity.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenDobOptInChanges_ShouldUpdateUpdatedAt()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new PortalDbContext(options);
        var entity = new UserOptInEntity
        {
            Email = "test@example.com",
            EmailOptIn = false,
            DobOptIn = false
        };

        context.UserOptIns.Add(entity);
        await context.SaveChangesAsync();

        var originalUpdatedAt = entity.UpdatedAt;
        await Task.Delay(10); // Small delay to ensure timestamp difference

        // Act
        entity.DobOptIn = true;
        await context.SaveChangesAsync();

        // Assert
        Assert.True(entity.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenEmailChanges_ShouldNotUpdateUpdatedAt()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new PortalDbContext(options);
        var entity = new UserOptInEntity
        {
            Email = "test@example.com",
            EmailOptIn = false,
            DobOptIn = false
        };

        context.UserOptIns.Add(entity);
        await context.SaveChangesAsync();

        var originalUpdatedAt = entity.UpdatedAt;
        await Task.Delay(10); // Small delay to ensure timestamp difference

        // Act
        entity.Email = "newemail@example.com";
        await context.SaveChangesAsync();

        // Assert
        // UpdatedAt should NOT change when only Email changes (not an opt-in property)
        Assert.Equal(originalUpdatedAt, entity.UpdatedAt);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenBothOptInPropertiesChange_ShouldUpdateUpdatedAt()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new PortalDbContext(options);
        var entity = new UserOptInEntity
        {
            Email = "test@example.com",
            EmailOptIn = false,
            DobOptIn = false
        };

        context.UserOptIns.Add(entity);
        await context.SaveChangesAsync();

        var originalUpdatedAt = entity.UpdatedAt;
        await Task.Delay(10); // Small delay to ensure timestamp difference

        // Act - Change both opt-in properties simultaneously
        entity.EmailOptIn = true;
        entity.DobOptIn = true;
        await context.SaveChangesAsync();

        // Assert
        Assert.True(entity.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenNewEntityAdded_ShouldSetBothCreatedAtAndUpdatedAt()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new PortalDbContext(options);
        var beforeCreation = DateTime.UtcNow;

        var entity = new UserOptInEntity
        {
            Email = "test@example.com",
            EmailOptIn = true,
            DobOptIn = false
        };

        // Act
        context.UserOptIns.Add(entity);
        await context.SaveChangesAsync();

        var afterCreation = DateTime.UtcNow;

        // Assert
        Assert.True(entity.CreatedAt >= beforeCreation);
        Assert.True(entity.CreatedAt <= afterCreation);
        Assert.True(entity.UpdatedAt >= beforeCreation);
        Assert.True(entity.UpdatedAt <= afterCreation);
        // Allow 1 second tolerance for CreatedAt and UpdatedAt to account for timing differences
        var timeDifference = Math.Abs((entity.CreatedAt - entity.UpdatedAt).TotalSeconds);
        Assert.True(timeDifference < 1.0, $"CreatedAt and UpdatedAt should be equal within 1 second, but difference was {timeDifference} seconds");
    }

    [Fact]
    public void Users_ShouldBeConfiguredWithCorrectTableName()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new PortalDbContext(options);

        // Act
        var entityType = context.Model.FindEntityType(typeof(UserEntity));

        // Assert
        Assert.NotNull(entityType);
        Assert.Equal("Users", entityType!.GetTableName());
    }

    [Fact]
    public void Users_ShouldHavePrimaryKeyOnEmail()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new PortalDbContext(options);

        // Act
        var entityType = context.Model.FindEntityType(typeof(UserEntity));
        var primaryKey = entityType!.FindPrimaryKey();

        // Assert
        Assert.NotNull(primaryKey);
        Assert.Single(primaryKey!.Properties);
        Assert.Equal("Email", primaryKey.Properties[0].Name);
    }

    [Fact]
    public void Users_Email_ShouldBeRequired()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new PortalDbContext(options);

        // Act
        var entityType = context.Model.FindEntityType(typeof(UserEntity));
        var emailProperty = entityType!.FindProperty("Email");

        // Assert
        Assert.NotNull(emailProperty);
        Assert.False(emailProperty!.IsNullable);
    }

    [Fact]
    public void Users_Email_ShouldHaveMaxLength255()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new PortalDbContext(options);

        // Act
        var entityType = context.Model.FindEntityType(typeof(UserEntity));
        var emailProperty = entityType!.FindProperty("Email");

        // Assert
        Assert.NotNull(emailProperty);
        Assert.Equal(255, emailProperty!.GetMaxLength());
    }

    [Fact]
    public void Users_IdProofingStatus_ShouldBeRequired()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new PortalDbContext(options);

        // Act
        var entityType = context.Model.FindEntityType(typeof(UserEntity));
        var statusProperty = entityType!.FindProperty("IdProofingStatus");

        // Assert
        Assert.NotNull(statusProperty);
        Assert.False(statusProperty!.IsNullable);
    }

    [Fact]
    public void Users_IdProofingStatus_ShouldHaveDefaultValueOfZero()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new PortalDbContext(options);

        // Act
        var entityType = context.Model.FindEntityType(typeof(UserEntity));
        var statusProperty = entityType!.FindProperty("IdProofingStatus");
        var defaultValue = statusProperty!.GetDefaultValue();

        // Assert
        Assert.NotNull(defaultValue);
        Assert.Equal(0, defaultValue);
    }

    [Fact]
    public void Users_IdProofingSessionId_ShouldHaveMaxLength255()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new PortalDbContext(options);

        // Act
        var entityType = context.Model.FindEntityType(typeof(UserEntity));
        var sessionIdProperty = entityType!.FindProperty("IdProofingSessionId");

        // Assert
        Assert.NotNull(sessionIdProperty);
        Assert.Equal(255, sessionIdProperty!.GetMaxLength());
    }

    [Fact]
    public void Users_IdProofingSessionId_ShouldHaveIndex()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new PortalDbContext(options);

        // Act
        var entityType = context.Model.FindEntityType(typeof(UserEntity));
        var indexes = entityType!.GetIndexes();

        // Assert
        var sessionIdIndex = indexes.FirstOrDefault(i =>
            i.Properties.Count == 1 &&
            i.Properties[0].Name == "IdProofingSessionId");
        Assert.NotNull(sessionIdIndex);
        Assert.Equal("IX_Users_IdProofingSessionId", sessionIdIndex!.GetDatabaseName());
    }

    [Fact]
    public void Users_CreatedAt_ShouldBeRequired()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new PortalDbContext(options);

        // Act
        var entityType = context.Model.FindEntityType(typeof(UserEntity));
        var createdAtProperty = entityType!.FindProperty("CreatedAt");

        // Assert
        Assert.NotNull(createdAtProperty);
        Assert.False(createdAtProperty!.IsNullable);
    }

    [Fact]
    public void Users_UpdatedAt_ShouldBeRequired()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new PortalDbContext(options);

        // Act
        var entityType = context.Model.FindEntityType(typeof(UserEntity));
        var updatedAtProperty = entityType!.FindProperty("UpdatedAt");

        // Assert
        Assert.NotNull(updatedAtProperty);
        Assert.False(updatedAtProperty!.IsNullable);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenNewUserAdded_ShouldSetBothCreatedAtAndUpdatedAt()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new PortalDbContext(options);
        var beforeCreation = DateTime.UtcNow;

        var entity = new UserEntity
        {
            Email = "newuser@example.com",
            IdProofingStatus = (int)IdProofingStatus.NotStarted
        };

        // Act
        context.Users.Add(entity);
        await context.SaveChangesAsync();

        var afterCreation = DateTime.UtcNow;

        // Assert
        Assert.True(entity.CreatedAt >= beforeCreation);
        Assert.True(entity.CreatedAt <= afterCreation);
        Assert.True(entity.UpdatedAt >= beforeCreation);
        Assert.True(entity.UpdatedAt <= afterCreation);
    }

}
