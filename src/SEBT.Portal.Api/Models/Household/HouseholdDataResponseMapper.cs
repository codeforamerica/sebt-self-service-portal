extern alias Core;

namespace SEBT.Portal.Api.Models.Household;

using Address = Core::SEBT.Portal.Core.Models.Household.Address;
using Application = Core::SEBT.Portal.Core.Models.Household.Application;
using Child = Core::SEBT.Portal.Core.Models.Household.Child;
using AllowedActions = Core::SEBT.Portal.Core.Models.Household.AllowedActions;
using HouseholdData = Core::SEBT.Portal.Core.Models.Household.HouseholdData;
using SummerEbtCase = Core::SEBT.Portal.Core.Models.Household.SummerEbtCase;
using UserProfile = Core::SEBT.Portal.Core.Models.Household.UserProfile;

/// <summary>
/// Maps domain household models to API response DTOs.
/// </summary>
public static class HouseholdDataResponseMapper
{
    /// <summary>
    /// Maps domain HouseholdData to the API response model.
    /// </summary>
    public static HouseholdDataResponse ToResponse(this HouseholdData domain)
    {
        return new HouseholdDataResponse
        {
            Email = domain.Email,
            Phone = domain.Phone,
            SummerEbtCases = domain.SummerEbtCases.Select(ToResponse).ToList(),
            Applications = domain.Applications.Select(a => ToResponse(a, domain.BenefitIssuanceType)).ToList(),
            AddressOnFile = domain.AddressOnFile?.ToResponse(),
            UserProfile = domain.UserProfile?.ToResponse(),
            BenefitIssuanceType = domain.BenefitIssuanceType,
            AllowedActions = domain.AllowedActions?.ToResponse()
        };
    }

    private static SummerEbtCaseResponse ToResponse(this SummerEbtCase domain)
    {
        return new SummerEbtCaseResponse
        {
            SummerEBTCaseID = domain.SummerEBTCaseID,
            ApplicationId = domain.ApplicationId,
            ApplicationStudentId = domain.ApplicationStudentId,
            ChildFirstName = domain.ChildFirstName,
            ChildLastName = domain.ChildLastName,
            ChildDateOfBirth = domain.ChildDateOfBirth,
            HouseholdType = domain.HouseholdType,
            EligibilityType = domain.EligibilityType,
            EligibilitySource = domain.EligibilitySource,
            IssuanceType = domain.IssuanceType,
            ApplicationDate = domain.ApplicationDate,
            ApplicationStatus = domain.ApplicationStatus,
            MailingAddress = domain.MailingAddress?.ToResponse(),
            EbtCaseNumber = domain.EbtCaseNumber,
            EbtCardLastFour = domain.EbtCardLastFour,
            EbtCardStatus = domain.EbtCardStatus,
            EbtCardIssueDate = domain.EbtCardIssueDate,
            EbtCardBalance = domain.EbtCardBalance,
            CardRequestedAt = domain.CardRequestedAt,
            BenefitAvailableDate = domain.BenefitAvailableDate,
            BenefitExpirationDate = domain.BenefitExpirationDate,
            // Per-case gating is driven by the per-case evaluator result; co-loaded
            // cases are always denied regardless of rules config (handled elsewhere).
            AllowAddressChange = (domain.AllowedActions?.CanUpdateAddress ?? false) && !domain.IsCoLoaded,
            AllowCardReplacement = (domain.AllowedActions?.CanRequestReplacementCard ?? false) && !domain.IsCoLoaded
        };
    }

    private static ApplicationResponse ToResponse(
        this Application domain,
        Core::SEBT.Portal.Core.Models.Household.BenefitIssuanceType householdIssuanceType)
    {
        // If application-level IssuanceType isn't set, inherit from the
        // household-level BenefitIssuanceType (both enums share the same int values)
        var issuanceType = domain.IssuanceType != Core::SEBT.Portal.Core.Models.Household.IssuanceType.Unknown
            ? domain.IssuanceType
            : (Core::SEBT.Portal.Core.Models.Household.IssuanceType)(int)householdIssuanceType;

        return new ApplicationResponse
        {
            ApplicationNumber = domain.ApplicationNumber,
            CaseNumber = domain.CaseNumber,
            ApplicationStatus = domain.ApplicationStatus,
            ApplicationDate = domain.ApplicationDate,
            BenefitIssueDate = domain.BenefitIssueDate,
            BenefitExpirationDate = domain.BenefitExpirationDate,
            Last4DigitsOfCard = domain.Last4DigitsOfCard,
            CardStatus = domain.CardStatus,
            CardRequestedAt = domain.CardRequestedAt,
            CardMailedAt = domain.CardMailedAt,
            CardActivatedAt = domain.CardActivatedAt,
            CardDeactivatedAt = domain.CardDeactivatedAt,
            Children = domain.Children.Select(ToResponse).ToList(),
            ChildrenOnApplication = domain.ChildrenOnApplication,
            IssuanceType = issuanceType
        };
    }

    private static ChildResponse ToResponse(this Child domain)
    {
        return new ChildResponse
        {
            FirstName = domain.FirstName,
            LastName = domain.LastName,
            Status = domain.Status
        };
    }

    private static AddressResponse ToResponse(this Address domain)
    {
        return new AddressResponse
        {
            StreetAddress1 = domain.StreetAddress1,
            StreetAddress2 = domain.StreetAddress2,
            City = domain.City,
            State = domain.State,
            PostalCode = domain.PostalCode
        };
    }

    private static UserProfileResponse ToResponse(this UserProfile domain)
    {
        return new UserProfileResponse
        {
            FirstName = domain.FirstName,
            MiddleName = domain.MiddleName,
            LastName = domain.LastName
        };
    }

    private static AllowedActionsResponse ToResponse(this AllowedActions domain)
    {
        return new AllowedActionsResponse
        {
            CanUpdateAddress = domain.CanUpdateAddress,
            CanRequestReplacementCard = domain.CanRequestReplacementCard,
            AddressUpdateDeniedMessageKey = domain.AddressUpdateDeniedMessageKey,
            CardReplacementDeniedMessageKey = domain.CardReplacementDeniedMessageKey
        };
    }
}
