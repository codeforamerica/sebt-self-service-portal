using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Seeding;
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
        _repository = new MockHouseholdRepository(logger, timeProvider: _timeProvider);
    }

    private static MockHouseholdRepository CreateRepository(string emailPattern, string? state = null)
    {
        var logger = NullLogger<MockHouseholdRepository>.Instance;
        var settings = Options.Create(new SeedingSettings { EmailPattern = emailPattern, State = state });
        return new MockHouseholdRepository(logger, settings, new FakeTimeProvider(FixedSeedTime));
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_WhenEmailIdentifierAndHouseholdExists_ReturnsHouseholdData()
    {
        // Arrange
        var identifier = HouseholdIdentifier.Email("verified@example.com");

        // Act
        var result = await _repository.GetHouseholdByIdentifierAsync(identifier, FullPiiVisibility, UserIalLevel.IAL1plus);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("verified@example.com", result.Email);
        Assert.NotNull(result.Applications);
        Assert.NotEmpty(result.Applications);
        Assert.Equal(ApplicationStatus.Approved, result.Applications.First().ApplicationStatus);
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_WhenPhoneIdentifierAndHouseholdExists_ReturnsHouseholdData()
    {
        // Mock supports phone lookup for DevelopmentPhoneOverride; co-loaded uses default override phone
        var identifier = HouseholdIdentifier.Phone("8185558437");

        var result = await _repository.GetHouseholdByIdentifierAsync(identifier, FullPiiVisibility, UserIalLevel.IAL1plus);

        Assert.NotNull(result);
        Assert.Equal("co-loaded@example.com", result.Email);
        Assert.Equal(ApplicationStatus.Approved, result.Applications?.First().ApplicationStatus);
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_WhenPhoneIdentifierWithFormatting_NormalizesAndFindsHousehold()
    {
        // Phone normalization strips non-digits; 555-123-4567 -> 5551234567
        var identifier = HouseholdIdentifier.Phone("555-123-4567");

        var result = await _repository.GetHouseholdByIdentifierAsync(identifier, FullPiiVisibility, UserIalLevel.IAL1plus);

        Assert.NotNull(result);
        Assert.Equal("non-co-loaded@example.com", result.Email);
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_WhenUnsupportedIdentifierType_ReturnsNull()
    {
        // SNAP ID, TANF ID, SSN, etc. are not keyed in mock data
        var identifier = HouseholdIdentifier.SnapId("SNAP123");

        var result = await _repository.GetHouseholdByIdentifierAsync(identifier, FullPiiVisibility, UserIalLevel.IAL1plus);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenHouseholdExists_ReturnsHouseholdData()
    {
        // Arrange
        var email = "verified@example.com";

        // Act
        var result = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility, UserIalLevel.IAL1plus);

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
        var result = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility, UserIalLevel.IAL1plus);

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
        var result = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility, UserIalLevel.IAL1plus);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenPiiVisibilityIsNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _repository.GetHouseholdByEmailAsync("verified@example.com", null!, UserIalLevel.None));
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenEmailIsNull_ReturnsNull()
    {
        // Act
        var result = await _repository.GetHouseholdByEmailAsync(null!, FullPiiVisibility, UserIalLevel.IAL1plus);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenEmailIsWhitespace_ReturnsNull()
    {
        // Act
        var result = await _repository.GetHouseholdByEmailAsync("   ", FullPiiVisibility, UserIalLevel.IAL1plus);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_NormalizesEmailToLowercase()
    {
        // Arrange
        var email = "VERIFIED@EXAMPLE.COM";

        // Act
        var result = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility, UserIalLevel.IAL1plus);

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
        var result = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility, UserIalLevel.IAL1plus);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.AddressOnFile);
        Assert.Equal("123 Main Street", result.AddressOnFile.StreetAddress1);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenIncludeAddressIsFalse_ReturnsMaskedAddress()
    {
        // Arrange
        var email = "verified@example.com";

        // Act
        var result = await _repository.GetHouseholdByEmailAsync(email, NoAddressPiiVisibility, UserIalLevel.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.AddressOnFile);
        Assert.Equal("****", result.AddressOnFile.StreetAddress1);
        Assert.Equal("Denver", result.AddressOnFile.City);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_ReturnsCopyOfHouseholdData()
    {
        // Arrange
        var email = "verified@example.com";

        // Act
        var result1 = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility, UserIalLevel.IAL1plus);
        var result2 = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility, UserIalLevel.IAL1plus);

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
        // Derive emails from the default catalog (no state set, so DC-only scenarios are excluded)
        var defaultSettings = new SeedingSettings();
        var expectedScenarios = SeedScenarios.AllScenarios
            .Where(s => !SeedScenarios.DcOnlyScenarios.Contains(s));

        foreach (var scenario in expectedScenarios)
        {
            var email = defaultSettings.BuildEmail(scenario.Name);
            var result = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility, UserIalLevel.IAL1plus);
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
        var result = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility, UserIalLevel.IAL1plus);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Applications);
        Assert.NotEmpty(result.Applications);
        var app = result.Applications.First();
        Assert.Equal(ApplicationStatus.Approved, app.ApplicationStatus);
        Assert.Equal(2, app.Children.Count);
        Assert.Equal("John", app.Children[0].FirstName);
        Assert.Equal("Doe", app.Children[0].LastName);
        Assert.Equal(ApplicationStatus.Unknown, app.Children[0].Status);
        Assert.Equal(ApplicationStatus.Unknown, app.Children[1].Status);
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
        var result = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility, UserIalLevel.IAL1plus);

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
        var result = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility, UserIalLevel.IAL1plus);

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
        var result = await _repository.GetHouseholdByEmailAsync("new@example.com", FullPiiVisibility, UserIalLevel.IAL1plus);
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
        var result = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility, UserIalLevel.IAL1plus);
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
        var result = await _repository.GetHouseholdByEmailAsync("new@example.com", FullPiiVisibility, UserIalLevel.IAL1plus);
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
        var result = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility, UserIalLevel.IAL1plus);

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
    public async Task GetHouseholdByEmailAsync_WhenIncludeEmailFalse_ReturnsMaskedEmail()
    {
        // Arrange
        var email = "verified@example.com";
        var noEmailVisibility = new PiiVisibility(IncludeAddress: true, IncludeEmail: false, IncludePhone: true);

        // Act
        var result = await _repository.GetHouseholdByEmailAsync(email, noEmailVisibility, UserIalLevel.IAL1plus);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("v***@example.com", result.Email);
        Assert.NotNull(result.Phone);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenIncludePhoneFalse_ReturnsMaskedPhone()
    {
        // Arrange — non-co-loaded scenario has an explicit phone number
        var email = "non-co-loaded@example.com";
        var noPhoneVisibility = new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: false);

        // Act
        var result = await _repository.GetHouseholdByEmailAsync(email, noPhoneVisibility, UserIalLevel.IAL1plus);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(email, result.Email);
        Assert.Equal("***-***-4567", result.Phone);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_ExpiredScenario_HasExpiredBenefits()
    {
        // Arrange
        var email = "expired@example.com";

        // Act
        var result = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility, UserIalLevel.IAL1plus);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Applications);
        Assert.NotEmpty(result.Applications);
        var app = result.Applications.First();
        Assert.NotNull(app.BenefitExpirationDate);
        Assert.True(app.BenefitExpirationDate < _timeProvider.GetUtcNow().UtcDateTime);
        Assert.Equal(ApplicationStatus.Unknown, app.ApplicationStatus);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WithDcEmailPattern_ReturnsAllSeededScenarios()
    {
        const string pattern = "sebt.dc+{0}@codeforamerica.org";
        var repo = CreateRepository(pattern, state: "dc");
        var settings = new SeedingSettings { EmailPattern = pattern, State = "dc" };

        foreach (var scenario in SeedScenarios.AllScenarios)
        {
            var email = settings.BuildEmail(scenario.Name);
            var result = await repo.GetHouseholdByEmailAsync(email, FullPiiVisibility, UserIalLevel.IAL1plus);
            Assert.NotNull(result);
            Assert.Equal(email, result.Email);
        }
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WithCoEmailPattern_VerifiedScenarioHasCorrectData()
    {
        var repo = CreateRepository("sebt.co+{0}@codeforamerica.org");
        var email = "sebt.co+verified@codeforamerica.org";

        var result = await repo.GetHouseholdByEmailAsync(email, FullPiiVisibility, UserIalLevel.IAL1plus);

        // Same scenario data, different email for CO
        Assert.NotNull(result);
        Assert.Equal(email, result.Email);
        Assert.NotNull(result.Applications);
        Assert.NotEmpty(result.Applications);
        var app = result.Applications.First();
        Assert.Equal(ApplicationStatus.Approved, app.ApplicationStatus);
        Assert.Equal(2, app.Children.Count);
        Assert.Equal("John", app.Children[0].FirstName);
        Assert.Equal("Doe", app.Children[0].LastName);
        Assert.Equal("1234", app.Last4DigitsOfCard);
        Assert.NotNull(result.AddressOnFile);
        Assert.Equal("123 Main Street", result.AddressOnFile.StreetAddress1);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WithCustomPattern_DefaultEmailsReturnNull()
    {
        var repo = CreateRepository("sebt.dc+{0}@codeforamerica.org");

        // Try looking up with default @example.com emails
        var result = await repo.GetHouseholdByEmailAsync("verified@example.com", FullPiiVisibility, UserIalLevel.IAL1plus);

        // Should not find anything since data is keyed by the custom pattern
        Assert.Null(result);
    }
}
