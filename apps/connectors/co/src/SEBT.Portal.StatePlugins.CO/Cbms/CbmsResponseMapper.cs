using Microsoft.Extensions.Logging;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;
using SEBT.Portal.StatesPlugins.Interfaces.Data.Cases;
using HouseholdAddress = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.Address;

namespace SEBT.Portal.StatePlugins.CO.Cbms;

/// <summary>
/// Maps CBMS Get Account Details response to the portal's HouseholdData model.
/// </summary>
/// <remarks>
/// <c>stdntEligSts</c> and <c>sebtAppSts</c> use 2-letter CBMS status codes (case-insensitive).
/// <c>ebtCardSts</c> uses full-word values from the CBMS OpenAPI examples (case-insensitive).
/// </remarks>
internal static class CbmsResponseMapper
{
    public static HouseholdData MapToHouseholdData(
        GetAccountDetailsResponse response,
        string queryPhone,
        PiiVisibility piiVisibility,
        ILogger? logger = null)
    {
        // Denied-duplicate rows (stdntEligSts=DD) must never reach cases, applications, or household metadata.
        var rawStudents = response.StdntEnrollDtls;
        var students = rawStudents is null or { Count: 0 }
            ? []
            : rawStudents.Where(s => !CbmsCaseFilters.IsDeniedDuplicate(s)).ToList();

        var excludedCount = (rawStudents?.Count ?? 0) - students.Count;
        if (excludedCount > 0)
        {
            logger?.LogInformation(
                "CBMS GetAccountDetails: excluded {ExcludedCount} denied-duplicate enrollment row(s) " +
                "(stdntEligSts=DD) from household mapping.",
                excludedCount);
        }

        var first = students.FirstOrDefault();

        var household = new HouseholdData
        {
            Phone = piiVisibility.IncludePhone ? queryPhone : null,
            Email = piiVisibility.IncludeEmail && first != null ? first.GurdEmailAddr ?? string.Empty : string.Empty,
            AddressOnFile = first != null && piiVisibility.IncludeAddress ? MapAddress(first) : null,
            UserProfile = first != null ? new UserProfile
            {
                FirstName = first.GurdFstNm ?? string.Empty,
                LastName = first.GurdLstNm ?? string.Empty
            } : null,
            BenefitIssuanceType = BenefitIssuanceType.SummerEbt,
            SummerEbtCases = BuildCases(students, piiVisibility, logger),
            Applications = BuildApplications(students)
        };

        return household;
    }

    private static string? FormatPostalCode(string? zip, string? zip4)
    {
        if (string.IsNullOrWhiteSpace(zip)) return null;
        return string.IsNullOrWhiteSpace(zip4) ? zip : $"{zip}-{zip4}";
    }

    private static HouseholdAddress? MapAddress(GetAccountStudentDetail s)
    {
        if (string.IsNullOrEmpty(s.AddrLn1) && string.IsNullOrEmpty(s.Cty))
            return null;
        return new HouseholdAddress
        {
            StreetAddress1 = s.AddrLn1,
            StreetAddress2 = s.AddrLn2,
            City = s.Cty,
            State = s.StaCd,
            PostalCode = FormatPostalCode(s.Zip, s.Zip4)
        };
    }

    private static SummerEbtCase MapToSummerEbtCase(GetAccountStudentDetail s, PiiVisibility piiVisibility, ILogger? logger)
    {
        var isApplicationBased = EligibilitySourceClassifier.IsApplicationBased(s.EligSrc);
        var cbmsCaseId = s.CbmsCsId;
        // Guardian-facing reference: CO shows sebtAppId whenever present (any eligibility origin); otherwise CBMS case id.
        var displayReferenceId = s.SebtAppId?.ToString() ?? cbmsCaseId;

        return new SummerEbtCase
        {
            SummerEBTCaseID = s.SebtChldCwin?.ToString(),
            ApplicationId = isApplicationBased
                ? s.SebtAppId?.ToString() : null,
            ApplicationStudentId = isApplicationBased
                ? s.SebtChldId?.ToString() : null,
            ChildFirstName = s.StdFstNm ?? string.Empty,
            ChildLastName = s.StdLstNm ?? string.Empty,
            ChildDateOfBirth = ParseDateOnly(s.StdDob) ?? DateOnly.MinValue,
            HouseholdType = "SEBT",
            EligibilityType = s.StdntEligSts ?? string.Empty,
            IssuanceType = IssuanceType.SummerEbt,
            ApplicationStatus = MapCaseStatus(s.StdntEligSts),
            MailingAddress = piiVisibility.IncludeAddress ? MapAddress(s) : null,
            EbtCaseNumber = cbmsCaseId,
            CaseDisplayNumber = displayReferenceId,
            EbtCardLastFour = s.EbtCardLastFour,
            EbtCardStatus = MapCardStatus(s.EbtCardSts, logger),
            EbtCardIssueDate = ParseDateOnly(s.CardIssDt),
            EbtCardBalance = s.CardBal.HasValue ? (decimal)s.CardBal.Value : null,
            BenefitAvailableDate = ParseDateOnly(s.BenAvalDt),
            BenefitExpirationDate = ParseDateOnly(s.BenExpDt),
            IsStreamlineCertified = !isApplicationBased,
        };
    }

