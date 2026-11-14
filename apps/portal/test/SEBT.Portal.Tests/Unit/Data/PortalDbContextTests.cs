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
    public void Users_ShouldHavePrimaryKeyOnId()
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
        Assert.Equal("Id", primaryKey.Properties[0].Name);
    }

    [Fact]
    public void Users_Email_ShouldBeNullable()
    {
        // Email is optional — OIDC users may not have an email address.
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
        Assert.True(emailProperty!.IsNullable);
    }

    [Fact]
    public void Users_EmailHash_ShouldHaveFilteredUniqueIndex()
    {
        // Primary equality lookup uses EmailHash; ciphertext rows omit IX_Users_Email_LegacyLookup (filtered out).
        // Arrange
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new PortalDbContext(options);

        // Act
        var entityType = context.Model.FindEntityType(typeof(UserEntity));
        var emailHashIndex = entityType!.GetIndexes()
            .FirstOrDefault(i => i.Properties.Count == 1 && i.Properties[0].Name == "EmailHash");

        // Assert
        Assert.NotNull(emailHashIndex);
        Assert.True(emailHashIndex!.IsUnique);
        Assert.Equal("IX_Users_EmailHash", emailHashIndex.GetDatabaseName());
        Assert.Equal("[EmailHash] IS NOT NULL", emailHashIndex.GetFilter());
    }

    [Fact]
    public void Users_Email_ShouldHaveFilteredNonUniqueIndex_ForLegacyPlaintextLookup()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new PortalDbContext(options);

        // Act
        var entityType = context.Model.FindEntityType(typeof(UserEntity));
        var legacyEmailIndex = entityType!.GetIndexes()
            .FirstOrDefault(i => i.Properties.Count == 1 && i.Properties[0].Name == "Email");

        // Assert
        Assert.NotNull(legacyEmailIndex);
        Assert.False(legacyEmailIndex!.IsUnique);
        Assert.Equal("IX_Users_Email_LegacyLookup", legacyEmailIndex.GetDatabaseName());
        Assert.Equal("[EmailHash] IS NULL AND [Email] IS NOT NULL", legacyEmailIndex.GetFilter());
    }

    [Fact]
    public void Users_IdentityProviderUserId_ShouldBeNullableWithMaxLength255()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new PortalDbContext(options);

        // Act
        var entityType = context.Model.FindEntityType(typeof(UserEntity));
        var property = entityType!.FindProperty("IdentityProviderUserId");

        // Assert
        Assert.NotNull(property);
        Assert.True(property!.IsNullable);
        Assert.Equal(255, property.GetMaxLength());
    }

    [Fact]
    public void Users_LoginCount_ShouldBeRequiredWithDefaultZero()
    {
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        using var context = new PortalDbContext(options);

        var property = context.Model.FindEntityType(typeof(UserEntity))!.FindProperty("LoginCount");

        Assert.NotNull(property);
        Assert.False(property!.IsNullable);
        Assert.Equal(0, property.GetDefaultValue());
    }

    [Fact]
    public void Users_HouseholdIdentifierHash_ShouldHaveMaxLength64AndIndex()
    {
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        using var context = new PortalDbContext(options);

        var entityType = context.Model.FindEntityType(typeof(UserEntity))!;
        var property = entityType.FindProperty("HouseholdIdentifierHash");
        var index = entityType.GetIndexes()
            .FirstOrDefault(i => i.Properties.Count == 1 && i.Properties[0].Name == "HouseholdIdentifierHash");

        Assert.NotNull(property);
        Assert.True(property!.IsNullable);
        Assert.Equal(64, property.GetMaxLength());
        Assert.NotNull(index);
        Assert.False(index!.IsUnique);
        Assert.Equal("IX_Users_HouseholdIdentifierHash", index.GetDatabaseName());
    }

    [Fact]
    public void Users_IdentityProviderUserId_ShouldHaveFilteredUniqueIndex()
    {
        // Filtered index allows multiple null IdentityProviderUserIds (email-auth users)
        // while enforcing uniqueness across OIDC users.
        // Arrange
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new PortalDbContext(options);

        // Act
        var entityType = context.Model.FindEntityType(typeof(UserEntity));
        var index = entityType!.GetIndexes()
            .FirstOrDefault(i => i.Properties.Count == 1 && i.Properties[0].Name == "IdentityProviderUserId");

        // Assert
        Assert.NotNull(index);
        Assert.True(index!.IsUnique);
        Assert.Equal("IX_Users_IdentityProviderUserId", index.GetDatabaseName());
        Assert.Equal("[IdentityProviderUserId] IS NOT NULL", index.GetFilter());
    }

    [Fact]
    public void Users_Email_ShouldHaveMaxLength512()
    {
        // Email column holds AES-GCM envelopes (much wider than plaintext addresses).
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
        Assert.Equal(512, emailProperty!.GetMaxLength());
    }

    [Fact]
    public void Users_IalLevel_ShouldBeRequired()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new PortalDbContext(options);

        // Act
        var entityType = context.Model.FindEntityType(typeof(UserEntity));
        var statusProperty = entityType!.FindProperty("IalLevel");

        // Assert
        Assert.NotNull(statusProperty);
        Assert.False(statusProperty!.IsNullable);
    }

    [Fact]
    public void Users_IalLevel_ShouldHaveDefaultValueOfZero()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new PortalDbContext(options);

        // Act
        var entityType = context.Model.FindEntityType(typeof(UserEntity));
        var statusProperty = entityType!.FindProperty("IalLevel");
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
    public void UserCases_ShouldBeConfiguredWithTableNamePrimaryKeyAndUniqueIdentityIndex()
    {
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new PortalDbContext(options);
        var entityType = context.Model.FindEntityType(typeof(UserCaseEntity));

        Assert.NotNull(entityType);
        Assert.Equal("UserCases", entityType!.GetTableName());
        Assert.Equal("Id", entityType.FindPrimaryKey()!.Properties.Single().Name);

        var identityIndex = entityType.GetIndexes().Single(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(
                ["UserId", "CaseIdHash", "ApplicationIdHash", "ApplicationStudentIdHash"]));
        Assert.True(identityIndex.IsUnique);
        Assert.Equal("IX_UserCases_User_CaseIdentity", identityIndex.GetDatabaseName());
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
            IalLevel = (int)UserIalLevel.None
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

    [Fact]
    public void UserActions_ShouldBeConfiguredForPerUserTimeline()
    {
        var options = new DbContextOptionsBuilder<PortalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new PortalDbContext(options);
        var entityType = context.Model.FindEntityType(typeof(UserActionEntity));

        Assert.NotNull(entityType);
        Assert.Equal("UserActions", entityType!.GetTableName());
        Assert.Equal("Id", entityType.FindPrimaryKey()!.Properties.Single().Name);

        var timelineIndex = entityType.GetIndexes().Single(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(["UserId", "CreatedAt"]));
        Assert.Equal("IX_UserActions_User_CreatedAt", timelineIndex.GetDatabaseName());

        var userForeignKey = entityType.GetForeignKeys().Single();
        Assert.Equal(typeof(UserEntity), userForeignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Cascade, userForeignKey.DeleteBehavior);

        Assert.Equal(typeof(string), entityType.FindProperty(nameof(UserActionEntity.Action))!.GetProviderClrType());
        Assert.Equal(typeof(string), entityType.FindProperty(nameof(UserActionEntity.Outcome))!.GetProviderClrType());

        Assert.Equal(16, entityType.FindProperty(nameof(UserActionEntity.SpanId))!.GetMaxLength());
        Assert.Equal(32, entityType.FindProperty(nameof(UserActionEntity.TraceId))!.GetMaxLength());
    }
}
