using System.Collections;
using System.Reflection;
using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Infrastructure.Repositories;

/// <summary>
/// Maps household data from the state plugin (StatesPlugins.Interfaces) type to the portal Core type.
/// Uses reflection so we only reference Core types here and avoid type identity conflicts.
/// </summary>
internal static class PluginHouseholdDataMapper
{
    public static HouseholdData? ToCore(object? source)
    {
        if (source == null)
        {
            return null;
        }

        var t = source.GetType();
        return new HouseholdData
        {
            Email = GetProp<string>(t, source, nameof(HouseholdData.Email)) ?? string.Empty,
            Phone = GetProp<string>(t, source, nameof(HouseholdData.Phone)),
            BenefitIssuanceType = (BenefitIssuanceType)(GetProp(t, source, nameof(HouseholdData.BenefitIssuanceType)) ?? (int)BenefitIssuanceType.Unknown),
            AddressOnFile = ToCoreAddress(GetProp(t, source, nameof(HouseholdData.AddressOnFile))),
            UserProfile = ToCoreUserProfile(GetProp(t, source, nameof(HouseholdData.UserProfile))),
            SummerEbtCases = ToCoreSummerEbtCaseList(GetProp(t, source, nameof(HouseholdData.SummerEbtCases))),
            Applications = ToCoreApplicationList(GetProp(t, source, nameof(HouseholdData.Applications)))
        };
    }

    private static Address? ToCoreAddress(object? source)
    {
        if (source == null) return null;
        var t = source.GetType();
        return new Address
        {
            StreetAddress1 = GetProp<string>(t, source, nameof(Address.StreetAddress1)),
            StreetAddress2 = GetProp<string>(t, source, nameof(Address.StreetAddress2)),
            City = GetProp<string>(t, source, nameof(Address.City)),
            State = GetProp<string>(t, source, nameof(Address.State)),
            PostalCode = GetProp<string>(t, source, nameof(Address.PostalCode))
        };
    }

    private static UserProfile? ToCoreUserProfile(object? source)
    {
        if (source == null) return null;
        var t = source.GetType();
        return new UserProfile
        {
            FirstName = GetProp<string>(t, source, nameof(UserProfile.FirstName)) ?? string.Empty,
            MiddleName = GetProp<string>(t, source, nameof(UserProfile.MiddleName)),
            LastName = GetProp<string>(t, source, nameof(UserProfile.LastName)) ?? string.Empty
        };
    }

    private static List<SummerEbtCase> ToCoreSummerEbtCaseList(object? source)
    {
        if (source is not IEnumerable list) return new List<SummerEbtCase>();
        var result = new List<SummerEbtCase>();
        foreach (var item in list)
        {
            if (item != null)
            {
                result.Add(ToCoreSummerEbtCase(item));
            }
        }
        return result;
    }

    private static SummerEbtCase ToCoreSummerEbtCase(object source)
    {
        var t = source.GetType();
        return new SummerEbtCase
        {
            SummerEBTCaseID = GetProp<string>(t, source, "SummerEBTCaseID"),
            ApplicationId = GetProp<string>(t, source, "ApplicationId"),
            ApplicationStudentId = GetProp<string>(t, source, "ApplicationStudentId"),
            ChildFirstName = GetProp<string>(t, source, "ChildFirstName") ?? string.Empty,
            ChildLastName = GetProp<string>(t, source, "ChildLastName") ?? string.Empty,
            ChildDateOfBirth = ToDateTimeOrNull(GetProp(t, source, "ChildDateOfBirth")),
            HouseholdType = GetProp<string>(t, source, "HouseholdType") ?? string.Empty,
            EligibilityType = GetProp<string>(t, source, "EligibilityType") ?? string.Empty,
            ApplicationDate = ToDateTimeOrNull(GetProp(t, source, "ApplicationDate")),
            ApplicationStatus = (ApplicationStatus)(GetProp(t, source, "ApplicationStatus") ?? (int)ApplicationStatus.Unknown),
            MailingAddress = ToCoreAddress(GetProp(t, source, "MailingAddress")),
            EbtCaseNumber = GetProp<string>(t, source, "EbtCaseNumber"),
            EbtCardLastFour = GetProp<string>(t, source, "EbtCardLastFour"),
            EbtCardStatus = GetProp<string>(t, source, "EbtCardStatus"),
            EbtCardIssueDate = ToDateTimeOrNull(GetProp(t, source, "EbtCardIssueDate")),
            EbtCardBalance = GetProp<decimal?>(t, source, "EbtCardBalance"),
            IsCoLoaded = GetProp<bool>(t, source, "IsCoLoaded"),
            IsStreamlineCertified = GetProp<bool>(t, source, "IsStreamlineCertified"),
            CardRequestedAt = GetProp<DateTime?>(t, source, "CardRequestedAt"),
            BenefitAvailableDate = ToDateTimeOrNull(GetProp(t, source, "BenefitAvailableDate")),
            BenefitExpirationDate = ToDateTimeOrNull(GetProp(t, source, "BenefitExpirationDate")),
            IssuanceType = (IssuanceType)(GetProp(t, source, nameof(SummerEbtCase.IssuanceType)) ?? (int)IssuanceType.Unknown)
        };
    }