    /// <summary>
    /// Builds the Cases collection. A child is a case if:
    /// - Auto-eligible (EligSrc = DIRC or CDE) — always a case
    /// - Unknown EligSrc (null/empty/unrecognized) — treated as auto-eligible
    /// - Application-based (EligSrc = CBMS or PK) AND case status is approved (stdntEligSts = AP)
    /// </summary>
    private static List<SummerEbtCase> BuildCases(
        List<GetAccountStudentDetail> students,
        PiiVisibility piiVisibility,
        ILogger? logger)
    {
        return students
            .Where(s => !EligibilitySourceClassifier.IsApplicationBased(s.EligSrc)
                      || MapCaseStatus(s.StdntEligSts) == ApplicationStatus.Approved)
            .Select(s => MapToSummerEbtCase(s, piiVisibility, logger))
            .ToList();
    }

    /// <summary>
    /// Builds the Applications collection. Only rows where EligSrc indicates
    /// an actual application was submitted (CBMS or PK), grouped by SebtAppId.
    /// </summary>
    private static List<Application> BuildApplications(List<GetAccountStudentDetail> students)
    {
        var applicationRows = students
            .Where(s => EligibilitySourceClassifier.IsApplicationBased(s.EligSrc))
            .Where(s => s.SebtAppId != null)
            .GroupBy(s => s.SebtAppId!);

        return applicationRows.Select(g =>
        {
            var first = g.First();
            var sebtAppIdText = first.SebtAppId!.Value.ToString();
            return new Application
            {
                ApplicationNumber = sebtAppIdText,
                CaseNumber = sebtAppIdText,
                ApplicationStatus = MapApplicationStatus(first.SebtAppSts),
                IssuanceType = IssuanceType.SummerEbt,
                Children = g.Select(c => new Child
                {
                    FirstName = c.StdFstNm ?? string.Empty,
                    LastName = c.StdLstNm ?? string.Empty,
                    Status = MapCaseStatus(c.StdntEligSts)
                }).ToList()
            };
        }).ToList();
    }

    /// <summary>
    /// Maps the CBMS case/eligibility status code (<c>stdntEligSts</c>) to a portal ApplicationStatus.
    /// These 2-letter codes represent the case-level determination (approved, denied, pending).
    /// </summary>
    private static ApplicationStatus MapCaseStatus(string? stdntEligSts)
    {
        if (string.IsNullOrEmpty(stdntEligSts)) return ApplicationStatus.Unknown;
        return stdntEligSts.ToUpperInvariant() switch
        {
            "AP" => ApplicationStatus.Approved,
            "DE" => ApplicationStatus.Denied,
            "OT" => ApplicationStatus.Denied,
            "AI" => ApplicationStatus.Pending,
            "PD" => ApplicationStatus.Pending,
            "PE" => ApplicationStatus.Pending,
            "PG" => ApplicationStatus.Pending,
            "PS" => ApplicationStatus.Pending,
            _ => ApplicationStatus.Unknown
        };
    }

    /// <summary>
    /// Maps the CBMS application processing status code (<c>sebtAppSts</c>) to a portal ApplicationStatus.
    /// These 2-letter codes represent application processing state — all known codes are in-process.
    /// </summary>
    private static ApplicationStatus MapApplicationStatus(string? sebtAppSts)
    {
        if (string.IsNullOrEmpty(sebtAppSts)) return ApplicationStatus.Unknown;
        return sebtAppSts.ToUpperInvariant() switch
        {
            "AI" => ApplicationStatus.Pending,
            "PD" => ApplicationStatus.Pending,
            "PG" => ApplicationStatus.Pending,
            "PI" => ApplicationStatus.Pending,
            "PN" => ApplicationStatus.Pending,
            "PS" => ApplicationStatus.Pending,
            "PW" => ApplicationStatus.Pending,
            "RC" => ApplicationStatus.Pending,
            _ => ApplicationStatus.Unknown
        };
    }

    private static CardStatus MapCardStatus(string? ebtCardSts, ILogger? logger = null)
    {
        if (string.IsNullOrEmpty(ebtCardSts)) return CardStatus.Unknown;
        return ebtCardSts.ToUpperInvariant() switch
        {
            "ACTIVE" => CardStatus.Active,
            "LOST" => CardStatus.Lost,
            "LOST, AUTO REISSUE" => CardStatus.Lost,
            "STOLEN" => CardStatus.Stolen,
            "DAMAGED" => CardStatus.Damaged,
            "STATUSED BY STATE, NO REISSUE" => CardStatus.DeactivatedByState,
            "DEACTIVATED BY STATE" => CardStatus.DeactivatedByState,
            "NOT ACTIVATED" => CardStatus.NotActivated,
            "FROZEN" => CardStatus.Frozen,
            "UNDELIVERABLE" => CardStatus.Undeliverable,
            _ => LogAndReturnUnknown(ebtCardSts, logger)
        };
    }

    private static CardStatus LogAndReturnUnknown(string raw, ILogger? logger)
    {
        logger?.LogError(
            "CBMS returned unmapped ebtCardSts token {Token}; falling back to CardStatus.Unknown. " +
            "If this token represents a real status, add it to the status mapping table.",
            raw);
        return CardStatus.Unknown;
    }

    private static DateOnly? ParseDateOnly(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateOnly.TryParse(value, out var d) ? d : null;
    }

    private static DateTime? ParseDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateTime.TryParse(value, out var dt) ? dt : null;
    }
}
