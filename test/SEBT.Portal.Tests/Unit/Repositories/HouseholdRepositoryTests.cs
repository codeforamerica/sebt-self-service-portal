using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Utilities;
using SEBT.Portal.Infrastructure.Repositories;
using ISummerEbtCaseService = SEBT.Portal.StatesPlugins.Interfaces.ISummerEbtCaseService;
using PluginHouseholdIdentifierType = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.HouseholdIdentifierType;
using PluginIdentityAssuranceLevel = SEBT.Portal.StatesPlugins.Interfaces.Models.IdentityAssuranceLevel;
using PluginPiiVisibility = SEBT.Portal.StatesPlugins.Interfaces.Models.PiiVisibility;
using PluginHouseholdData = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.HouseholdData;
using PluginApplication = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.Application;
using PluginChild = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.Child;
using PluginSummerEbtCase = SEBT.Portal.StatesPlugins.Interfaces.Data.Cases.SummerEbtCase;
using PluginAddress = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.Address;
using PluginApplicationStatus = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.ApplicationStatus;
using PluginCardStatus = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CardStatus;
using PluginIssuanceType = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.IssuanceType;
using PluginBenefitIssuanceType = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.BenefitIssuanceType;

namespace SEBT.Portal.Tests.Unit.Repositories;

/// <summary>
/// Unit tests for HouseholdRepository.
/// </summary>
public class HouseholdRepositoryTests
{
    private static readonly PiiVisibility FullPii = new(IncludeAddress: true, IncludeEmail: true, IncludePhone: true);
    private static readonly PiiVisibility NoAddressPii = new(IncludeAddress: false, IncludeEmail: true, IncludePhone: true);

    private readonly ISummerEbtCaseService _summerEbtCaseService;
    private readonly HouseholdRepository _repository;

