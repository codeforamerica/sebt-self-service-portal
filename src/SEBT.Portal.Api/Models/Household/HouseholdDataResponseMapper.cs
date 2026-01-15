using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Api.Models.Household;

/// <summary>
/// Maps domain household models to API response DTOs.
/// </summary>
public static class HouseholdDataResponseMapper
{
    /// <summary>
    /// Maps domain HouseholdData (flat model) to the API response model.
    /// </summary>
    public static HouseholdDataResponse ToResponse(this HouseholdData domain)
    {
        return new HouseholdDataResponse
        {
            Email = domain.Email,
            Phone = domain.Phone,
            Applications = new[] { MapToApplicationResponse(domain) },
            AddressOnFile = domain.AddressOnFile?.ToResponse(),
            UserProfile = domain.UserProfile?.ToResponse(),
            BenefitIssuanceType = domain.BenefitIssuanceType
        };
    }

    private static ApplicationResponse MapToApplicationResponse(HouseholdData domain)
    {
        return new ApplicationResponse
        {
            ApplicationNumber = domain.ApplicationNumber,
            CaseNumber = domain.CaseNumber,
            ApplicationStatus = domain.ApplicationStatus,
            BenefitIssueDate = domain.BenefitIssueDate,
            BenefitExpirationDate = domain.BenefitExpirationDate,
            Last4DigitsOfCard = domain.Last4DigitsOfCard,
            CardStatus = CardStatus.Requested,
            CardRequestedAt = null,
            CardMailedAt = null,
            CardActivatedAt = null,
            CardDeactivatedAt = null,
            Children = domain.Children.Select(ToResponse).ToList(),
            ChildrenOnApplication = domain.ChildrenOnApplication,
            IssuanceType = (IssuanceType)domain.BenefitIssuanceType
        };
    }

    private static ChildResponse ToResponse(this Child domain)
    {
        return new ChildResponse
        {
            CaseNumber = null,
            FirstName = domain.FirstName,
            LastName = domain.LastName
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
}
