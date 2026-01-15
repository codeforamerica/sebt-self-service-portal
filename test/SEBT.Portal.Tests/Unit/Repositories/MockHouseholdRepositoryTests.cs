using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Infrastructure.Repositories;

namespace SEBT.Portal.Tests.Unit.Repositories;

/// <summary>
/// Unit tests for MockHouseholdRepository.
/// </summary>
public class MockHouseholdRepositoryTests
{
    private static readonly DateTimeOffset FixedSeedTime = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly MockHouseholdRepository _repository;
    private readonly FakeTimeProvider _timeProvider;

    public MockHouseholdRepositoryTests()
    {
        var logger = NullLogger<MockHouseholdRepository>.Instance;
        _timeProvider = new FakeTimeProvider(FixedSeedTime);
        _repository = new MockHouseholdRepository(logger, _timeProvider);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenHouseholdExists_ReturnsHouseholdData()
    {
        // Arrange
        var email = "verified@example.com";

        // Act
        var result = await _repository.GetHouseholdByEmailAsync(email);

        // Assert (flat model)
        Assert.NotNull(result);
        Assert.Equal(email, result.Email);
        Assert.Equal(ApplicationStatus.Approved, result.ApplicationStatus);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenHouseholdDoesNotExist_ReturnsNull()
    {
        // Arrange
        var email = "nonexistent@example.com";

        // Act
        var result = await _repository.GetHouseholdByEmailAsync(email);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenEmailIsNull_ReturnsNull()
    {
        // Act
        var result = await _repository.GetHouseholdByEmailAsync(null!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenEmailIsWhitespace_ReturnsNull()
    {
        // Act
        var result = await _repository.GetHouseholdByEmailAsync("   ");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_NormalizesEmailToLowercase()
    {
        // Arrange
        var email = "VERIFIED@EXAMPLE.COM";

        // Act
        var result = await _repository.GetHouseholdByEmailAsync(email);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("verified@example.com", result.Email);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenIncludeAddressIsTrue_ReturnsAddress()
    {
        // Arrange
        var email = "verified@example.com";

        // Act
        var result = await _repository.GetHouseholdByEmailAsync(email, includeAddress: true);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.AddressOnFile);
        Assert.Equal("123 Main Street", result.AddressOnFile.StreetAddress1);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenIncludeAddressIsFalse_DoesNotReturnAddress()
    {
        // Arrange
        var email = "verified@example.com";

        // Act
        var result = await _repository.GetHouseholdByEmailAsync(email, includeAddress: false);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.AddressOnFile);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_ReturnsCopyOfHouseholdData()
    {
        // Arrange
        var email = "verified@example.com";

        // Act
        var result1 = await _repository.GetHouseholdByEmailAsync(email);
        var result2 = await _repository.GetHouseholdByEmailAsync(email);

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        // Should be different instances (copies)
        Assert.NotSame(result1, result2);
        // But should have same data
        Assert.Equal(result1.Email, result2.Email);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_ReturnsAllSeededScenarios()
    {
        // Arrange
        var testEmails = new[]
        {
            "co-loaded@example.com",
            "verified@example.com",
            "pending@example.com",
            "denied@example.com",
            "review@example.com",
            "cancelled@example.com",
            "singlechild@example.com",
            "largefamily@example.com",
            "minimal@example.com",
            "expired@example.com",
            "unknown@example.com"
        };

        // Act & Assert
        foreach (var email in testEmails)
        {
            var result = await _repository.GetHouseholdByEmailAsync(email);
            Assert.NotNull(result);
            Assert.Equal(email, result.Email);
        }
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_VerifiedScenario_HasCorrectData()
    {
        // Arrange
        var email = "verified@example.com";

        // Act
        var result = await _repository.GetHouseholdByEmailAsync(email, includeAddress: true);

        // Assert (flat model)
        Assert.NotNull(result);
        Assert.Equal(ApplicationStatus.Approved, result.ApplicationStatus);
        Assert.Equal(2, result.Children.Count);
        Assert.Equal("John", result.Children[0].FirstName);
        Assert.Equal("Doe", result.Children[0].LastName);
        Assert.NotNull(result.BenefitIssueDate);
        Assert.NotNull(result.BenefitExpirationDate);
        Assert.Equal("1234", result.Last4DigitsOfCard);
        Assert.NotNull(result.AddressOnFile);
        Assert.Equal("123 Main Street", result.AddressOnFile.StreetAddress1);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_PendingScenario_HasCorrectData()
    {
        // Arrange
        var email = "pending@example.com";

        // Act
        var result = await _repository.GetHouseholdByEmailAsync(email);

        // Assert (flat model)
        Assert.NotNull(result);
        Assert.Equal(ApplicationStatus.Pending, result.ApplicationStatus);
        Assert.Single(result.Children);
        Assert.Equal("Alice", result.Children[0].FirstName);
        Assert.Equal("Smith", result.Children[0].LastName);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_LargeFamilyScenario_HasCorrectData()
    {
        // Arrange
        var email = "largefamily@example.com";

        // Act
        var result = await _repository.GetHouseholdByEmailAsync(email, includeAddress: true);

        // Assert (flat model)
        Assert.NotNull(result);
        Assert.Equal(4, result.Children.Count);
        Assert.Equal("Michael", result.Children[0].FirstName);
        Assert.Equal("Brown", result.Children[0].LastName);
    }

    [Fact]
    public async Task UpsertHouseholdAsync_CreatesNewHousehold()
    {
        // Arrange
        var newHousehold = new HouseholdData
        {
            Email = "new@example.com",
            Phone = "555-0000",
            ApplicationStatus = ApplicationStatus.Pending,
            Children = new List<Child>()
        };

        // Act
        await _repository.UpsertHouseholdAsync(newHousehold);

        // Assert
        var result = await _repository.GetHouseholdByEmailAsync("new@example.com");
        Assert.NotNull(result);
        Assert.Equal("new@example.com", result.Email);
        Assert.Equal("555-0000", result.Phone);
    }

    [Fact]
    public async Task UpsertHouseholdAsync_UpdatesExistingHousehold()
    {
        // Arrange
        var email = "verified@example.com";
        var updatedHousehold = new HouseholdData
        {
            Email = email,
            Phone = "555-9999",
            ApplicationStatus = ApplicationStatus.Denied,
            Children = new List<Child>()
        };

        // Act
        await _repository.UpsertHouseholdAsync(updatedHousehold);

        // Assert (flat model)
        var result = await _repository.GetHouseholdByEmailAsync(email);
        Assert.NotNull(result);
        Assert.Equal("555-9999", result.Phone);
        Assert.Equal(ApplicationStatus.Denied, result.ApplicationStatus);
    }

    [Fact]
    public async Task UpsertHouseholdAsync_NormalizesEmail()
    {
        // Arrange
        var household = new HouseholdData
        {
            Email = "  NEW@EXAMPLE.COM  ",
            ApplicationStatus = ApplicationStatus.Pending,
            Children = new List<Child>()
        };

        // Act
        await _repository.UpsertHouseholdAsync(household);

        // Assert
        var result = await _repository.GetHouseholdByEmailAsync("new@example.com");
        Assert.NotNull(result);
    }

    [Fact]
    public async Task UpsertHouseholdAsync_WhenHouseholdDataIsNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _repository.UpsertHouseholdAsync(null!));
    }

    [Fact]
    public async Task UpsertHouseholdAsync_WhenEmailIsNull_ThrowsArgumentException()
    {
        // Arrange
        var household = new HouseholdData
        {
            Email = null!,
            ApplicationStatus = ApplicationStatus.Pending,
            Children = new List<Child>()
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _repository.UpsertHouseholdAsync(household));
    }

    [Fact]
    public async Task UpsertHouseholdAsync_WhenEmailIsEmpty_ThrowsArgumentException()
    {
        // Arrange
        var household = new HouseholdData
        {
            Email = string.Empty,
            ApplicationStatus = ApplicationStatus.Pending,
            Children = new List<Child>()
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _repository.UpsertHouseholdAsync(household));
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_MinimalScenario_HasCorrectData()
    {
        // Arrange
        var email = "minimal@example.com";

        // Act
        var result = await _repository.GetHouseholdByEmailAsync(email);

        // Assert (flat model)
        Assert.NotNull(result);
        Assert.Equal(ApplicationStatus.Pending, result.ApplicationStatus);
        Assert.Null(result.Phone);
        Assert.Empty(result.Children);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_ExpiredScenario_HasExpiredBenefits()
    {
        // Arrange
        var email = "expired@example.com";

        // Act
        var result = await _repository.GetHouseholdByEmailAsync(email, includeAddress: true);

        // Assert (flat model)
        Assert.NotNull(result);
        Assert.NotNull(result.BenefitExpirationDate);
        Assert.True(result.BenefitExpirationDate < _timeProvider.GetUtcNow().UtcDateTime);
        Assert.Equal(ApplicationStatus.Approved, result.ApplicationStatus);
    }
}
