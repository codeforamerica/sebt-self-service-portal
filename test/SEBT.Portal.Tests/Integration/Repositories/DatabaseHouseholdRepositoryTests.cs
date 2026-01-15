using Microsoft.EntityFrameworkCore;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Data.Entities;
using SEBT.Portal.Infrastructure.Repositories;
using SEBT.Portal.Tests.Unit.Repositories;

namespace SEBT.Portal.Tests.Integration.Repositories;

/// <summary>
/// Integration tests for DatabaseHouseholdRepository.
/// </summary>
[Collection("SqlServer")]
public class DatabaseHouseholdRepositoryTests : IClassFixture<SqlServerTestFixture>
{
    private readonly SqlServerTestFixture _fixture;

    public DatabaseHouseholdRepositoryTests(SqlServerTestFixture fixture)
    {
        _fixture = fixture;
    }

    private PortalDbContext CreateContext()
    {
        return _fixture.CreateContext();
    }

    #region GetHouseholdByEmailAsync Tests

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenHouseholdExists_ShouldReturnHouseholdData()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseHouseholdRepository(context);

        var email = $"test-{Guid.NewGuid()}@example.com";
        var entity = new HouseholdEntity
        {
            Email = email,
            Phone = "555-1234",
            BenefitIssueDate = DateTime.UtcNow.AddDays(-30),
            BenefitExpirationDate = DateTime.UtcNow.AddDays(60),
            Last4DigitsOfCard = "1234",
            ApplicationNumber = "APP-123",
            CaseNumber = "CASE-456",
            ApplicationStatus = (int)ApplicationStatus.Approved,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Households.Add(entity);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetHouseholdByEmailAsync(email);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(email, result!.Email);
        Assert.Equal("555-1234", result.Phone);
        Assert.Equal("APP-123", result.ApplicationNumber);
        Assert.Equal("CASE-456", result.CaseNumber);
        Assert.Equal(ApplicationStatus.Approved, result.ApplicationStatus);
        Assert.Equal("1234", result.Last4DigitsOfCard);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenHouseholdDoesNotExist_ShouldReturnNull()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseHouseholdRepository(context);

        // Act
        var result = await repository.GetHouseholdByEmailAsync("nonexistent@example.com");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenIncludeAddressIsTrue_ShouldReturnAddress()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseHouseholdRepository(context);

        var email = $"test-{Guid.NewGuid()}@example.com";
        var householdEntity = new HouseholdEntity
        {
            Email = email,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var addressEntity = new AddressEntity
        {
            HouseholdId = 0, // Will be set after save
            StreetAddress1 = "123 Main St",
            StreetAddress2 = "Apt 4B",
            City = "Denver",
            State = "CO",
            PostalCode = "80202"
        };
        householdEntity.Address = addressEntity;
        context.Households.Add(householdEntity);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetHouseholdByEmailAsync(email, includeAddress: true);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result!.AddressOnFile);
        Assert.Equal("123 Main St", result.AddressOnFile.StreetAddress1);
        Assert.Equal("Apt 4B", result.AddressOnFile.StreetAddress2);
        Assert.Equal("Denver", result.AddressOnFile.City);
        Assert.Equal("CO", result.AddressOnFile.State);
        Assert.Equal("80202", result.AddressOnFile.PostalCode);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenIncludeAddressIsFalse_ShouldNotReturnAddress()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseHouseholdRepository(context);

        var email = $"test-{Guid.NewGuid()}@example.com";
        var householdEntity = new HouseholdEntity
        {
            Email = email,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var addressEntity = new AddressEntity
        {
            HouseholdId = 0,
            StreetAddress1 = "123 Main St",
            City = "Denver",
            State = "CO",
            PostalCode = "80202"
        };
        householdEntity.Address = addressEntity;
        context.Households.Add(householdEntity);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetHouseholdByEmailAsync(email, includeAddress: false);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result!.AddressOnFile);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenHouseholdHasNoAddress_ShouldReturnNullAddress()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseHouseholdRepository(context);

        var email = $"test-{Guid.NewGuid()}@example.com";
        var entity = new HouseholdEntity
        {
            Email = email,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Households.Add(entity);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetHouseholdByEmailAsync(email, includeAddress: true);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result!.AddressOnFile);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_ShouldIncludeChildren()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseHouseholdRepository(context);

        var email = $"test-{Guid.NewGuid()}@example.com";
        var householdEntity = new HouseholdEntity
        {
            Email = email,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Children = new List<ChildEntity>
            {
                new ChildEntity { FirstName = "John", LastName = "Doe" },
                new ChildEntity { FirstName = "Jane", LastName = "Doe" }
            }
        };
        context.Households.Add(householdEntity);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetHouseholdByEmailAsync(email);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result!.Children.Count);
        Assert.Equal(2, result.ChildrenOnApplication);
        Assert.Contains(result.Children, c => c.FirstName == "John" && c.LastName == "Doe");
        Assert.Contains(result.Children, c => c.FirstName == "Jane" && c.LastName == "Doe");
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_ShouldNormalizeEmailToLowercase()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseHouseholdRepository(context);

