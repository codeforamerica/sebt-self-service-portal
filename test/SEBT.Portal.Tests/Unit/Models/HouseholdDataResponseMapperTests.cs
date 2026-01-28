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
        // Arrange - HouseholdData with application
        var benefitIssue = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var benefitExpiry = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);

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
            }
        };

        // Act
        var response = domain.ToResponse();

        // Assert - top level
        Assert.NotNull(response);
        Assert.Equal("user@example.com", response.Email);
        Assert.Equal("555-1234", response.Phone);
        Assert.Equal(BenefitIssuanceType.SnapEbtCard, response.BenefitIssuanceType);
        Assert.NotNull(response.Applications);
        Assert.Single(response.Applications);

        // Assert - address
        Assert.NotNull(response.AddressOnFile);
        Assert.Equal("123 Main St", response.AddressOnFile.StreetAddress1);
        Assert.Equal("Apt 4B", response.AddressOnFile.StreetAddress2);
        Assert.Equal("Denver", response.AddressOnFile.City);
        Assert.Equal("CO", response.AddressOnFile.State);
        Assert.Equal("80202", response.AddressOnFile.PostalCode);

        // Assert - single application
        var app = response.Applications[0];
        Assert.Equal("APP-123", app.ApplicationNumber);
        Assert.Equal("CASE-456", app.CaseNumber);
        Assert.Equal(ApplicationStatus.Approved, app.ApplicationStatus);
        Assert.Equal(IssuanceType.SnapEbtCard, app.IssuanceType);
        Assert.Equal(benefitIssue, app.BenefitIssueDate);
        Assert.Equal(benefitExpiry, app.BenefitExpirationDate);
        Assert.Equal("1234", app.Last4DigitsOfCard);
        Assert.Equal(2, app.ChildrenOnApplication);
        Assert.Equal(2, app.Children.Count);
        Assert.Null(app.Children[0].CaseNumber);
        Assert.Equal("John", app.Children[0].FirstName);
        Assert.Equal("Doe", app.Children[0].LastName);
        Assert.Equal("Jane", app.Children[1].FirstName);
        Assert.Equal("Doe", app.Children[1].LastName);

        // Flat model has no UserProfile; mapper sets null
        Assert.Null(response.UserProfile);
    }

    [Fact]
    public void ToResponse_HandlesNullAddressOnFile()
    {
        // Arrange - model with no address
        var domain = new HouseholdData
        {
            Email = "user@example.com",
            Phone = null,
            Applications = new List<Application>
            {
                new Application { ApplicationStatus = ApplicationStatus.Unknown, Children = new List<Child>() }
            },
            AddressOnFile = null
        };

        // Act
        var response = domain.ToResponse();

        // Assert
        Assert.NotNull(response);
        Assert.Equal("user@example.com", response.Email);
        Assert.Null(response.Phone);
        Assert.NotNull(response.Applications);
        Assert.Single(response.Applications); // one application view from flat data
        Assert.Null(response.AddressOnFile);
        Assert.Null(response.UserProfile);
    }

    [Fact]
    public void ToResponse_HandlesEmptyApplicationsAndChildren()
    {
        // Arrange - model with no children
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
    public void ToResponse_FlatModel_ProducesSingleApplicationInResponse()
    {
        // Arrange - model with one application
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
                }
            }
        };

        // Act
        var response = domain.ToResponse();

        // Assert - model produces one application in response
        Assert.NotNull(response);
        Assert.Single(response.Applications);
        Assert.Equal("APP-1", response.Applications[0].ApplicationNumber);
        Assert.Equal(ApplicationStatus.Approved, response.Applications[0].ApplicationStatus);
        Assert.Single(response.Applications[0].Children);
        Assert.Equal("A", response.Applications[0].Children[0].FirstName);
    }
}