    public HouseholdRepositoryTests()
    {
        _summerEbtCaseService = Substitute.For<ISummerEbtCaseService>();
        _repository = new HouseholdRepository(
            _summerEbtCaseService,
            NullLogger<HouseholdRepository>.Instance);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenPluginReturnsData_ReturnsMappedCoreHouseholdData()
    {
        var email = "guardian@example.com";
        var pluginData = new PluginHouseholdData
        {
            Email = email,
            Phone = "555-123-4567",
            BenefitIssuanceType = PluginBenefitIssuanceType.SummerEbt,
            Applications = new List<PluginApplication>
            {
                new PluginApplication
                {
                    ApplicationNumber = "APP-001",
                    CaseNumber = "CASE-001",
                    ApplicationStatus = PluginApplicationStatus.Approved,
                    Last4DigitsOfCard = "1234",
                    CardStatus = PluginCardStatus.Active,
                    IssuanceType = PluginIssuanceType.SummerEbt,
                    Children = new List<PluginChild>
                    {
                        new PluginChild { FirstName = "Maria", LastName = "Garcia" }
                    }
                }
            }
        };

        _summerEbtCaseService
            .GetHouseholdByIdentifierAsync(PluginHouseholdIdentifierType.Email, email, Arg.Any<PluginPiiVisibility>(), Arg.Any<PluginIdentityAssuranceLevel>(), Arg.Any<CancellationToken>())
            .Returns(pluginData);

        var result = await _repository.GetHouseholdByEmailAsync(email, FullPii, UserIalLevel.IAL1plus);

        Assert.NotNull(result);
        Assert.Equal(email, result.Email);
        Assert.Equal("555-123-4567", result.Phone);
        Assert.Equal(BenefitIssuanceType.SummerEbt, result.BenefitIssuanceType);
        Assert.Single(result.Applications);
        Assert.Equal("APP-001", result.Applications[0].ApplicationNumber);
        Assert.Single(result.Applications[0].Children);
        Assert.Equal("Maria", result.Applications[0].Children[0].FirstName);
    }

    [Fact]
    public async Task TryMatchCoLoadedGuardianByBenefitIdAndDobAsync_DelegatesToPlugin()
    {
        var dob = new DateOnly(2000, 1, 1);
        var userId = Guid.NewGuid();
        _summerEbtCaseService
            .TryMatchCoLoadedGuardianByBenefitIdAndDobAsync("IC1", dob, userId, Arg.Any<CancellationToken>())
            .Returns(true);

        var result = await _repository.TryMatchCoLoadedGuardianByBenefitIdAndDobAsync("IC1", dob, userId);

        Assert.True(result);
        await _summerEbtCaseService.Received(1).TryMatchCoLoadedGuardianByBenefitIdAndDobAsync(
            "IC1",
            dob,
            userId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHouseholdByBenefitIdentifierAndGuardianDobAsync_DelegatesToPlugin_AndMapsCore()
    {
        var loginEmail = "guardian@example.com";
        var dob = new DateOnly(1984, 3, 5);
        var userId = Guid.NewGuid();
        var pluginData = new PluginHouseholdData
        {
            Email = EmailNormalizer.Normalize(loginEmail),
            BenefitIssuanceType = PluginBenefitIssuanceType.SnapEbtCard,
            Applications = new List<PluginApplication>(),
            SummerEbtCases = new List<PluginSummerEbtCase>()
        };

        _summerEbtCaseService
            .GetHouseholdByBenefitIdentifierAndDobAsync(
                "IC000001",
                dob,
                EmailNormalizer.Normalize(loginEmail),
                Arg.Any<PluginPiiVisibility>(),
                PluginIdentityAssuranceLevel.IAL1plus,
                userId,
                Arg.Any<CancellationToken>())
            .Returns(pluginData);

        var result = await _repository.GetHouseholdByBenefitIdentifierAndGuardianDobAsync(
            loginEmail,
            "IC000001",
            dob,
            FullPii,
            UserIalLevel.IAL1plus,
            userId);

        Assert.NotNull(result);
        Assert.Equal(EmailNormalizer.Normalize(loginEmail), result!.Email);
        await _summerEbtCaseService.Received(1).GetHouseholdByBenefitIdentifierAndDobAsync(
            "IC000001",
            dob,
            EmailNormalizer.Normalize(loginEmail),
            Arg.Any<PluginPiiVisibility>(),
            PluginIdentityAssuranceLevel.IAL1plus,
            userId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenPluginReturnsSummerEbtCases_MapsToCore()
    {
        var email = "guardian@example.com";
        var pluginData = new PluginHouseholdData
        {
            Email = email,
            Phone = "555-123-4567",
            Applications = new List<PluginApplication>(),
            SummerEbtCases = new List<PluginSummerEbtCase>
            {
                new PluginSummerEbtCase
                {
                    SummerEBTCaseID = "CASE-001",
                    ApplicationId = "APP-001",
                    ChildFirstName = "Maria",
                    ChildLastName = "Garcia",
                    ChildDateOfBirth = new DateOnly(2015, 5, 15),
                    HouseholdType = "DirectCert",
                    EligibilityType = "SNAP",
                    ApplicationStatus = PluginApplicationStatus.Approved,
                    EbtCardLastFour = "1234",
                    EbtCardBalance = 120.50m,
                    MailingAddress = new PluginAddress
                    {
                        StreetAddress1 = "123 Main St",
                        City = "Denver",
                        State = "CO",
                        PostalCode = "80202"
                    }
                }
            }
        };

        _summerEbtCaseService
            .GetHouseholdByIdentifierAsync(PluginHouseholdIdentifierType.Email, email, Arg.Any<PluginPiiVisibility>(), Arg.Any<PluginIdentityAssuranceLevel>(), Arg.Any<CancellationToken>())
            .Returns(pluginData);

        var result = await _repository.GetHouseholdByEmailAsync(email, FullPii, UserIalLevel.IAL1plus);

        Assert.NotNull(result);
        Assert.Single(result.SummerEbtCases);
        var sec = result.SummerEbtCases[0];
        Assert.Equal("CASE-001", sec.SummerEBTCaseID);
        Assert.Equal("APP-001", sec.ApplicationId);
        Assert.Equal("Maria", sec.ChildFirstName);
        Assert.Equal("Garcia", sec.ChildLastName);
        Assert.Equal(new DateTime(2015, 5, 15), sec.ChildDateOfBirth);
        Assert.Equal("DirectCert", sec.HouseholdType);
        Assert.Equal("SNAP", sec.EligibilityType);
        Assert.Equal(ApplicationStatus.Approved, sec.ApplicationStatus);
        Assert.Equal("1234", sec.EbtCardLastFour);
        Assert.Equal(120.50m, sec.EbtCardBalance);
        Assert.NotNull(sec.MailingAddress);
        Assert.Equal("123 Main St", sec.MailingAddress.StreetAddress1);
        Assert.Equal("Denver", sec.MailingAddress.City);
        Assert.Equal("CO", sec.MailingAddress.State);
        Assert.Equal("80202", sec.MailingAddress.PostalCode);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenPluginReturnsNull_ReturnsNull()
    {
        _summerEbtCaseService
            .GetHouseholdByIdentifierAsync(Arg.Any<PluginHouseholdIdentifierType>(), Arg.Any<string>(), Arg.Any<PluginPiiVisibility>(), Arg.Any<PluginIdentityAssuranceLevel>(), Arg.Any<CancellationToken>())
            .Returns((PluginHouseholdData?)null);

        var result = await _repository.GetHouseholdByEmailAsync("ishouldnotexist@example.com", FullPii, UserIalLevel.IAL1plus);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenEmailIsNull_ReturnsNull()
    {
        var result = await _repository.GetHouseholdByEmailAsync(null!, FullPii, UserIalLevel.IAL1plus);

        Assert.Null(result);
        await _summerEbtCaseService.DidNotReceive()
            .GetHouseholdByIdentifierAsync(Arg.Any<PluginHouseholdIdentifierType>(), Arg.Any<string>(), Arg.Any<PluginPiiVisibility>(), Arg.Any<PluginIdentityAssuranceLevel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenEmailIsWhitespace_ReturnsNull()
    {
        var result = await _repository.GetHouseholdByEmailAsync("   ", FullPii, UserIalLevel.IAL1plus);

        Assert.Null(result);
        await _summerEbtCaseService.DidNotReceive()
            .GetHouseholdByIdentifierAsync(Arg.Any<PluginHouseholdIdentifierType>(), Arg.Any<string>(), Arg.Any<PluginPiiVisibility>(), Arg.Any<PluginIdentityAssuranceLevel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_NormalizesEmail()
    {
        _summerEbtCaseService
            .GetHouseholdByIdentifierAsync(Arg.Any<PluginHouseholdIdentifierType>(), Arg.Any<string>(), Arg.Any<PluginPiiVisibility>(), Arg.Any<PluginIdentityAssuranceLevel>(), Arg.Any<CancellationToken>())
            .Returns(new PluginHouseholdData { Email = "user@example.com", Applications = new List<PluginApplication>() });

        await _repository.GetHouseholdByEmailAsync("  USER@EXAMPLE.COM  ", FullPii, UserIalLevel.IAL1plus);

        await _summerEbtCaseService.Received(1)
            .GetHouseholdByIdentifierAsync(PluginHouseholdIdentifierType.Email, "user@example.com", Arg.Any<PluginPiiVisibility>(), Arg.Any<PluginIdentityAssuranceLevel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_PassesPiiVisibilityToPlugin()
    {
        _summerEbtCaseService
            .GetHouseholdByIdentifierAsync(Arg.Any<PluginHouseholdIdentifierType>(), Arg.Any<string>(), Arg.Any<PluginPiiVisibility>(), Arg.Any<PluginIdentityAssuranceLevel>(), Arg.Any<CancellationToken>())
            .Returns(new PluginHouseholdData { Email = "u@e.com", Applications = new List<PluginApplication>() });

        await _repository.GetHouseholdByEmailAsync("u@e.com", new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true), UserIalLevel.IAL1plus);

        await _summerEbtCaseService.Received(1)
            .GetHouseholdByIdentifierAsync(PluginHouseholdIdentifierType.Email, "u@e.com", Arg.Is<PluginPiiVisibility>(p => p.IncludeAddress && p.IncludeEmail && p.IncludePhone), Arg.Any<PluginIdentityAssuranceLevel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenPiiVisibilityExcludesEmail_ReturnsMaskedEmail()
    {
        _summerEbtCaseService
            .GetHouseholdByIdentifierAsync(Arg.Any<PluginHouseholdIdentifierType>(), Arg.Any<string>(), Arg.Any<PluginPiiVisibility>(), Arg.Any<PluginIdentityAssuranceLevel>(), Arg.Any<CancellationToken>())
            .Returns(new PluginHouseholdData { Email = "u@e.com", Phone = "303-555-0100", Applications = new List<PluginApplication>() });

        var noEmailPii = new PiiVisibility(IncludeAddress: true, IncludeEmail: false, IncludePhone: true);
        var result = await _repository.GetHouseholdByEmailAsync("user@example.com", noEmailPii, UserIalLevel.IAL1plus);

        Assert.NotNull(result);
        // MaskEmail keeps first local char and full domain ("u@e.com" → "u***@e.com").
        Assert.Equal("u***@e.com", result.Email);
        Assert.Equal("303-555-0100", result.Phone);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenPiiExcludesPhone_ReturnsMaskedPhone()
    {
        _summerEbtCaseService
            .GetHouseholdByIdentifierAsync(Arg.Any<PluginHouseholdIdentifierType>(), Arg.Any<string>(), Arg.Any<PluginPiiVisibility>(), Arg.Any<PluginIdentityAssuranceLevel>(), Arg.Any<CancellationToken>())
            .Returns(new PluginHouseholdData { Email = "u@e.com", Phone = "303-555-0100", Applications = new List<PluginApplication>() });

        var noPhonePii = new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: false);
        var result = await _repository.GetHouseholdByEmailAsync("u@e.com", noPhonePii, UserIalLevel.IAL1plus);

        Assert.NotNull(result);
        Assert.Equal("u@e.com", result.Email);
        Assert.Equal("***-***-0100", result.Phone);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenPiiExcludesAddress_ReturnsMaskedAddress()
    {
        _summerEbtCaseService
            .GetHouseholdByIdentifierAsync(Arg.Any<PluginHouseholdIdentifierType>(), Arg.Any<string>(), Arg.Any<PluginPiiVisibility>(), Arg.Any<PluginIdentityAssuranceLevel>(), Arg.Any<CancellationToken>())
            .Returns(new PluginHouseholdData
            {
                Email = "u@e.com",
                Phone = "555",
                AddressOnFile = new StatesPlugins.Interfaces.Models.Household.Address
                {
                    StreetAddress1 = "123 Main St",
                    StreetAddress2 = "Apt 4B",
                    City = "Denver",
                    State = "CO",
                    PostalCode = "80202"
                },
                Applications = new List<PluginApplication>()
            });

        var noAddressPii = new PiiVisibility(IncludeAddress: false, IncludeEmail: true, IncludePhone: true);
        var result = await _repository.GetHouseholdByEmailAsync("u@e.com", noAddressPii, UserIalLevel.IAL1plus);

        Assert.NotNull(result);
        Assert.NotNull(result.AddressOnFile);
        Assert.Equal("****", result.AddressOnFile.StreetAddress1);
        Assert.Null(result.AddressOnFile.StreetAddress2);
        Assert.Equal("Denver", result.AddressOnFile.City);
        Assert.Equal("CO", result.AddressOnFile.State);
        Assert.Equal("80202", result.AddressOnFile.PostalCode);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenPiiVisibilityExcludesAddress_ReturnsMaskedAddressOnFile()
    {
        _summerEbtCaseService
            .GetHouseholdByIdentifierAsync(Arg.Any<PluginHouseholdIdentifierType>(), Arg.Any<string>(), Arg.Any<PluginPiiVisibility>(), Arg.Any<PluginIdentityAssuranceLevel>(), Arg.Any<CancellationToken>())
            .Returns(new PluginHouseholdData
            {
                Email = "u@e.com",
                Applications = new List<PluginApplication>(),
                AddressOnFile = new PluginAddress
                {
                    StreetAddress1 = "123 Main St",
                    City = "Denver",
                    State = "CO",
                    PostalCode = "80202"
                }
            });

        var noAddressPii = new PiiVisibility(IncludeAddress: false, IncludeEmail: true, IncludePhone: true);
        var result = await _repository.GetHouseholdByEmailAsync("u@e.com", noAddressPii, UserIalLevel.IAL1plus);

        Assert.NotNull(result);
        Assert.NotNull(result.AddressOnFile);
        Assert.Equal("****", result.AddressOnFile.StreetAddress1);
        Assert.Null(result.AddressOnFile.StreetAddress2);
        Assert.Equal("Denver", result.AddressOnFile.City);
        Assert.Equal("CO", result.AddressOnFile.State);
        Assert.Equal("80202", result.AddressOnFile.PostalCode);
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_WhenEmailIdentifier_DelegatesToPlugin()
    {
        var email = "guardian@example.com";
        var pluginData = new PluginHouseholdData { Email = email, Applications = new List<PluginApplication>() };
        _summerEbtCaseService
            .GetHouseholdByIdentifierAsync(PluginHouseholdIdentifierType.Email, email, Arg.Any<PluginPiiVisibility>(), Arg.Any<PluginIdentityAssuranceLevel>(), Arg.Any<CancellationToken>())
            .Returns(pluginData);

        var result = await _repository.GetHouseholdByIdentifierAsync(
            HouseholdIdentifier.Email(email), NoAddressPii, UserIalLevel.None);

        Assert.NotNull(result);
        // Plugin always receives full visibility — masking is handled by ApplyPiiVisibility
        await _summerEbtCaseService.Received(1)
            .GetHouseholdByIdentifierAsync(PluginHouseholdIdentifierType.Email, email, Arg.Is<PluginPiiVisibility>(p => p.IncludeAddress && p.IncludeEmail && p.IncludePhone), Arg.Any<PluginIdentityAssuranceLevel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_WhenPhoneIdentifier_DelegatesToPlugin()
    {
        var phone = "8185558439";
        var pluginData = new PluginHouseholdData { Phone = phone, Applications = new List<PluginApplication>() };
        _summerEbtCaseService
            .GetHouseholdByIdentifierAsync(PluginHouseholdIdentifierType.Phone, phone, Arg.Any<PluginPiiVisibility>(), Arg.Any<PluginIdentityAssuranceLevel>(), Arg.Any<CancellationToken>())
            .Returns(pluginData);

        var result = await _repository.GetHouseholdByIdentifierAsync(
            HouseholdIdentifier.Phone(phone), FullPii, UserIalLevel.IAL1plus);

        Assert.NotNull(result);
        Assert.Equal(phone, result.Phone);
        await _summerEbtCaseService.Received(1)
            .GetHouseholdByIdentifierAsync(PluginHouseholdIdentifierType.Phone, phone, Arg.Any<PluginPiiVisibility>(), Arg.Any<PluginIdentityAssuranceLevel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_WhenPhoneIdentifier_TrimsWhitespaceBeforePluginCall()
    {
        var pluginData = new PluginHouseholdData { Phone = "8185558439", Applications = new List<PluginApplication>() };
        _summerEbtCaseService
            .GetHouseholdByIdentifierAsync(PluginHouseholdIdentifierType.Phone, "8185558439", Arg.Any<PluginPiiVisibility>(), Arg.Any<PluginIdentityAssuranceLevel>(), Arg.Any<CancellationToken>())
            .Returns(pluginData);

        await _repository.GetHouseholdByIdentifierAsync(
            HouseholdIdentifier.Phone("  8185558439  "), FullPii, UserIalLevel.IAL1plus);

        await _summerEbtCaseService.Received(1)
            .GetHouseholdByIdentifierAsync(PluginHouseholdIdentifierType.Phone, "8185558439", Arg.Any<PluginPiiVisibility>(), Arg.Any<PluginIdentityAssuranceLevel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_WhenIdentifierValueIsWhitespace_ReturnsNullWithoutCallingPlugin()
    {
        var result = await _repository.GetHouseholdByIdentifierAsync(
            HouseholdIdentifier.Email("   "), FullPii, UserIalLevel.IAL1plus);

        Assert.Null(result);
        await _summerEbtCaseService.DidNotReceive()
            .GetHouseholdByIdentifierAsync(Arg.Any<PluginHouseholdIdentifierType>(), Arg.Any<string>(), Arg.Any<PluginPiiVisibility>(), Arg.Any<PluginIdentityAssuranceLevel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_WhenPiiVisibilityNull_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _repository.GetHouseholdByIdentifierAsync(HouseholdIdentifier.Email("u@e.com"), null!, UserIalLevel.None));
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_WhenPluginReturnsNull_ReturnsNull()
    {
        _summerEbtCaseService
            .GetHouseholdByIdentifierAsync(PluginHouseholdIdentifierType.SnapId, "SNAP123", Arg.Any<PluginPiiVisibility>(), Arg.Any<PluginIdentityAssuranceLevel>(), Arg.Any<CancellationToken>())
            .Returns((PluginHouseholdData?)null);

        var result = await _repository.GetHouseholdByIdentifierAsync(
            HouseholdIdentifier.SnapId("SNAP123"), FullPii, UserIalLevel.IAL1plus);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenPiiVisibilityNull_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _repository.GetHouseholdByEmailAsync("u@e.com", null!, UserIalLevel.None));
    }

    [Fact]
    public async Task UpsertHouseholdAsync_ThrowsNotSupportedException()
    {
        var household = new HouseholdData { Email = "u@e.com", Applications = new List<Application>() };

        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => _repository.UpsertHouseholdAsync(household));

        Assert.Contains("read-only", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}