        var baseEmail = $"test-{Guid.NewGuid()}@example.com";
        var lowercaseEmail = baseEmail.ToLowerInvariant();
        var entity = new HouseholdEntity
        {
            Email = lowercaseEmail,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Households.Add(entity);
        await context.SaveChangesAsync();

        // Act - query with different casing
        var result1 = await repository.GetHouseholdByEmailAsync(baseEmail.ToUpperInvariant());
        var result2 = await repository.GetHouseholdByEmailAsync(baseEmail);
        var result3 = await repository.GetHouseholdByEmailAsync(lowercaseEmail);

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotNull(result3);
        Assert.Equal(lowercaseEmail, result1!.Email);
        Assert.Equal(lowercaseEmail, result2!.Email);
        Assert.Equal(lowercaseEmail, result3!.Email);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenEmailIsNull_ShouldReturnNull()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseHouseholdRepository(context);

        // Act
        var result = await repository.GetHouseholdByEmailAsync(null!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenEmailIsWhitespace_ShouldReturnNull()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseHouseholdRepository(context);

        // Act
        var result = await repository.GetHouseholdByEmailAsync("   ");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_ShouldMapAllPropertiesCorrectly()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseHouseholdRepository(context);

        var email = $"test-{Guid.NewGuid()}@example.com";
        var issueDate = DateTime.UtcNow.AddDays(-30);
        var expirationDate = DateTime.UtcNow.AddDays(60);
        var createdAt = DateTime.UtcNow.AddDays(-5);
        var updatedAt = DateTime.UtcNow.AddDays(-2);

        var entity = new HouseholdEntity
        {
            Email = email,
            Phone = "555-1234",
            BenefitIssueDate = issueDate,
            BenefitExpirationDate = expirationDate,
            Last4DigitsOfCard = "1234",
            ApplicationNumber = "APP-123",
            CaseNumber = "CASE-456",
            ApplicationStatus = (int)ApplicationStatus.UnderReview,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
        context.Households.Add(entity);
        await context.SaveChangesAsync();

        // Act
        var result = await repository.GetHouseholdByEmailAsync(email);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(email, result!.Email);
        Assert.Equal("555-1234", result.Phone);
        Assert.Equal(issueDate, result.BenefitIssueDate);
        Assert.Equal(expirationDate, result.BenefitExpirationDate);
        Assert.Equal("1234", result.Last4DigitsOfCard);
        Assert.Equal("APP-123", result.ApplicationNumber);
        Assert.Equal("CASE-456", result.CaseNumber);
        Assert.Equal(ApplicationStatus.UnderReview, result.ApplicationStatus);
    }

    #endregion

    #region UpsertHouseholdAsync Tests

    [Fact]
    public async Task UpsertHouseholdAsync_WhenHouseholdDoesNotExist_ShouldCreateNewHousehold()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseHouseholdRepository(context);

        var email = $"new-{Guid.NewGuid()}@example.com";
        var householdData = new HouseholdData
        {
            Email = email,
            Phone = "555-1234",
            BenefitIssueDate = DateTime.UtcNow.AddDays(-30),
            BenefitExpirationDate = DateTime.UtcNow.AddDays(60),
            Last4DigitsOfCard = "1234",
            ApplicationNumber = "APP-123",
            CaseNumber = "CASE-456",
            ApplicationStatus = ApplicationStatus.Approved
        };

        // Act
        await repository.UpsertHouseholdAsync(householdData);

        // Assert
        var stored = await context.Households
            .FirstOrDefaultAsync(h => h.Email == email.ToLowerInvariant());
        Assert.NotNull(stored);
        Assert.Equal(email.ToLowerInvariant(), stored!.Email);
        Assert.Equal("555-1234", stored.Phone);
        Assert.Equal("APP-123", stored.ApplicationNumber);
        Assert.Equal("CASE-456", stored.CaseNumber);
        Assert.Equal((int)ApplicationStatus.Approved, stored.ApplicationStatus);
    }

    [Fact]
    public async Task UpsertHouseholdAsync_WhenHouseholdExists_ShouldUpdateExistingHousehold()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseHouseholdRepository(context);

        var email = $"update-{Guid.NewGuid()}@example.com";
        var originalEntity = new HouseholdEntity
        {
            Email = email,
            Phone = "555-1111",
            ApplicationStatus = (int)ApplicationStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            UpdatedAt = DateTime.UtcNow.AddDays(-5)
        };
        context.Households.Add(originalEntity);
        await context.SaveChangesAsync();

        var originalUpdatedAt = originalEntity.UpdatedAt;

        await Task.Delay(10); // Small delay to ensure timestamp difference

        var householdData = new HouseholdData
        {
            Email = email,
            Phone = "555-9999",
            ApplicationStatus = ApplicationStatus.Approved,
            ApplicationNumber = "APP-UPDATED"
        };

        // Act
        await repository.UpsertHouseholdAsync(householdData);

        // Assert
        var updated = await context.Households
            .FirstOrDefaultAsync(h => h.Email == email);
        Assert.NotNull(updated);
        Assert.Equal("555-9999", updated!.Phone);
        Assert.Equal((int)ApplicationStatus.Approved, updated.ApplicationStatus);
        Assert.Equal("APP-UPDATED", updated.ApplicationNumber);
        Assert.True(updated.UpdatedAt > originalUpdatedAt);
        Assert.Equal(originalEntity.CreatedAt, updated.CreatedAt); // CreatedAt should not change
    }

