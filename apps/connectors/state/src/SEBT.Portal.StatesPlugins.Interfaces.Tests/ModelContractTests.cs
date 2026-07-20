using System.Reflection;
using Xunit;
using SEBT.Portal.StatesPlugins.Interfaces.Data;
using SEBT.Portal.StatesPlugins.Interfaces.Data.Cases;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

namespace SEBT.Portal.StatesPlugins.Interfaces.Tests;

/// <summary>
/// Contract tests for model type shape.
///  Ensures expected public properties exist so consumers and serialization don't break accidentally.
/// </summary>
public class ModelContractTests
{
    private static string[] GetPublicInstancePropertyNames(Type type)
    {
        return type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToArray();
    }

    [Fact]
    public void StateMetadata_has_expected_properties()
    {
        var names = GetPublicInstancePropertyNames(typeof(StateMetadata));
        Assert.Contains("Name", names);
        Assert.Single(names);
    }

    [Fact]
    public void SummerEbtCase_has_expected_properties()
    {
        var names = GetPublicInstancePropertyNames(typeof(SummerEbtCase));
        var expected = new[]
        {
            "ApplicationDate", "ApplicationId", "ApplicationStatus", "ApplicationStudentId",
            "BenefitAvailableDate", "BenefitExpirationDate", "CaseDisplayNumber", "ChildDateOfBirth",
            "ChildFirstName", "ChildLastName", "EbtCardBalance", "EbtCardIssueDate", "EbtCardLastFour",
            "EbtCardStatus", "EbtCaseNumber", "EligibilityType", "HouseholdType",
            "EligibilitySource", "IsCoLoaded", "IsStreamlineCertified", "IssuanceType",
            "MailingAddress", "SummerEBTCaseID"
        };
        foreach (var name in expected)
            Assert.Contains(name, names);
        Assert.Equal(expected.Length, names.Length);
    }

    [Fact]
    public void HouseholdData_has_expected_properties()
    {
        var names = GetPublicInstancePropertyNames(typeof(HouseholdData));
        Assert.Contains("Email", names);
        Assert.Contains("Phone", names);
        Assert.Contains("SummerEbtCases", names);
        Assert.Contains("Applications", names);
        Assert.Contains("AddressOnFile", names);
        Assert.Contains("UserProfile", names);
        Assert.Contains("BenefitIssuanceType", names);
        Assert.Equal(7, names.Length);
    }

    [Fact]
    public void Address_has_expected_properties()
    {
        var names = GetPublicInstancePropertyNames(typeof(Address));
        var expected = new[] { "City", "PostalCode", "State", "StreetAddress1", "StreetAddress2" };
        Assert.Equal(expected, names);
    }

    [Fact]
    public void Application_has_expected_properties()
    {
        var names = GetPublicInstancePropertyNames(typeof(Application));
        Assert.Contains("ApplicationNumber", names);
        Assert.Contains("CaseNumber", names);
        Assert.Contains("ApplicationStatus", names);
        Assert.Contains("BenefitIssueDate", names);
        Assert.Contains("BenefitExpirationDate", names);
        Assert.Contains("Children", names);
        Assert.Contains("ChildrenOnApplication", names);
        Assert.Contains("IssuanceType", names);
        Assert.Contains("ApplicationDate", names);
        Assert.Equal(9, names.Length);
    }

    [Fact]
    public void UserProfile_has_expected_properties()
    {
        var names = GetPublicInstancePropertyNames(typeof(UserProfile));
        var expected = new[] { "FirstName", "LastName", "MiddleName" };
        Assert.Equal(expected, names);
    }

    [Fact]
    public void Child_has_expected_properties()
    {
        var names = GetPublicInstancePropertyNames(typeof(Child));
        var expected = new[] { "FirstName", "LastName", "Status" };
        Assert.Equal(expected, names);
    }

    [Fact]
    public void CardReplacementRequest_has_expected_properties()
    {
        var names = GetPublicInstancePropertyNames(typeof(CardReplacementRequest));
        var expected = new[] { "CaseRefs", "HouseholdIdentifierValue", "Reason" };
        Assert.Equal(expected, names);
    }

    [Fact]
    public void CaseRef_has_expected_properties()
    {
        var names = GetPublicInstancePropertyNames(typeof(CaseRef));
        var expected = new[] { "ApplicationId", "ApplicationStudentId", "SummerEbtCaseId" };
        Assert.Equal(expected, names);
    }

    [Fact]
    public void CardReplacementResult_has_expected_properties()
    {
        var names = GetPublicInstancePropertyNames(typeof(CardReplacementResult));
        var expected = new[] { "ErrorCode", "ErrorMessage", "IsPolicyRejection", "IsSuccess" };
        Assert.Equal(expected, names);
    }
}