    private static DateTime? ToDateTimeOrNull(object? value)
    {
        if (value == null) return null;
        if (value is DateTime dt) return dt;
        if (value is DateOnly d) return d.ToDateTime(TimeOnly.MinValue);
        return null;
    }

    private static List<Application> ToCoreApplicationList(object? source)
    {
        if (source is not IEnumerable list) return new List<Application>();
        var result = new List<Application>();
        foreach (var item in list)
        {
            if (item != null)
            {
                result.Add(ToCoreApplication(item));
            }
        }
        return result;
    }

    private static Application ToCoreApplication(object source)
    {
        var t = source.GetType();
        var children = ToCoreChildList(GetProp(t, source, nameof(Application.Children)));
        return new Application
        {
            ApplicationNumber = GetProp<string>(t, source, nameof(Application.ApplicationNumber)),
            CaseNumber = GetProp<string>(t, source, nameof(Application.CaseNumber)),
            ApplicationStatus = (ApplicationStatus)(GetProp(t, source, nameof(Application.ApplicationStatus)) ?? (int)ApplicationStatus.Unknown),
            BenefitIssueDate = GetProp<DateTime?>(t, source, nameof(Application.BenefitIssueDate)),
            BenefitExpirationDate = GetProp<DateTime?>(t, source, nameof(Application.BenefitExpirationDate)),
            Last4DigitsOfCard = GetProp<string>(t, source, nameof(Application.Last4DigitsOfCard)),
            CardStatus = (CardStatus)(GetProp(t, source, nameof(Application.CardStatus)) ?? (int)CardStatus.Requested),
            CardRequestedAt = GetProp<DateTime?>(t, source, nameof(Application.CardRequestedAt)),
            CardMailedAt = GetProp<DateTime?>(t, source, nameof(Application.CardMailedAt)),
            CardActivatedAt = GetProp<DateTime?>(t, source, nameof(Application.CardActivatedAt)),
            CardDeactivatedAt = GetProp<DateTime?>(t, source, nameof(Application.CardDeactivatedAt)),
            IssuanceType = (IssuanceType)(GetProp(t, source, nameof(Application.IssuanceType)) ?? (int)IssuanceType.Unknown),
            Children = children
        };
    }

    private static List<Child> ToCoreChildList(object? source)
    {
        if (source is not IEnumerable list) return new List<Child>();
        var result = new List<Child>();
        foreach (var item in list)
        {
            if (item != null)
            {
                result.Add(ToCoreChild(item));
            }
        }
        return result;
    }

    private static Child ToCoreChild(object source)
    {
        var t = source.GetType();
        return new Child
        {
            FirstName = GetProp<string>(t, source, nameof(Child.FirstName)) ?? string.Empty,
            LastName = GetProp<string>(t, source, nameof(Child.LastName)) ?? string.Empty,
            Status = GetProp<ApplicationStatus>(t, source, nameof(Child.Status))
        };
    }

    private static T? GetProp<T>(Type type, object obj, string name)
    {
        var prop = type.GetProperty(name);
        if (prop == null) return default;
        var v = prop.GetValue(obj);
        if (v == null || (typeof(T).IsClass && v is not T)) return default;
        return (T)v;
    }

    private static object? GetProp(Type type, object obj, string name)
    {
        var prop = type.GetProperty(name);
        return prop?.GetValue(obj);
    }
}
