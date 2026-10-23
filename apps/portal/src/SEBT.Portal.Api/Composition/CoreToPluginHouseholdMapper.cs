using CoreAddress = SEBT.Portal.Core.Models.Household.Address;
using CoreApplication = SEBT.Portal.Core.Models.Household.Application;
using CoreCardStatus = SEBT.Portal.Core.Models.Household.CardStatus;
using CoreChild = SEBT.Portal.Core.Models.Household.Child;
using CoreHousehold = SEBT.Portal.Core.Models.Household.HouseholdData;
using CoreSummerEbtCase = SEBT.Portal.Core.Models.Household.SummerEbtCase;
using CoreUserProfile = SEBT.Portal.Core.Models.Household.UserProfile;
using PluginAddress = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.Address;
using PluginApplication = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.Application;
using PluginApplicationStatus = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.ApplicationStatus;
using PluginBenefitIssuanceType = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.BenefitIssuanceType;
using PluginCardStatus = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CardStatus;
using PluginChild = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.Child;
using PluginHousehold = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.HouseholdData;
using PluginIssuanceType = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.IssuanceType;
using PluginSummerEbtCase = SEBT.Portal.StatesPlugins.Interfaces.Data.Cases.SummerEbtCase;
using PluginUserProfile = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.UserProfile;

namespace SEBT.Portal.Api.Composition;

/// <summary>
/// Maps Core household data back to the plugin contract so
/// <see cref="FeatureGatedSummerEbtCaseService"/> can sit in front of
/// <c>ISummerEbtCaseService</c> without changing <c>HouseholdRepository</c>.
/// </summary>
internal static class CoreToPluginHouseholdMapper
{
    public static PluginHousehold ToPlugin(CoreHousehold source)
    {
        return new PluginHousehold
        {
            Email = source.Email ?? string.Empty,
            Phone = source.Phone,
            BenefitIssuanceType = (PluginBenefitIssuanceType)(int)source.BenefitIssuanceType,
            AddressOnFile = ToPluginAddress(source.AddressOnFile),
            UserProfile = ToPluginUserProfile(source.UserProfile),
            SummerEbtCases = source.SummerEbtCases.Select(ToPluginCase).ToList(),
            Applications = source.Applications.Select(ToPluginApplication).ToList(),
        };
    }

    private static PluginAddress? ToPluginAddress(CoreAddress? source)
    {
        if (source is null)
        {
            return null;
        }

        return new PluginAddress
        {
            StreetAddress1 = source.StreetAddress1,
            StreetAddress2 = source.StreetAddress2,
            City = source.City,
            State = source.State,
            PostalCode = source.PostalCode,
        };
    }

    private static PluginUserProfile? ToPluginUserProfile(CoreUserProfile? source)
    {
        if (source is null)
        {
            return null;
        }

        return new PluginUserProfile
        {
            FirstName = source.FirstName,
            MiddleName = source.MiddleName,
            LastName = source.LastName,
        };
    }

    private static PluginSummerEbtCase ToPluginCase(CoreSummerEbtCase source)
    {
        return new PluginSummerEbtCase
        {
            SummerEBTCaseID = source.SummerEBTCaseID,
            ApplicationId = source.ApplicationId,
            ApplicationStudentId = source.ApplicationStudentId,
            ChildFirstName = source.ChildFirstName,
            ChildLastName = source.ChildLastName,
            ChildDateOfBirth = ToDateOnly(source.ChildDateOfBirth),
            HouseholdType = source.HouseholdType,
            EligibilityType = source.EligibilityType,
            ApplicationDate = ToDateOnlyOrNull(source.ApplicationDate),
            ApplicationStatus = (PluginApplicationStatus)(int)source.ApplicationStatus,
            MailingAddress = ToPluginAddress(source.MailingAddress),
            EbtCaseNumber = source.EbtCaseNumber,
            CaseDisplayNumber = source.CaseDisplayNumber,
            EbtCardLastFour = source.EbtCardLastFour,
            EbtCardStatus = source.EbtCardStatus is CoreCardStatus cardStatus
                ? (PluginCardStatus)(int)cardStatus
                : null,
            EbtCardIssueDate = ToDateOnlyOrNull(source.EbtCardIssueDate),
            EbtCardBalance = source.EbtCardBalance,
            IsCoLoaded = source.IsCoLoaded,
            IsStreamlineCertified = source.IsStreamlineCertified,
            BenefitAvailableDate = ToDateOnlyOrNull(source.BenefitAvailableDate),
            BenefitExpirationDate = ToDateOnlyOrNull(source.BenefitExpirationDate),
            EligibilitySource = source.EligibilitySource,
            IssuanceType = (PluginIssuanceType)(int)source.IssuanceType,
        };
    }

    private static PluginApplication ToPluginApplication(CoreApplication source)
    {
        return new PluginApplication
        {
            ApplicationNumber = source.ApplicationNumber,
            CaseNumber = source.CaseNumber,
            ApplicationStatus = (PluginApplicationStatus)(int)source.ApplicationStatus,
            ApplicationDate = source.ApplicationDate,
            BenefitIssueDate = source.BenefitIssueDate,
            BenefitExpirationDate = source.BenefitExpirationDate,
            IssuanceType = (PluginIssuanceType)(int)source.IssuanceType,
            Children = source.Children.Select(ToPluginChild).ToList(),
        };
    }

    private static PluginChild ToPluginChild(CoreChild source)
    {
        return new PluginChild
        {
            FirstName = source.FirstName,
            LastName = source.LastName,
            Status = (PluginApplicationStatus)(int)source.Status,
        };
    }

    private static DateOnly ToDateOnly(DateTime? value) =>
        value.HasValue ? DateOnly.FromDateTime(value.Value) : default;

    private static DateOnly? ToDateOnlyOrNull(DateTime? value) =>
        value.HasValue ? DateOnly.FromDateTime(value.Value) : null;
}
