using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Infrastructure.Repositories;

using InterfaceCardStatus = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CardStatus;
using InterfaceHouseholdData = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.HouseholdData;
using InterfaceSummerEbtCase = SEBT.Portal.StatesPlugins.Interfaces.Data.Cases.SummerEbtCase;

namespace SEBT.Portal.Tests.Unit.Repositories;

/// <summary>
/// Unit tests for PluginHouseholdDataMapper.
/// </summary>
public class PluginHouseholdDataMapperTests
{
    [Fact]
    public void ToCore_WhenSourceIsNull_ReturnsNull()
    {
        var result = PluginHouseholdDataMapper.ToCore(null);

        Assert.Null(result);
    }

    [Fact]
    public void ToCore_WhenSourceIsMinimalHouseholdData_MapsToCore()
    {
        var source = new HouseholdData
        {
            Email = "user@example.com",
            Phone = null,
            BenefitIssuanceType = BenefitIssuanceType.SummerEbt,
            Applications = new List<Application>(),
            AddressOnFile = null,
            UserProfile = null
        };

        var result = PluginHouseholdDataMapper.ToCore(source);

        Assert.NotNull(result);
        Assert.Equal("user@example.com", result.Email);
        Assert.Null(result.Phone);
        Assert.Equal(BenefitIssuanceType.SummerEbt, result.BenefitIssuanceType);
        Assert.Empty(result.Applications);
        Assert.Null(result.AddressOnFile);
        Assert.Null(result.UserProfile);
    }

    [Fact]
    public void ToCore_WhenSourceHasAddress_MapsAddress()
    {
        var source = new HouseholdData
        {
            Email = "a@b.com",
            AddressOnFile = new Address
            {
                StreetAddress1 = "123 Main St",
                StreetAddress2 = "Apt 4",
                City = "Washington",
                State = "DC",
                PostalCode = "20001"
            },
            Applications = new List<Application>()
        };

        var result = PluginHouseholdDataMapper.ToCore(source);

        Assert.NotNull(result?.AddressOnFile);
        Assert.Equal("123 Main St", result.AddressOnFile.StreetAddress1);
        Assert.Equal("Apt 4", result.AddressOnFile.StreetAddress2);
        Assert.Equal("Washington", result.AddressOnFile.City);
        Assert.Equal("DC", result.AddressOnFile.State);
        Assert.Equal("20001", result.AddressOnFile.PostalCode);
    }

    [Fact]
    public void ToCore_WhenSourceHasUserProfile_MapsUserProfile()
    {
        var source = new HouseholdData
        {
            Email = "a@b.com",
            UserProfile = new UserProfile
            {
                FirstName = "Jane",
                MiddleName = "M",
                LastName = "Doe"
            },
            Applications = new List<Application>()
        };

        var result = PluginHouseholdDataMapper.ToCore(source);

        Assert.NotNull(result?.UserProfile);
        Assert.Equal("Jane", result.UserProfile.FirstName);
        Assert.Equal("M", result.UserProfile.MiddleName);
        Assert.Equal("Doe", result.UserProfile.LastName);
    }

    [Fact]
    public void ToCore_WhenSourceHasApplicationsAndChildren_MapsNestedData()
    {
        var source = new HouseholdData
        {
            Email = "a@b.com",
            Applications = new List<Application>
            {
                new Application
                {
                    ApplicationNumber = "APP-001",
                    CaseNumber = "CASE-001",
                    ApplicationStatus = ApplicationStatus.Approved,
                    IssuanceType = IssuanceType.SummerEbt,
                    Children = new List<Child>
                    {
                        new Child { FirstName = "Maria", LastName = "Garcia" }
                    }
                }
            }
        };

        var result = PluginHouseholdDataMapper.ToCore(source);

        Assert.NotNull(result);
        Assert.Single(result.Applications);
        var app = result.Applications[0];
        Assert.Equal("APP-001", app.ApplicationNumber);
        Assert.Equal("CASE-001", app.CaseNumber);
        Assert.Equal(ApplicationStatus.Approved, app.ApplicationStatus);
        Assert.Equal(IssuanceType.SummerEbt, app.IssuanceType);
        Assert.Single(app.Children);
        Assert.Equal("Maria", app.Children[0].FirstName);
        Assert.Equal("Garcia", app.Children[0].LastName);
    }

