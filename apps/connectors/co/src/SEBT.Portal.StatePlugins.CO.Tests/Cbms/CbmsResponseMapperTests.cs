using Microsoft.Extensions.Logging;
using SEBT.Portal.StatePlugins.CO.Cbms;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Data.Cases;
using SEBT.Portal.StatesPlugins.Interfaces.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

namespace SEBT.Portal.StatePlugins.CO.Tests.Cbms;

public class CbmsResponseMapperTests
{
    [Fact]
    public void MapToHouseholdData_empty_students_returns_household_with_empty_cases()
    {
        var response = new GetAccountDetailsResponse { StdntEnrollDtls = new List<GetAccountStudentDetail>() };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: true);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        Assert.NotNull(result);
        Assert.Equal("8185551234", result.Phone);
        Assert.Empty(result.SummerEbtCases);
        Assert.Empty(result.Applications);
        Assert.Null(result.AddressOnFile);
        Assert.Null(result.UserProfile);
    }

    [Fact]
    public void MapToHouseholdData_null_students_treats_as_empty()
    {
        var response = new GetAccountDetailsResponse { StdntEnrollDtls = null };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        Assert.NotNull(result);
        Assert.Empty(result.SummerEbtCases);
    }

    [Fact]
    public void MapToHouseholdData_respects_PiiVisibility_IncludePhone_false()
    {
        var student = CreateMinimalStudent();
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { student }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        Assert.Null(result.Phone);
    }

    [Fact]
    public void MapToHouseholdData_respects_PiiVisibility_IncludeAddress_false()
    {
        var student = CreateMinimalStudent();
        student.AddrLn1 = "123 Main St";
        student.Cty = "Denver";
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { student }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: true, IncludePhone: true);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        Assert.Null(result.AddressOnFile);
        Assert.All(result.SummerEbtCases, c => Assert.Null(c.MailingAddress));
    }

    [Fact]
    public void MapToHouseholdData_maps_student_to_summer_ebt_case()
    {
        var student = CreateMinimalStudent();
        student.SebtChldId = 1001;
        student.SebtChldCwin = 5001001;
        student.SebtAppId = 2001;
        student.StdFstNm = "Jane";
        student.StdLstNm = "Doe";
        student.StdDob = "2015-03-15";
        student.StdntEligSts = "AP";
        student.EligSrc = "CBMS";
        student.SebtAppSts = "PW";
        student.CbmsCsId = "case-123";
        student.EbtCardLastFour = "4321";
        student.EbtCardSts = "active";
        student.BenAvalDt = "2025-06-01";
        student.BenExpDt = "2025-08-31";
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { student }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: true);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        var @case = Assert.Single(result.SummerEbtCases);
        Assert.Equal("5001001", @case.SummerEBTCaseID);
        Assert.Equal("2001", @case.ApplicationId);
        Assert.Equal("Jane", @case.ChildFirstName);
        Assert.Equal("Doe", @case.ChildLastName);
        Assert.Equal(new DateOnly(2015, 3, 15), @case.ChildDateOfBirth);
        Assert.Equal(ApplicationStatus.Approved, @case.ApplicationStatus);
        Assert.Equal("case-123", @case.EbtCaseNumber);
        Assert.Equal("2001", @case.CaseDisplayNumber);
        Assert.Equal("4321", @case.EbtCardLastFour);
        Assert.Equal(CardStatus.Active, @case.EbtCardStatus);
        Assert.False(@case.IsStreamlineCertified);
    }

    [Fact]
    public void MapToHouseholdData_streamline_case_uses_cbms_case_id_when_sebt_app_id_absent()
    {
        var student = CreateMinimalStudent();
        student.EligSrc = "DIRC";
        student.SebtAppId = null;
        student.CbmsCsId = "CBMS-CS-ONLY";
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { student }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        var @case = Assert.Single(result.SummerEbtCases);
        Assert.Equal("CBMS-CS-ONLY", @case.EbtCaseNumber);
        Assert.Equal("CBMS-CS-ONLY", @case.CaseDisplayNumber);
        Assert.Null(@case.ApplicationId);
    }

    [Fact]
    public void MapToHouseholdData_streamline_case_prefers_sebt_app_id_for_case_display_when_present()
    {
        var student = CreateMinimalStudent();
        student.EligSrc = "DIRC";
        student.SebtAppId = 7777;
        student.CbmsCsId = "STREAMLINE-CBMS";
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { student }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        var @case = Assert.Single(result.SummerEbtCases);
        Assert.Equal("STREAMLINE-CBMS", @case.EbtCaseNumber);
        Assert.Equal("7777", @case.CaseDisplayNumber);
        Assert.Null(@case.ApplicationId);
        Assert.True(@case.IsStreamlineCertified);
    }

    [Fact]
    public void MapToHouseholdData_application_based_uses_cbms_case_id_when_sebt_app_id_absent()
    {
        var student = CreateMinimalStudent();
        student.EligSrc = "CBMS";
        student.SebtAppId = null;
        student.StdntEligSts = "AP";
        student.CbmsCsId = "FALLBACK-CS";
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { student }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        var @case = Assert.Single(result.SummerEbtCases);
        Assert.Equal("FALLBACK-CS", @case.EbtCaseNumber);
        Assert.Equal("FALLBACK-CS", @case.CaseDisplayNumber);
    }

    [Theory]
    [InlineData("AI", ApplicationStatus.Pending)]
    [InlineData("PD", ApplicationStatus.Pending)]
    [InlineData("PG", ApplicationStatus.Pending)]
    [InlineData("PI", ApplicationStatus.Pending)]
    [InlineData("PN", ApplicationStatus.Pending)]
    [InlineData("PS", ApplicationStatus.Pending)]
    [InlineData("PW", ApplicationStatus.Pending)]
    [InlineData("RC", ApplicationStatus.Pending)]
    [InlineData("AM", ApplicationStatus.Unknown)]
    [InlineData("DU", ApplicationStatus.Unknown)]
    [InlineData("XYZZY", ApplicationStatus.Unknown)]
    [InlineData("", ApplicationStatus.Unknown)]
    [InlineData(null, ApplicationStatus.Unknown)]
    public void MapToHouseholdData_maps_application_status(string? sebtAppSts, ApplicationStatus expected)
    {
        var student = CreateMinimalStudent();
        student.EligSrc = "CBMS";
        student.SebtAppSts = sebtAppSts;
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { student }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        var app = Assert.Single(result.Applications);
        Assert.Equal(expected, app.ApplicationStatus);
    }

    [Theory]
    [InlineData("AP", ApplicationStatus.Approved)]
    [InlineData("DE", ApplicationStatus.Denied)]
    [InlineData("OT", ApplicationStatus.Denied)]
    [InlineData("AI", ApplicationStatus.Pending)]
    [InlineData("PD", ApplicationStatus.Pending)]
    [InlineData("PE", ApplicationStatus.Pending)]
    [InlineData("PG", ApplicationStatus.Pending)]
    [InlineData("PS", ApplicationStatus.Pending)]
    [InlineData("AM", ApplicationStatus.Unknown)]
    [InlineData("XYZZY", ApplicationStatus.Unknown)]
    [InlineData("", ApplicationStatus.Unknown)]
    [InlineData(null, ApplicationStatus.Unknown)]
    public void MapToHouseholdData_maps_case_status_from_stdntEligSts(string? stdntEligSts, ApplicationStatus expected)
    {
        var student = CreateMinimalStudent();
        student.StdntEligSts = stdntEligSts;
        // Use DIRC so the student always appears in cases regardless of application status
        student.EligSrc = "DIRC";
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { student }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        var @case = Assert.Single(result.SummerEbtCases);
        Assert.Equal(expected, @case.ApplicationStatus);
    }

    [Fact]
    public void MapToHouseholdData_MapAddress_returns_null_when_AddrLn1_and_Cty_both_empty()
    {
        var student = CreateMinimalStudent();
        student.AddrLn1 = null;
        student.AddrLn2 = null;
        student.Cty = null;
        student.StaCd = null;
        student.Zip = null;
        student.Zip4 = null;
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { student }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: true, IncludeEmail: false, IncludePhone: false);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        Assert.Null(result.AddressOnFile);
    }

    [Fact]
    public void MapToHouseholdData_FormatPostalCode_returns_null_when_zip_null_even_if_zip4_present()
    {
        var student = CreateMinimalStudent();
        student.AddrLn1 = "123 Main St";
        student.Cty = "Denver";
        student.Zip = null;
        student.Zip4 = "1234";
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { student }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: true, IncludeEmail: false, IncludePhone: false);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        Assert.NotNull(result.AddressOnFile);
        Assert.Null(result.AddressOnFile.PostalCode);
    }

    [Fact]
    public void MapToHouseholdData_builds_applications_grouped_by_app_id()
    {
        var s1 = CreateMinimalStudent();
        s1.SebtAppId = 1001;
        s1.StdFstNm = "Child1";
        s1.EligSrc = "CBMS";
        var s2 = CreateMinimalStudent();
        s2.SebtAppId = 1001;
        s2.StdFstNm = "Child2";
        s2.EligSrc = "CBMS";
        var s3 = CreateMinimalStudent();
        s3.SebtAppId = 2002;
        s3.StdFstNm = "Child3";
        s3.EligSrc = "PK";
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { s1, s2, s3 }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        Assert.Equal(2, result.Applications.Count);
        var app1 = result.Applications.First(a => a.ApplicationNumber == "1001");
        Assert.Equal("1001", app1.CaseNumber);
        Assert.Equal(2, app1.Children.Count);
        var app2 = result.Applications.First(a => a.ApplicationNumber == "2002");
        Assert.Equal("2002", app2.CaseNumber);
        Assert.Single(app2.Children);
    }

    [Fact]
    public void MapToHouseholdData_auto_eligible_child_goes_to_cases_only()
    {
        var student = CreateMinimalStudent();
        student.EligSrc = "DIRC";
        student.StdFstNm = "AutoChild";
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { student }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        Assert.Single(result.SummerEbtCases);
        Assert.Equal("AutoChild", result.SummerEbtCases[0].ChildFirstName);
        Assert.Empty(result.Applications);
        Assert.True(result.SummerEbtCases[0].IsStreamlineCertified);
        Assert.False(result.SummerEbtCases[0].IsCoLoaded);
    }

    [Fact]
    public void MapToHouseholdData_application_child_pending_goes_to_applications_only()
    {
        var student = CreateMinimalStudent();
        student.EligSrc = "CBMS";
        student.SebtAppSts = "RC";
        student.StdntEligSts = "PE";
        student.StdFstNm = "AppChild";
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { student }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        Assert.Empty(result.SummerEbtCases);
        var app = Assert.Single(result.Applications);
        var child = Assert.Single(app.Children);
        Assert.Equal("AppChild", child.FirstName);
        Assert.Equal(ApplicationStatus.Pending, app.ApplicationStatus);
        Assert.Equal(IssuanceType.SummerEbt, app.IssuanceType);
    }

    [Fact]
    public void MapToHouseholdData_approved_application_child_goes_to_both_collections()
    {
        var student = CreateMinimalStudent();
        student.EligSrc = "CBMS";
        student.SebtAppSts = "PW";
        student.StdntEligSts = "AP";
        student.StdFstNm = "ApprovedChild";
        student.EbtCardLastFour = "9999";
        student.EbtCardSts = "ACTIVE";
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { student }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        var caseRecord = Assert.Single(result.SummerEbtCases);
        Assert.Equal("ApprovedChild", caseRecord.ChildFirstName);
        Assert.Equal("9999", caseRecord.EbtCardLastFour);
        Assert.Equal(IssuanceType.SummerEbt, caseRecord.IssuanceType);

        var app = Assert.Single(result.Applications);
        var child = Assert.Single(app.Children);
        Assert.Equal("ApprovedChild", child.FirstName);
        Assert.Equal(ApplicationStatus.Pending, app.ApplicationStatus);
        Assert.Equal(IssuanceType.SummerEbt, app.IssuanceType);
    }

    [Fact]
    public void MapToHouseholdData_unknown_eligsrc_treated_as_case()
    {
        var student = CreateMinimalStudent();
        student.EligSrc = "SOMETHING_NEW";
        student.StdFstNm = "UnknownChild";
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { student }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        Assert.Single(result.SummerEbtCases);
        Assert.Equal("UnknownChild", result.SummerEbtCases[0].ChildFirstName);
        Assert.Empty(result.Applications);
        Assert.True(result.SummerEbtCases[0].IsStreamlineCertified);
    }

    [Fact]
    public void MapToHouseholdData_DIRC_auto_eligible_sets_household_benefit_issuance_and_case()
    {
        var student = CreateMinimalStudent();
        student.EligSrc = "DIRC";
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { student }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        Assert.Equal(BenefitIssuanceType.SummerEbt, result.BenefitIssuanceType);
        Assert.Single(result.SummerEbtCases);
    }

    [Theory]
    [InlineData("ACTIVE", CardStatus.Active)]
    [InlineData("LOST", CardStatus.Lost)]
    [InlineData("LOST, AUTO REISSUE", CardStatus.Lost)]
    [InlineData("STOLEN", CardStatus.Stolen)]
    [InlineData("DAMAGED", CardStatus.Damaged)]
    [InlineData("STATUSED BY STATE, NO REISSUE", CardStatus.DeactivatedByState)]
    [InlineData("DEACTIVATED BY STATE", CardStatus.DeactivatedByState)]
    [InlineData("NOT ACTIVATED", CardStatus.NotActivated)]
    [InlineData("FROZEN", CardStatus.Frozen)]
    [InlineData("UNDELIVERABLE", CardStatus.Undeliverable)]
    [InlineData("active", CardStatus.Active)]
    [InlineData("undeliverable", CardStatus.Undeliverable)]
    [InlineData("", CardStatus.Unknown)]
    [InlineData(null, CardStatus.Unknown)]
    public void MapCardStatus_DcSpecValues_MapsCorrectly(string? ebtCardSts, CardStatus expected)
    {
        var student = CreateMinimalStudent();
        student.EbtCardSts = ebtCardSts;
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { student }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        var caseRecord = Assert.Single(result.SummerEbtCases);
        Assert.Equal(expected, caseRecord.EbtCardStatus);
    }

    [Theory]
    [InlineData("DAMAGED, AUTO REISSUE")]
    [InlineData("RETURNED")]
    [InlineData("DEACTIVATED")]
    [InlineData("DEACTIVATED, NO REISSUE")]
    [InlineData("DEACTIVATED/CANCELLED")]
    [InlineData("CANCELED BY PRIMARY NO REISSUE")]
    [InlineData("UNAUTHORIZED USE, NO REISSUE")]
    [InlineData("COMPROMISED, NO REISSUE")]
    [InlineData("SOMETHING WHOLLY NOVEL")]
    public void MapCardStatus_UnmappedToken_ReturnsUnknownAndLogsError(string raw)
    {
        var student = CreateMinimalStudent();
        student.EbtCardSts = raw;
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { student }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);
        var logger = new CapturingLogger();

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility, logger);

        var caseRecord = Assert.Single(result.SummerEbtCases);
        Assert.Equal(CardStatus.Unknown, caseRecord.EbtCardStatus);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains(raw, entry.Message);
    }

    [Fact]
    public void MapToHouseholdData_auto_eligible_case_has_no_application_reference()
    {
        var student = CreateMinimalStudent();
        student.EligSrc = "DIRC";
        student.SebtAppId = 5001;
        student.SebtChldId = 9001;
        student.SebtChldCwin = 7009001;
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { student }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        var caseRecord = Assert.Single(result.SummerEbtCases);
        Assert.Null(caseRecord.ApplicationId);
        Assert.Null(caseRecord.ApplicationStudentId);
        Assert.Equal("7009001", caseRecord.SummerEBTCaseID);
        Assert.Equal("5001", caseRecord.CaseDisplayNumber);
    }

    [Fact]
    public void MapToHouseholdData_logs_unmapped_card_status_tokens()
    {
        var student = CreateMinimalStudent();
        student.EbtCardSts = "STATUSED_BY_STATE_NO_REISSUE";
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { student }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);
        var logger = new CapturingLogger();

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility, logger);

        var caseRecord = Assert.Single(result.SummerEbtCases);
        Assert.Equal(CardStatus.Unknown, caseRecord.EbtCardStatus);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains("STATUSED_BY_STATE_NO_REISSUE", entry.Message);
    }

    [Fact]
    public void MapToHouseholdData_does_not_log_when_card_status_maps_cleanly()
    {
        var student = CreateMinimalStudent();
        student.EbtCardSts = "ACTIVE";
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { student }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);
        var logger = new CapturingLogger();

        CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility, logger);

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void MapToHouseholdData_does_not_log_for_null_or_empty_card_status()
    {
        var student = CreateMinimalStudent();
        student.EbtCardSts = null;
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { student }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);
        var logger = new CapturingLogger();

        CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility, logger);

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void MapToHouseholdData_populates_child_status_from_stdntEligSts()
    {
        var approved = CreateMinimalStudent();
        approved.EligSrc = "CBMS";
        approved.SebtAppId = 1001;
        approved.StdntEligSts = "AP";
        approved.StdFstNm = "ApprovedChild";

        var denied = CreateMinimalStudent();
        denied.EligSrc = "CBMS";
        denied.SebtAppId = 1001;
        denied.StdntEligSts = "DE";
        denied.StdFstNm = "DeniedChild";

        var pending = CreateMinimalStudent();
        pending.EligSrc = "CBMS";
        pending.SebtAppId = 1001;
        pending.StdntEligSts = "PE";
        pending.StdFstNm = "PendingChild";

        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { approved, denied, pending }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        var app = Assert.Single(result.Applications);
        Assert.Equal(3, app.Children.Count);

        var approvedChild = app.Children.First(c => c.FirstName == "ApprovedChild");
        Assert.Equal(ApplicationStatus.Approved, approvedChild.Status);

        var deniedChild = app.Children.First(c => c.FirstName == "DeniedChild");
        Assert.Equal(ApplicationStatus.Denied, deniedChild.Status);

        var pendingChild = app.Children.First(c => c.FirstName == "PendingChild");
        Assert.Equal(ApplicationStatus.Pending, pendingChild.Status);
    }

    [Fact]
    public void MapToHouseholdData_excludes_denied_duplicate_from_cases_and_applications()
    {
        var duplicate = CreateMinimalStudent();
        duplicate.StdFstNm = "Yoko";
        duplicate.StdLstNm = "Alden";
        duplicate.StdntEligSts = "DD";
        duplicate.SebtAppSts = "DU";
        duplicate.EligSrc = "PK";

        var active = CreateMinimalStudent();
        active.StdFstNm = "Yoko";
        active.StdLstNm = "Alden";
        active.StdntEligSts = "AP";
        active.EligSrc = "DIRC";
        active.SebtChldCwin = duplicate.SebtChldCwin;

        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = [duplicate, active]
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        var @case = Assert.Single(result.SummerEbtCases);
        Assert.Equal("Yoko", @case.ChildFirstName);
        Assert.Equal(ApplicationStatus.Approved, @case.ApplicationStatus);
        Assert.Empty(result.Applications);
    }

    [Fact]
    public void MapToHouseholdData_excludes_denied_duplicate_from_application_children()
    {
        var duplicate = CreateMinimalStudent();
        duplicate.StdFstNm = "Yoko";
        duplicate.StdLstNm = "Alden";
        duplicate.StdntEligSts = "DD";
        duplicate.SebtAppSts = "DU";
        duplicate.EligSrc = "PK";
        duplicate.SebtAppId = 1129264;

        var denied = CreateMinimalStudent();
        denied.StdFstNm = "Lucienne";
        denied.StdLstNm = "Hayman";
        denied.StdntEligSts = "DE";
        denied.SebtAppSts = "PI";
        denied.EligSrc = "PK";
        denied.SebtAppId = 1129264;

        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = [duplicate, denied]
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        Assert.Empty(result.SummerEbtCases);
        var app = Assert.Single(result.Applications);
        var child = Assert.Single(app.Children);
        Assert.Equal("Lucienne", child.FirstName);
        Assert.Equal(ApplicationStatus.Denied, child.Status);
    }

    [Fact]
    public void MapToHouseholdData_uses_first_actionable_row_for_household_metadata_when_leading_row_is_denied_duplicate()
    {
        var duplicate = CreateMinimalStudent();
        duplicate.StdntEligSts = "DD";
        duplicate.EligSrc = "PK";
        duplicate.GurdFstNm = "Wrong";
        duplicate.GurdLstNm = "Guardian";
        duplicate.GurdEmailAddr = "wrong@example.org";

        var actionable = CreateMinimalStudent();
        actionable.GurdFstNm = "Della";
        actionable.GurdLstNm = "MockAlden";
        actionable.GurdEmailAddr = "della@example.org";
        actionable.AddrLn1 = "475 TIMM WAY";
        actionable.Cty = "LONGMONT";

        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = [duplicate, actionable]
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: false);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        Assert.NotNull(result.UserProfile);
        Assert.Equal("Della", result.UserProfile.FirstName);
        Assert.Equal("MockAlden", result.UserProfile.LastName);
        Assert.Equal("della@example.org", result.Email);
        Assert.NotNull(result.AddressOnFile);
        Assert.Equal("475 TIMM WAY", result.AddressOnFile.StreetAddress1);
    }

    [Fact]
    public void MapToHouseholdData_logs_when_excluding_denied_duplicate_rows()
    {
        var duplicate = CreateMinimalStudent();
        duplicate.StdntEligSts = "DD";
        duplicate.EligSrc = "PK";

        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = [duplicate]
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);
        var logger = new CapturingLogger();

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility, logger);

        Assert.Empty(result.SummerEbtCases);
        Assert.Empty(result.Applications);
        var log = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, log.Level);
        Assert.Contains("denied-duplicate", log.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static GetAccountStudentDetail CreateMinimalStudent()
    {
        return new GetAccountStudentDetail
        {
            SebtChldId = 1,
            SebtChldCwin = 100001,
            SebtAppId = 1,
            StdFstNm = "First",
            StdLstNm = "Last",
            StdDob = "2010-01-01",
            EligSrc = "DIRC"
        };
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
