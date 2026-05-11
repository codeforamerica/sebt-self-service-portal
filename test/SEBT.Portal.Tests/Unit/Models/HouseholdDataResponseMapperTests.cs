using SEBT.Portal.Api.Models.Household;
using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Tests.Unit.Models;

/// <summary>
/// Unit tests for HouseholdDataResponseMapper.
/// </summary>
public class HouseholdDataResponseMapperTests
{
    [Fact]
    public void ToResponse_MapsAllHouseholdDataProperties_WhenFullyPopulated()
    {
        // Arrange
        var benefitIssue = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var benefitExpiry = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);
        var cardRequested = new DateTime(2025, 11, 11, 12, 0, 0, DateTimeKind.Utc);
        var cardMailed = new DateTime(2025, 11, 28, 12, 0, 0, DateTimeKind.Utc);
        var cardActivated = new DateTime(2025, 12, 2, 12, 0, 0, DateTimeKind.Utc);

        var domain = new HouseholdData
        {
            Email = "user@example.com",
            Phone = "555-1234",
            BenefitIssuanceType = BenefitIssuanceType.SnapEbtCard,
            Applications = new List<Application>
            {
                new Application
                {
                    ApplicationNumber = "APP-123",
                    CaseNumber = "CASE-456",
                    ApplicationStatus = ApplicationStatus.Approved,
                    IssuanceType = IssuanceType.SnapEbtCard,
                    BenefitIssueDate = benefitIssue,
                    BenefitExpirationDate = benefitExpiry,
                    Last4DigitsOfCard = "1234",
                    CardStatus = CardStatus.Active,
                    CardRequestedAt = cardRequested,
                    CardMailedAt = cardMailed,
                    CardActivatedAt = cardActivated,
                    CardDeactivatedAt = null,
                    Children = new List<Child>
                    {
                        new Child { FirstName = "John", LastName = "Doe" },
                        new Child { FirstName = "Jane", LastName = "Doe" }
                    }
                }
            },
            AddressOnFile = new Address
            {
                StreetAddress1 = "123 Main St",
                StreetAddress2 = "Apt 4B",
                City = "Denver",
                State = "CO",
                PostalCode = "80202"
            },
            UserProfile = new UserProfile
            {
                FirstName = "Jane",
                MiddleName = "Marie",
                LastName = "Doe"
            }
        };

        // Act
        var response = domain.ToResponse();

        // Assert - top level
        Assert.NotNull(response);
        Assert.Equal("user@example.com", response.Email);
        Assert.Equal("555-1234", response.Phone);
        Assert.Equal(BenefitIssuanceType.SnapEbtCard, response.BenefitIssuanceType);
        Assert.NotNull(response.SummerEbtCases);
        Assert.Empty(response.SummerEbtCases);
        Assert.NotNull(response.Applications);
        Assert.Single(response.Applications);

        // Assert - address
        Assert.NotNull(response.AddressOnFile);
        Assert.Equal("123 Main St", response.AddressOnFile.StreetAddress1);
        Assert.Equal("Apt 4B", response.AddressOnFile.StreetAddress2);
        Assert.Equal("Denver", response.AddressOnFile.City);
        Assert.Equal("CO", response.AddressOnFile.State);
        Assert.Equal("80202", response.AddressOnFile.PostalCode);

        // Assert - application
        var app = response.Applications[0];
        Assert.Equal("APP-123", app.ApplicationNumber);
        Assert.Equal("CASE-456", app.CaseNumber);
        Assert.Equal(ApplicationStatus.Approved, app.ApplicationStatus);
        Assert.Equal(IssuanceType.SnapEbtCard, app.IssuanceType);
        Assert.Equal(benefitIssue, app.BenefitIssueDate);
        Assert.Equal(benefitExpiry, app.BenefitExpirationDate);
        Assert.Equal("1234", app.Last4DigitsOfCard);
        Assert.Equal(CardStatus.Active, app.CardStatus);
        Assert.Equal(cardRequested, app.CardRequestedAt);
        Assert.Equal(cardMailed, app.CardMailedAt);
        Assert.Equal(cardActivated, app.CardActivatedAt);
        Assert.Null(app.CardDeactivatedAt);
        Assert.Equal(2, app.ChildrenOnApplication);
        Assert.Equal(2, app.Children.Count);
        Assert.Equal("John", app.Children[0].FirstName);
        Assert.Equal("Doe", app.Children[0].LastName);
        Assert.Equal(ApplicationStatus.Unknown, app.Children[0].Status);
        Assert.Equal("Jane", app.Children[1].FirstName);
        Assert.Equal("Doe", app.Children[1].LastName);
        Assert.Equal(ApplicationStatus.Unknown, app.Children[1].Status);