    [Fact]
    public void ToCore_WhenSourceApplicationHasApplicationDate_MapsApplicationDate()
    {
        var source = new HouseholdData
        {
            Email = "a@b.com",
            Applications = new List<Application>
            {
                new Application
                {
                    ApplicationNumber = "APP-002",
                    ApplicationStatus = ApplicationStatus.Pending,
                    ApplicationDate = new DateTime(2026, 5, 15)
                }
            }
        };

        var result = PluginHouseholdDataMapper.ToCore(source);

        Assert.NotNull(result);
        Assert.Single(result.Applications);
        Assert.Equal(new DateTime(2026, 5, 15), result.Applications[0].ApplicationDate);
    }

    [Fact]
    public void ToCore_WhenSourceHasSummerEbtCases_MapsIssuanceType()
    {
        var source = new HouseholdData
        {
            Email = "a@b.com",
            Applications = new List<Application>(),
            SummerEbtCases = new List<SummerEbtCase>
            {
                new SummerEbtCase
                {
                    ChildFirstName = "Alex",
                    ChildLastName = "Rivera",
                    HouseholdType = "OSSE",
                    EligibilityType = "OSSE",
                    IssuanceType = IssuanceType.SnapEbtCard
                }
            }
        };

        var result = PluginHouseholdDataMapper.ToCore(source);

        Assert.NotNull(result);
        Assert.Single(result.SummerEbtCases);
        Assert.Equal(IssuanceType.SnapEbtCard, result.SummerEbtCases[0].IssuanceType);
    }

    [Fact]
    public void ToCore_WhenSourceHasSummerEbtCases_MapsCaseDisplayNumber()
    {
        var source = new HouseholdData
        {
            Email = "a@b.com",
            Applications = new List<Application>(),
            SummerEbtCases = new List<SummerEbtCase>
            {
                new SummerEbtCase
                {
                    ChildFirstName = "Alex",
                    ChildLastName = "Rivera",
                    HouseholdType = "OSSE",
                    EligibilityType = "OSSE",
                    IssuanceType = IssuanceType.SummerEbt,
                    EbtCaseNumber = "STATE-CASE",
                    CaseDisplayNumber = "SHOW-ME"
                }
            }
        };

        var result = PluginHouseholdDataMapper.ToCore(source);

        Assert.NotNull(result);
        Assert.Single(result.SummerEbtCases);
        Assert.Equal("STATE-CASE", result.SummerEbtCases[0].EbtCaseNumber);
        Assert.Equal("SHOW-ME", result.SummerEbtCases[0].CaseDisplayNumber);
    }

    [Fact]
    public void ToCore_WhenInterfaceSummerEbtCaseHasEbtCardStatus_ConvertsToCoreEnumByName()
    {
        // Source uses the StatesPlugins.Interfaces type so the reflective mapper exercises
        // the name-based enum conversion path.
        var source = new InterfaceHouseholdData
        {
            Email = "a@b.com",
            SummerEbtCases = new List<InterfaceSummerEbtCase>
            {
                new InterfaceSummerEbtCase
                {
                    ChildFirstName = "Alex",
                    ChildLastName = "Rivera",
                    ChildDateOfBirth = DateOnly.FromDateTime(DateTime.Today.AddYears(-10)),
                    HouseholdType = "OSSE",
                    EligibilityType = "OSSE",
                    EbtCardStatus = InterfaceCardStatus.Active
                }
            }
        };

        var core = PluginHouseholdDataMapper.ToCore(source);

        Assert.NotNull(core);
        Assert.Single(core!.SummerEbtCases);
        Assert.Equal(CardStatus.Active, core.SummerEbtCases[0].EbtCardStatus);
    }

    [Fact]
    public void ToCore_WhenInterfaceSummerEbtCaseEbtCardStatusIsNull_ReturnsNull()
    {
        var source = new InterfaceHouseholdData
        {
            Email = "a@b.com",
            SummerEbtCases = new List<InterfaceSummerEbtCase>
            {
                new InterfaceSummerEbtCase
                {
                    ChildFirstName = "Alex",
                    ChildLastName = "Rivera",
                    ChildDateOfBirth = DateOnly.FromDateTime(DateTime.Today.AddYears(-10)),
                    HouseholdType = "OSSE",
                    EligibilityType = "OSSE",
                    EbtCardStatus = null
                }
            }
        };

        var core = PluginHouseholdDataMapper.ToCore(source);

        Assert.NotNull(core);
        Assert.Single(core!.SummerEbtCases);
        Assert.Null(core.SummerEbtCases[0].EbtCardStatus);
    }
}
