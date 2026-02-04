using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Repositories;

namespace SEBT.Portal.Tests.Unit.Repositories;

/// <summary>
/// Unit tests for MockHouseholdRepository.
/// </summary>
public class MockHouseholdRepositoryTests
{
    private static readonly PiiVisibility FullPiiVisibility = new(IncludeAddress: true, IncludeEmail: true, IncludePhone: true);
    private static readonly PiiVisibility NoAddressPiiVisibility = new(IncludeAddress: false, IncludeEmail: true, IncludePhone: true);
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
        var result = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(email, result.Email);
        Assert.NotNull(result.Applications);
        Assert.NotEmpty(result.Applications);
        Assert.Equal(ApplicationStatus.Approved, result.Applications.First().ApplicationStatus);
    }

    [Theory]
    [InlineData("non-co-loaded@example.com", "Carlos", "Garcia", "Emma", ApplicationStatus.Pending)]
    [InlineData("not-started@example.com", "Jordan", "Anderson", "Liam", ApplicationStatus.Pending)]
    public async Task GetHouseholdByEmailAsync_DefaultSeededUsers_HaveAssociatedHouseholdData(
        string email,
        string expectedFirstName,
        string expectedLastName,
        string expectedChildFirstName,
        ApplicationStatus expectedApplicationStatus)
    {
        // Arrange & Act - Default seeded users must have household data for end-to-end testing
        var result = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(email, result.Email);
        Assert.NotNull(result.UserProfile);
        Assert.Equal(expectedFirstName, result.UserProfile.FirstName);
        Assert.Equal(expectedLastName, result.UserProfile.LastName);
        Assert.NotNull(result.Applications);
        Assert.NotEmpty(result.Applications);
        Assert.Equal(expectedApplicationStatus, result.Applications.First().ApplicationStatus);
        Assert.NotNull(result.Applications.First().Children);
        Assert.NotEmpty(result.Applications.First().Children);
        Assert.Equal(expectedChildFirstName, result.Applications.First().Children.First().FirstName);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenHouseholdDoesNotExist_ReturnsNull()
    {
        // Arrange
        var email = "nonexistent@example.com";

        // Act
        var result = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenPiiVisibilityIsNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _repository.GetHouseholdByEmailAsync("verified@example.com", null!));
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenEmailIsNull_ReturnsNull()
    {
        // Act
        var result = await _repository.GetHouseholdByEmailAsync(null!, FullPiiVisibility);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenEmailIsWhitespace_ReturnsNull()
    {
        // Act
        var result = await _repository.GetHouseholdByEmailAsync("   ", FullPiiVisibility);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_NormalizesEmailToLowercase()
    {
        // Arrange
        var email = "VERIFIED@EXAMPLE.COM";

        // Act
        var result = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility);

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
        var result = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility);

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
        var result = await _repository.GetHouseholdByEmailAsync(email, NoAddressPiiVisibility);

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
        var result1 = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility);
        var result2 = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility);

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
            var result = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility);
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
        var result = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Applications);
        Assert.NotEmpty(result.Applications);
        var app = result.Applications.First();
        Assert.Equal(ApplicationStatus.Approved, app.ApplicationStatus);
        Assert.Equal(2, app.Children.Count);
        Assert.Equal("John", app.Children[0].FirstName);
        Assert.Equal("Doe", app.Children[0].LastName);
        Assert.NotNull(app.BenefitIssueDate);
        Assert.NotNull(app.BenefitExpirationDate);
        Assert.Equal("1234", app.Last4DigitsOfCard);
        Assert.NotNull(result.AddressOnFile);
        Assert.Equal("123 Main Street", result.AddressOnFile.StreetAddress1);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_PendingScenario_HasCorrectData()
    {
        // Arrange
        var email = "pending@example.com";

        // Act
        var result = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Applications);
        Assert.NotEmpty(result.Applications);
        var app = result.Applications.First();
        Assert.Equal(ApplicationStatus.Pending, app.ApplicationStatus);
        Assert.Single(app.Children);
        Assert.Equal("Alice", app.Children[0].FirstName);
        Assert.Equal("Smith", app.Children[0].LastName);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_LargeFamilyScenario_HasCorrectData()
    {
        // Arrange
        var email = "largefamily@example.com";

        // Act
        var result = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Applications);
        Assert.NotEmpty(result.Applications);
        var allChildren = result.Applications.SelectMany(a => a.Children).ToList();
        Assert.Equal(4, allChildren.Count);
        Assert.Equal("Michael", allChildren[0].FirstName);
        Assert.Equal("Brown", allChildren[0].LastName);
    }

    [Fact]
    public async Task UpsertHouseholdAsync_CreatesNewHousehold()
    {
        // Arrange
        var newHousehold = new HouseholdData
        {
            Email = "new@example.com",
            Phone = "555-0000",
            Applications = new List<Application>
            {
                new Application { ApplicationStatus = ApplicationStatus.Pending }
            }
        };

        // Act
        await _repository.UpsertHouseholdAsync(newHousehold);

        // Assert
        var result = await _repository.GetHouseholdByEmailAsync("new@example.com", FullPiiVisibility);
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
            Applications = new List<Application>
            {
                new Application { ApplicationStatus = ApplicationStatus.Denied }
            }
        };

        // Act
        await _repository.UpsertHouseholdAsync(updatedHousehold);

        // Assert
        var result = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility);
        Assert.NotNull(result);
        Assert.Equal("555-9999", result.Phone);
        Assert.NotNull(result.Applications);
        Assert.NotEmpty(result.Applications);
        Assert.Equal(ApplicationStatus.Denied, result.Applications.First().ApplicationStatus);
    }

    [Fact]
    public async Task UpsertHouseholdAsync_NormalizesEmail()
    {
        // Arrange
        var household = new HouseholdData
        {
            Email = "  NEW@EXAMPLE.COM  ",
            Applications = new List<Application>
            {
                new Application { ApplicationStatus = ApplicationStatus.Pending }
            }
        };

        // Act
        await _repository.UpsertHouseholdAsync(household);

        // Assert
        var result = await _repository.GetHouseholdByEmailAsync("new@example.com", FullPiiVisibility);
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
            Applications = new List<Application>
            {
                new Application { ApplicationStatus = ApplicationStatus.Pending }
            }
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
            Applications = new List<Application>
            {
                new Application { ApplicationStatus = ApplicationStatus.Pending }
            }
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
        var result = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Applications);
        Assert.NotEmpty(result.Applications);
        Assert.Equal(ApplicationStatus.Pending, result.Applications.First().ApplicationStatus);
        Assert.Null(result.Phone);
        var allChildren = result.Applications.SelectMany(a => a.Children).ToList();
        Assert.Empty(allChildren);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenIncludeEmailFalse_ReturnsEmptyEmail()
    {
        // Arrange
        var email = "verified@example.com";
        var noEmailVisibility = new PiiVisibility(IncludeAddress: true, IncludeEmail: false, IncludePhone: true);

        // Act
        var result = await _repository.GetHouseholdByEmailAsync(email, noEmailVisibility);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.Email);
        Assert.NotNull(result.Phone);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenIncludePhoneFalse_ReturnsNullPhone()
    {
        // Arrange
        var email = "verified@example.com";
        var noPhoneVisibility = new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: false);

        // Act
        var result = await _repository.GetHouseholdByEmailAsync(email, noPhoneVisibility);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(email, result.Email);
        Assert.Null(result.Phone);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_ExpiredScenario_HasExpiredBenefits()
    {
        // Arrange
        var email = "expired@example.com";

        // Act
        var result = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Applications);
        Assert.NotEmpty(result.Applications);
        var app = result.Applications.First();
        Assert.NotNull(app.BenefitExpirationDate);
        Assert.True(app.BenefitExpirationDate < _timeProvider.GetUtcNow().UtcDateTime);
        Assert.Equal(ApplicationStatus.Approved, app.ApplicationStatus);
    }
}
