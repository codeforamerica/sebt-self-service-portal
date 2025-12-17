using Microsoft.EntityFrameworkCore;
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

}
