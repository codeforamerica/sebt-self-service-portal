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
            Applications = new List<Application>
            {
                new Application
                {
                    ApplicationNumber = "APP-123",
                    CaseNumber = "CASE-456",
                    ApplicationStatus = ApplicationStatus.Approved,
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
                        new Child { CaseNumber = 1001, FirstName = "John", LastName = "Doe" },
                        new Child { CaseNumber = 1002, FirstName = "Jane", LastName = "Doe" }
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
        Assert.Equal(1001, app.Children[0].CaseNumber);
        Assert.Equal("John", app.Children[0].FirstName);
        Assert.Equal("Doe", app.Children[0].LastName);
        Assert.Equal(1002, app.Children[1].CaseNumber);
        Assert.Equal("Jane", app.Children[1].FirstName);
        Assert.Equal("Doe", app.Children[1].LastName);

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
}