    [Fact]
    public async Task UpsertHouseholdAsync_ShouldNormalizeEmailToLowercase()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseHouseholdRepository(context);

        var uniqueId = Guid.NewGuid();
        var mixedCaseEmail = $"MIXED-{uniqueId}@CASE.COM";
        var householdData = new HouseholdData
        {
            Email = mixedCaseEmail,
            ApplicationStatus = ApplicationStatus.Pending
        };

        // Act
        await repository.UpsertHouseholdAsync(householdData);

        // Assert
        var expectedEmail = $"mixed-{uniqueId}@case.com";
        var stored = await context.Households
            .FirstOrDefaultAsync(h => h.Email == expectedEmail);
        Assert.NotNull(stored);
        Assert.Equal(expectedEmail, stored!.Email);
    }

    [Fact]
    public async Task UpsertHouseholdAsync_WhenHouseholdIsNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseHouseholdRepository(context);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            repository.UpsertHouseholdAsync(null!));
    }

    [Fact]
    public async Task UpsertHouseholdAsync_WhenEmailIsEmpty_ShouldThrowArgumentException()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseHouseholdRepository(context);

        var householdData = new HouseholdData
        {
            Email = "",
            ApplicationStatus = ApplicationStatus.Pending
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.UpsertHouseholdAsync(householdData));
        Assert.Contains("Email cannot be null or empty", exception.Message);
    }

    [Fact]
    public async Task UpsertHouseholdAsync_WhenEmailIsWhitespace_ShouldThrowArgumentException()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseHouseholdRepository(context);

        var householdData = new HouseholdData
        {
            Email = "   ",
            ApplicationStatus = ApplicationStatus.Pending
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.UpsertHouseholdAsync(householdData));
    }

    [Fact]
    public async Task UpsertHouseholdAsync_ShouldCreateChildren()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseHouseholdRepository(context);

        var email = $"children-{Guid.NewGuid()}@example.com";
        var householdData = new HouseholdData
        {
            Email = email,
            ApplicationStatus = ApplicationStatus.Pending,
            Children = new List<Child>
            {
                new Child { FirstName = "John", LastName = "Doe" },
                new Child { FirstName = "Jane", LastName = "Doe" }
            }
        };

        // Act
        await repository.UpsertHouseholdAsync(householdData);

        // Assert
        var stored = await context.Households
            .Include(h => h.Children)
            .FirstOrDefaultAsync(h => h.Email == email);
        Assert.NotNull(stored);
        Assert.Equal(2, stored!.Children.Count);
        Assert.Contains(stored.Children, c => c.FirstName == "John" && c.LastName == "Doe");
        Assert.Contains(stored.Children, c => c.FirstName == "Jane" && c.LastName == "Doe");
    }

    [Fact]
    public async Task UpsertHouseholdAsync_ShouldUpdateChildren()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseHouseholdRepository(context);

        var email = $"update-children-{Guid.NewGuid()}@example.com";
        var householdEntity = new HouseholdEntity
        {
            Email = email,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Children = new List<ChildEntity>
            {
                new ChildEntity { FirstName = "Old", LastName = "Child" }
            }
        };
        context.Households.Add(householdEntity);
        await context.SaveChangesAsync();

        var householdData = new HouseholdData
        {
            Email = email,
            ApplicationStatus = ApplicationStatus.Pending,
            Children = new List<Child>
            {
                new Child { FirstName = "New", LastName = "Child1" },
                new Child { FirstName = "New", LastName = "Child2" }
            }
        };

        // Act
        await repository.UpsertHouseholdAsync(householdData);

        // Assert
        var updated = await context.Households
            .Include(h => h.Children)
            .FirstOrDefaultAsync(h => h.Email == email);
        Assert.NotNull(updated);
        Assert.Equal(2, updated!.Children.Count);
        Assert.DoesNotContain(updated.Children, c => c.FirstName == "Old");
        Assert.Contains(updated.Children, c => c.FirstName == "New" && c.LastName == "Child1");
        Assert.Contains(updated.Children, c => c.FirstName == "New" && c.LastName == "Child2");
    }

    [Fact]
    public async Task UpsertHouseholdAsync_ShouldRemoveChildrenWhenEmptyList()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseHouseholdRepository(context);

        var email = $"remove-children-{Guid.NewGuid()}@example.com";
        var householdEntity = new HouseholdEntity
        {
            Email = email,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Children = new List<ChildEntity>
            {
                new ChildEntity { FirstName = "Child", LastName = "ToRemove" }
            }
        };
        context.Households.Add(householdEntity);
        await context.SaveChangesAsync();

        var householdData = new HouseholdData
        {
            Email = email,
            ApplicationStatus = ApplicationStatus.Pending,
            Children = new List<Child>()
        };

        // Act
        await repository.UpsertHouseholdAsync(householdData);

        // Assert
        var updated = await context.Households
            .Include(h => h.Children)
            .FirstOrDefaultAsync(h => h.Email == email);
        Assert.NotNull(updated);
        Assert.Empty(updated!.Children);
    }

    [Fact]
    public async Task UpsertHouseholdAsync_ShouldCreateAddress()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseHouseholdRepository(context);

        var email = $"address-{Guid.NewGuid()}@example.com";
        var householdData = new HouseholdData
        {
            Email = email,
            ApplicationStatus = ApplicationStatus.Pending,
            AddressOnFile = new Address
            {
                StreetAddress1 = "123 Main St",
                StreetAddress2 = "Apt 4B",
                City = "Denver",
                State = "CO",
                PostalCode = "80202"
            }
        };

        // Act
        await repository.UpsertHouseholdAsync(householdData);

        // Assert
        var stored = await context.Households
            .Include(h => h.Address)
            .FirstOrDefaultAsync(h => h.Email == email);
        Assert.NotNull(stored);
        Assert.NotNull(stored!.Address);
        Assert.Equal("123 Main St", stored.Address.StreetAddress1);
        Assert.Equal("Apt 4B", stored.Address.StreetAddress2);
        Assert.Equal("Denver", stored.Address.City);
        Assert.Equal("CO", stored.Address.State);
        Assert.Equal("80202", stored.Address.PostalCode);
    }

    [Fact]
    public async Task UpsertHouseholdAsync_ShouldUpdateAddress()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseHouseholdRepository(context);

        var email = $"update-address-{Guid.NewGuid()}@example.com";
        var householdEntity = new HouseholdEntity
        {
            Email = email,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Address = new AddressEntity
            {
                StreetAddress1 = "Old Address",
                City = "Old City",
                State = "NY",
                PostalCode = "10001"
            }
        };
        context.Households.Add(householdEntity);
        await context.SaveChangesAsync();

        var householdData = new HouseholdData
        {
            Email = email,
            ApplicationStatus = ApplicationStatus.Pending,
            AddressOnFile = new Address
            {
                StreetAddress1 = "New Address",
                City = "New City",
                State = "CO",
                PostalCode = "80202"
            }
        };

        // Act
        await repository.UpsertHouseholdAsync(householdData);

        // Assert
        var updated = await context.Households
            .Include(h => h.Address)
            .FirstOrDefaultAsync(h => h.Email == email);
        Assert.NotNull(updated);
        Assert.NotNull(updated!.Address);
        Assert.Equal("New Address", updated.Address.StreetAddress1);
        Assert.Equal("New City", updated.Address.City);
        Assert.Equal("CO", updated.Address.State);
        Assert.Equal("80202", updated.Address.PostalCode);
    }

    [Fact]
    public async Task UpsertHouseholdAsync_ShouldRemoveAddressWhenNull()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseHouseholdRepository(context);

        var email = $"remove-address-{Guid.NewGuid()}@example.com";
        var householdEntity = new HouseholdEntity
        {
            Email = email,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Address = new AddressEntity
            {
                StreetAddress1 = "Address To Remove",
                City = "City",
                State = "CO",
                PostalCode = "80202"
            }
        };
        context.Households.Add(householdEntity);
        await context.SaveChangesAsync();

        var householdData = new HouseholdData
        {
            Email = email,
            ApplicationStatus = ApplicationStatus.Pending,
            AddressOnFile = null
        };

        // Act
        await repository.UpsertHouseholdAsync(householdData);

        // Assert
        var updated = await context.Households
            .Include(h => h.Address)
            .FirstOrDefaultAsync(h => h.Email == email);
        Assert.NotNull(updated);
        Assert.Null(updated!.Address);

        // Verify address was deleted from database
        var addressCount = await context.Addresses
            .CountAsync(a => a.HouseholdId == updated.Id);
        Assert.Equal(0, addressCount);
    }

    [Fact]
    public async Task UpsertHouseholdAsync_ShouldHandleAllApplicationStatuses()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseHouseholdRepository(context);

        var statuses = new[]
        {
            ApplicationStatus.Unknown,
            ApplicationStatus.Pending,
            ApplicationStatus.Approved,
            ApplicationStatus.Denied,
            ApplicationStatus.UnderReview,
            ApplicationStatus.Cancelled
        };

        foreach (var status in statuses)
        {
            var email = $"status-{status}-{Guid.NewGuid()}@example.com";
            var householdData = new HouseholdData
            {
                Email = email,
                ApplicationStatus = status
            };

            // Act
            await repository.UpsertHouseholdAsync(householdData);

            // Assert
            var stored = await context.Households
                .FirstOrDefaultAsync(h => h.Email == email);
            Assert.NotNull(stored);
            Assert.Equal((int)status, stored!.ApplicationStatus);
        }
    }

    [Fact]
    public async Task UpsertHouseholdAsync_ShouldPreserveCreatedAtOnUpdate()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseHouseholdRepository(context);

        var email = $"preserve-created-{Guid.NewGuid()}@example.com";
        var originalCreatedAt = DateTime.UtcNow.AddDays(-10);
        var householdEntity = new HouseholdEntity
        {
            Email = email,
            CreatedAt = originalCreatedAt,
            UpdatedAt = DateTime.UtcNow.AddDays(-5)
        };
        context.Households.Add(householdEntity);
        await context.SaveChangesAsync();

        var householdData = new HouseholdData
        {
            Email = email,
            ApplicationStatus = ApplicationStatus.Approved
        };

        // Act
        await repository.UpsertHouseholdAsync(householdData);

        // Assert
        var updated = await context.Households
            .FirstOrDefaultAsync(h => h.Email == email);
        Assert.NotNull(updated);
        Assert.Equal(originalCreatedAt, updated!.CreatedAt);
    }

    [Fact]
    public async Task UpsertHouseholdAsync_ShouldHandleComplexHouseholdData()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new DatabaseHouseholdRepository(context);

        var email = $"complex-{Guid.NewGuid()}@example.com";
        var issueDate = DateTime.UtcNow.AddDays(-30);
        var expirationDate = DateTime.UtcNow.AddDays(60);

        var householdData = new HouseholdData
        {
            Email = email,
            Phone = "555-1234",
            BenefitIssueDate = issueDate,
            BenefitExpirationDate = expirationDate,
            Last4DigitsOfCard = "1234",
            ApplicationNumber = "APP-123",
            CaseNumber = "CASE-456",
            ApplicationStatus = ApplicationStatus.Approved,
            Children = new List<Child>
            {
                new Child { FirstName = "John", LastName = "Doe" },
                new Child { FirstName = "Jane", LastName = "Doe" }
            },
            AddressOnFile = new Address
            {
                StreetAddress1 = "123 Main St",
                StreetAddress2 = "Apt 4B",
                City = "Denver",
                State = "CO",
                PostalCode = "80202"
            }
        };

        // Act
        await repository.UpsertHouseholdAsync(householdData);

        // Assert
        var stored = await context.Households
            .Include(h => h.Children)
            .Include(h => h.Address)
            .FirstOrDefaultAsync(h => h.Email == email);
        Assert.NotNull(stored);
        Assert.Equal("555-1234", stored!.Phone);
        Assert.Equal(issueDate, stored.BenefitIssueDate);
        Assert.Equal(expirationDate, stored.BenefitExpirationDate);
        Assert.Equal("1234", stored.Last4DigitsOfCard);
        Assert.Equal("APP-123", stored.ApplicationNumber);
        Assert.Equal("CASE-456", stored.CaseNumber);
        Assert.Equal((int)ApplicationStatus.Approved, stored.ApplicationStatus);
        Assert.Equal(2, stored.Children.Count);
        Assert.NotNull(stored.Address);
        Assert.Equal("123 Main St", stored.Address!.StreetAddress1);
    }

    #endregion
}
