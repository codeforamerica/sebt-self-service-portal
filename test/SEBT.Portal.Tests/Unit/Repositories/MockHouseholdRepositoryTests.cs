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
    [InlineData("non-co-loaded@example.com", "Carlos", "GarciaMOCK", "Emma", ApplicationStatus.Pending)]
    [InlineData("not-started@example.com", "Jordan", "AndersonMOCK", "Liam", ApplicationStatus.Pending)]
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
    public async Task GetHouseholdByEmailAsync_WhenDcCoLoadedPendingIdProofing_UsesDistinctPhoneAndCoLoadedCases()
    {
        var repo = CreateRepository("{0}@example.com", state: "dc");
        const string email = "co-loaded-pending-id-proofing@example.com";

        var result = await repo.GetHouseholdByEmailAsync(email, FullPiiVisibility, UserIalLevel.None);

        Assert.NotNull(result);
        Assert.Equal(email, result!.Email);
        Assert.Equal("8185558438", result.Phone);
        Assert.NotNull(result.SummerEbtCases);
        var snapCase = Assert.Single(result.SummerEbtCases.Where(c => c.EbtCaseNumber == "SNAP-CO-001"));
        Assert.True(snapCase.IsCoLoaded);
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_WhenPhone8185558438_AndDc_ReturnsCoLoadedPendingHousehold()
    {
        var repo = CreateRepository("{0}@example.com", state: "dc");
        var identifier = HouseholdIdentifier.Phone("8185558438");

        var result = await repo.GetHouseholdByIdentifierAsync(identifier, FullPiiVisibility, UserIalLevel.None);

        Assert.NotNull(result);
        Assert.Equal("co-loaded-pending-id-proofing@example.com", result!.Email);
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
        Assert.Equal("APP-2025-01-100001", app.ApplicationNumber);
        Assert.Equal("CASE-100001", app.CaseNumber);
        Assert.Equal("1234", app.Last4DigitsOfCard);
        Assert.Equal(IssuanceType.SummerEbt, app.IssuanceType);
        Assert.NotNull(result.AddressOnFile);
        Assert.Equal("123 Main Street", result.AddressOnFile.StreetAddress1);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_CoNotActivatedScenario_HasApprovedCaseAndNotActivatedCard()
    {
        // Tester AC: CO NotActivated persona should have Approved application + NotActivated
        // card status so CO SelfServiceRules correctly hides Request Replacement CTA while
        // leaving Update Address visible.
        var email = "co-notactivated@example.com";

        var result = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility, UserIalLevel.IAL1plus);

        Assert.NotNull(result);
        var app = result.Applications.First();
        Assert.Equal(ApplicationStatus.Approved, app.ApplicationStatus);
        Assert.Equal(CardStatus.NotActivated, app.CardStatus);
        Assert.Equal(IssuanceType.SummerEbt, app.IssuanceType);
        Assert.Single(result.SummerEbtCases);
        Assert.Equal("NotActivated", result.SummerEbtCases[0].EbtCardStatus);
        Assert.Equal(IssuanceType.SummerEbt, result.SummerEbtCases[0].IssuanceType);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_CoDeactivatedByStateScenario_HasApprovedCaseAndDeactivatedByStateCard()
    {
        // Tester AC: CO DeactivatedByState persona should have Approved application +
        // DeactivatedByState card status so CO SelfServiceRules correctly hides Request
        // Replacement CTA while leaving Update Address visible.
        var email = "co-deactivatedbystate@example.com";

        var result = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility, UserIalLevel.IAL1plus);

        Assert.NotNull(result);
        var app = result.Applications.First();
        Assert.Equal(ApplicationStatus.Approved, app.ApplicationStatus);
        Assert.Equal(CardStatus.DeactivatedByState, app.CardStatus);
        Assert.Equal(IssuanceType.SummerEbt, app.IssuanceType);
        Assert.Single(result.SummerEbtCases);
        Assert.Equal("DeactivatedByState", result.SummerEbtCases[0].EbtCardStatus);
        Assert.Equal(IssuanceType.SummerEbt, result.SummerEbtCases[0].IssuanceType);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_CoActiveScenario_HasApprovedCaseAndActiveCard()
    {
        // Tester AC: CO Active persona is the standard happy path — both Update Address
        // and Request Replacement CTAs should be visible (Active is in CO
        // CardReplacement.AllowedCardStatuses).
        var email = "co-active@example.com";

        var result = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility, UserIalLevel.IAL1plus);

        Assert.NotNull(result);
        var app = result.Applications.First();
        Assert.Equal(ApplicationStatus.Approved, app.ApplicationStatus);
        Assert.Equal(CardStatus.Active, app.CardStatus);
        Assert.Equal(IssuanceType.SummerEbt, app.IssuanceType);
        Assert.Single(result.SummerEbtCases);
        Assert.Equal("Active", result.SummerEbtCases[0].EbtCardStatus);
        Assert.Equal(IssuanceType.SummerEbt, result.SummerEbtCases[0].IssuanceType);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_VerifiedScenario_HasStableApplicationNumbers()
    {
        // Application numbers must be stable across multiple reads to ensure
        // dashboard replacement links match ConfirmAddress guard lookups
        var email = "verified@example.com";

        var result1 = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility, UserIalLevel.IAL1plus);
        var result2 = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility, UserIalLevel.IAL1plus);

        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal(
            result1!.Applications.First().ApplicationNumber,
            result2!.Applications.First().ApplicationNumber);
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
        Assert.Equal("APP-2025-01-100001", app.ApplicationNumber);
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

    [Fact]
    public async Task GetHouseholdByEmailAsync_LargeFamilyScenario_HasSummerEbtCases()
    {
        var email = "largefamily@example.com";
        var result = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility, UserIalLevel.IAL1plus);

        Assert.NotNull(result);
        Assert.Equal(4, result.SummerEbtCases.Count);
        Assert.Equal("Michael", result.SummerEbtCases[0].ChildFirstName);
        Assert.Equal("Brown", result.SummerEbtCases[0].ChildLastName);
        Assert.Equal("Sarah", result.SummerEbtCases[1].ChildFirstName);
        Assert.Equal("David", result.SummerEbtCases[2].ChildFirstName);
        Assert.Equal("Emily", result.SummerEbtCases[3].ChildFirstName);
        Assert.All(result.SummerEbtCases, c =>
        {
            Assert.Equal("NSLP", c.EligibilityType);
            Assert.Equal(IssuanceType.SummerEbt, c.IssuanceType);
            Assert.Equal(ApplicationStatus.Approved, c.ApplicationStatus);
            Assert.NotNull(c.SummerEBTCaseID);
        });
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_CoLoadedScenario_HasSummerEbtCases()
    {
        var email = "co-loaded@example.com";
        var result = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility, UserIalLevel.IAL1plus);

        Assert.NotNull(result);
        Assert.Equal(2, result.SummerEbtCases.Count);

        var sophiaCase = result.SummerEbtCases.First(c => c.ChildFirstName == "Sophia");
        Assert.Equal("Martinez", sophiaCase.ChildLastName);
        Assert.Equal("SNAP", sophiaCase.EligibilityType);
        Assert.Equal(IssuanceType.SnapEbtCard, sophiaCase.IssuanceType);

        var jamesCase = result.SummerEbtCases.First(c => c.ChildFirstName == "James");
        Assert.Equal("Martinez", jamesCase.ChildLastName);
        Assert.Equal("TANF", jamesCase.EligibilityType);
        Assert.Equal(IssuanceType.TanfEbtCard, jamesCase.IssuanceType);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_VerifiedScenario_HasSummerEbtCases()
    {
        var email = "verified@example.com";
        var result = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility, UserIalLevel.IAL1plus);

        Assert.NotNull(result);
        Assert.Equal(2, result.SummerEbtCases.Count);

        var johnCase = result.SummerEbtCases.First(c => c.ChildFirstName == "John");
        Assert.Equal("Doe", johnCase.ChildLastName);
        Assert.Equal("Application", johnCase.EligibilityType);
        Assert.Equal(IssuanceType.SnapEbtCard, johnCase.IssuanceType);
        Assert.NotNull(johnCase.BenefitAvailableDate);
        Assert.True(johnCase.BenefitAvailableDate >= new DateTime(2026, 6, 15));
        Assert.True(johnCase.BenefitAvailableDate <= new DateTime(2026, 6, 30));

        var janeCase = result.SummerEbtCases.First(c => c.ChildFirstName == "Jane");
        Assert.Equal("Application", janeCase.EligibilityType);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_ExpiredScenario_HasSummerEbtCaseWithExpiredBenefits()
    {
        var email = "expired@example.com";
        var result = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility, UserIalLevel.IAL1plus);

        Assert.NotNull(result);
        Assert.Single(result.SummerEbtCases);
        var sebtCase = result.SummerEbtCases[0];
        Assert.Equal("Deactivated", sebtCase.EbtCardStatus);
        Assert.NotNull(sebtCase.BenefitExpirationDate);
        Assert.True(sebtCase.BenefitExpirationDate < _timeProvider.GetUtcNow().UtcDateTime);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_SingleChildScenario_HasSummerEbtCases()
    {
        var email = "singlechild@example.com";
        var result = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility, UserIalLevel.IAL1plus);

        Assert.NotNull(result);
        Assert.Equal(2, result.SummerEbtCases.Count);
        Assert.Contains(result.SummerEbtCases, c => c.EligibilityType == "Medicaid");
        Assert.Contains(result.SummerEbtCases, c => c.EligibilityType == "Application");
    }

    [Theory]
    [InlineData("review@example.com", "NSLP", IssuanceType.SummerEbt)]
    [InlineData("non-co-loaded@example.com", "TANF", IssuanceType.TanfEbtCard)]
    [InlineData("not-started@example.com", "NSLP", IssuanceType.SummerEbt)]
    public async Task GetHouseholdByEmailAsync_ScenarioWithOneCase_HasSummerEbtCase(
        string email,
        string expectedEligibilityType,
        IssuanceType expectedIssuanceType)
    {
        var result = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility, UserIalLevel.IAL1plus);

        Assert.NotNull(result);
        Assert.Single(result.SummerEbtCases);
        var sebtCase = result.SummerEbtCases[0];
        Assert.Equal(expectedEligibilityType, sebtCase.EligibilityType);
        Assert.Equal(expectedIssuanceType, sebtCase.IssuanceType);
        Assert.NotNull(sebtCase.SummerEBTCaseID);
    }

    [Theory]
    [InlineData("pending@example.com")]
    [InlineData("minimal@example.com")]
    [InlineData("denied@example.com")]
    [InlineData("cancelled@example.com")]
    [InlineData("unknown@example.com")]
    public async Task GetHouseholdByEmailAsync_ScenarioWithNoCases_HasEmptySummerEbtCases(string email)
    {
        var result = await _repository.GetHouseholdByEmailAsync(email, FullPiiVisibility, UserIalLevel.IAL1plus);

        Assert.NotNull(result);
        Assert.Empty(result.SummerEbtCases);
    }

    [Theory]
    [InlineData("simple1")]
    [InlineData("simple2")]
    [InlineData("simple3")]
    [InlineData("simple4")]
    [InlineData("simple5")]
    [InlineData("simple6")]
    [InlineData("simple7")]
    public async Task GetHouseholdByEmailAsync_DcSimpleScenarios_HaveSummerEbtCases(string scenarioName)
    {
        // Arrange
        const string pattern = "sebt.dc+{0}@codeforamerica.org";
        var repo = CreateRepository(pattern, state: "dc");
        var email = string.Format(pattern, scenarioName);

        // Act
        var result = await repo.GetHouseholdByEmailAsync(email, FullPiiVisibility, UserIalLevel.IAL1plus);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.SummerEbtCases);
        var sebtCase = result.SummerEbtCases[0];
        Assert.Equal("NSLP", sebtCase.EligibilityType);
        Assert.Equal(IssuanceType.SummerEbt, sebtCase.IssuanceType);
        Assert.NotNull(sebtCase.SummerEBTCaseID);
        Assert.NotEmpty(sebtCase.ChildFirstName);
        Assert.NotEmpty(sebtCase.ChildLastName);
    }
}