        // Assert - user profile
        Assert.NotNull(response.UserProfile);
        Assert.Equal("Jane", response.UserProfile.FirstName);
        Assert.Equal("Marie", response.UserProfile.MiddleName);
        Assert.Equal("Doe", response.UserProfile.LastName);
    }

    [Fact]
    public void ToResponse_HandlesNullAddressOnFile()
    {
        // Arrange
        var domain = new HouseholdData
        {
            Email = "user@example.com",
            Phone = null,
            Applications = new List<Application>(),
            AddressOnFile = null,
            UserProfile = null
        };

        // Act
        var response = domain.ToResponse();

        // Assert
        Assert.NotNull(response);
        Assert.Equal("user@example.com", response.Email);
        Assert.Null(response.Phone);
        Assert.NotNull(response.Applications);
        Assert.Empty(response.Applications);
        Assert.Null(response.AddressOnFile);
        Assert.Null(response.UserProfile);
    }

    [Fact]
    public void ToResponse_HandlesEmptyApplicationsAndChildren()
    {
        // Arrange
        var domain = new HouseholdData
        {
            Email = "empty@example.com",
            Applications = new List<Application>
            {
                new Application
                {
                    ApplicationNumber = "APP-001",
                    ApplicationStatus = ApplicationStatus.Pending,
                    Children = new List<Child>()
                }
            }
        };

        // Act
        var response = domain.ToResponse();

        // Assert
        Assert.NotNull(response);
        Assert.Single(response.Applications);
        var app = response.Applications[0];
        Assert.Equal("APP-001", app.ApplicationNumber);
        Assert.Equal(ApplicationStatus.Pending, app.ApplicationStatus);
        Assert.Equal(0, app.ChildrenOnApplication);
        Assert.NotNull(app.Children);
        Assert.Empty(app.Children);
    }

    [Fact]
    public void ToResponse_MapsMultipleApplications()
    {
        // Arrange
        var domain = new HouseholdData
        {
            Email = "multi@example.com",
            Applications = new List<Application>
            {
                new Application
                {
                    ApplicationNumber = "APP-1",
                    ApplicationStatus = ApplicationStatus.Approved,
                    Children = new List<Child> { new Child { FirstName = "A", LastName = "One" } }
                },
                new Application
                {
                    ApplicationNumber = "APP-2",
                    ApplicationStatus = ApplicationStatus.Pending,
                    Children = new List<Child> { new Child { FirstName = "B", LastName = "Two" } }
                }
            }
        };

        // Act
        var response = domain.ToResponse();

        // Assert
        Assert.NotNull(response);
        Assert.Equal(2, response.Applications.Count);
        Assert.Equal("APP-1", response.Applications[0].ApplicationNumber);
        Assert.Equal(ApplicationStatus.Approved, response.Applications[0].ApplicationStatus);
        Assert.Single(response.Applications[0].Children);
        Assert.Equal("A", response.Applications[0].Children[0].FirstName);
        Assert.Equal("APP-2", response.Applications[1].ApplicationNumber);
        Assert.Equal(ApplicationStatus.Pending, response.Applications[1].ApplicationStatus);
        Assert.Single(response.Applications[1].Children);
        Assert.Equal("B", response.Applications[1].Children[0].FirstName);
    }

    [Fact]
    public void ToResponse_MapsSummerEbtCases()
    {
        var domain = new HouseholdData
        {
            Email = "user@example.com",
            Applications = new List<Application>(),
            SummerEbtCases = new List<SummerEbtCase>
            {
                new SummerEbtCase
                {
                    SummerEBTCaseID = "CASE-001",
                    ChildFirstName = "Maria",
                    ChildLastName = "Garcia",
                    ChildDateOfBirth = new DateTime(2015, 5, 15),
                    ApplicationStatus = ApplicationStatus.Approved,
                    EbtCardLastFour = "1234",
                    EbtCardBalance = 120.50m,
                    EbtCaseNumber = "CBMS-REF",
                    CaseDisplayNumber = "APP-DISPLAY"
                }
            }
        };

        var response = domain.ToResponse();

        Assert.NotNull(response);
        Assert.Single(response.SummerEbtCases);
        var sec = response.SummerEbtCases[0];
        Assert.Equal("CASE-001", sec.SummerEBTCaseID);
        Assert.Equal("Maria", sec.ChildFirstName);
        Assert.Equal("Garcia", sec.ChildLastName);
        Assert.Equal(new DateTime(2015, 5, 15), sec.ChildDateOfBirth);
        Assert.Equal(ApplicationStatus.Approved, sec.ApplicationStatus);
        Assert.Equal("1234", sec.EbtCardLastFour);
        Assert.Equal(120.50m, sec.EbtCardBalance);
        Assert.Equal("CBMS-REF", sec.EbtCaseNumber);
        Assert.Equal("APP-DISPLAY", sec.CaseDisplayNumber);
    }

    [Fact]
    public void ToResponse_PassesHashedAppIdThrough_WhenProvided()
    {
        var domain = new HouseholdData
        {
            Email = "test@example.com",
            SummerEbtCases = new List<SummerEbtCase>(),
            Applications = new List<Application>()
        };

        var response = domain.ToResponse(hashedAppId: "ca383d90647e371547d6e66297cda8089b81fc1c5cb30da6cfcbdf744d9e2861");

        Assert.Equal("ca383d90647e371547d6e66297cda8089b81fc1c5cb30da6cfcbdf744d9e2861", response.HashedAppId);
    }

    [Fact]
    public void ToResponse_DefaultsHashedAppIdToNull_WhenNotProvided()
    {
        var domain = new HouseholdData
        {
            Email = "test@example.com",
            SummerEbtCases = new List<SummerEbtCase>(),
            Applications = new List<Application>()
        };

        var response = domain.ToResponse();

        Assert.Null(response.HashedAppId);
    }
}
