using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Infrastructure.Repositories;
using ISummerEbtCaseService = SEBT.Portal.StatesPlugins.Interfaces.ISummerEbtCaseService;
using PluginIdentityAssuranceLevel = SEBT.Portal.StatesPlugins.Interfaces.Models.IdentityAssuranceLevel;
using PluginPiiVisibility = SEBT.Portal.StatesPlugins.Interfaces.Models.PiiVisibility;
using PluginHouseholdData = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.HouseholdData;
using PluginApplication = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.Application;
using PluginChild = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.Child;
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
            .GetHouseholdByGuardianEmailAsync(email, Arg.Any<PluginPiiVisibility>(), Arg.Any<PluginIdentityAssuranceLevel>(), Arg.Any<CancellationToken>())
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
    public async Task GetHouseholdByEmailAsync_WhenPluginReturnsNull_ReturnsNull()
    {
        _summerEbtCaseService
            .GetHouseholdByGuardianEmailAsync(Arg.Any<string>(), Arg.Any<PluginPiiVisibility>(), Arg.Any<PluginIdentityAssuranceLevel>(), Arg.Any<CancellationToken>())
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
            .GetHouseholdByGuardianEmailAsync(Arg.Any<string>(), Arg.Any<PluginPiiVisibility>(), Arg.Any<PluginIdentityAssuranceLevel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenEmailIsWhitespace_ReturnsNull()
    {
        var result = await _repository.GetHouseholdByEmailAsync("   ", FullPii, UserIalLevel.IAL1plus);

        Assert.Null(result);
        await _summerEbtCaseService.DidNotReceive()
            .GetHouseholdByGuardianEmailAsync(Arg.Any<string>(), Arg.Any<PluginPiiVisibility>(), Arg.Any<PluginIdentityAssuranceLevel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_NormalizesEmail()
    {
        _summerEbtCaseService
            .GetHouseholdByGuardianEmailAsync(Arg.Any<string>(), Arg.Any<PluginPiiVisibility>(), Arg.Any<PluginIdentityAssuranceLevel>(), Arg.Any<CancellationToken>())
            .Returns(new PluginHouseholdData { Email = "user@example.com", Applications = new List<PluginApplication>() });

        await _repository.GetHouseholdByEmailAsync("  USER@EXAMPLE.COM  ", FullPii, UserIalLevel.IAL1plus);

        await _summerEbtCaseService.Received(1)
            .GetHouseholdByGuardianEmailAsync("user@example.com", Arg.Any<PluginPiiVisibility>(), Arg.Any<PluginIdentityAssuranceLevel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_PassesPiiVisibilityToPlugin()
    {
        _summerEbtCaseService
            .GetHouseholdByGuardianEmailAsync(Arg.Any<string>(), Arg.Any<PluginPiiVisibility>(), Arg.Any<PluginIdentityAssuranceLevel>(), Arg.Any<CancellationToken>())
            .Returns(new PluginHouseholdData { Email = "u@e.com", Applications = new List<PluginApplication>() });

        await _repository.GetHouseholdByEmailAsync("u@e.com", new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true), UserIalLevel.IAL1plus);

        await _summerEbtCaseService.Received(1)
            .GetHouseholdByGuardianEmailAsync("u@e.com", Arg.Is<PluginPiiVisibility>(p => p.IncludeAddress && p.IncludeEmail && p.IncludePhone), Arg.Any<PluginIdentityAssuranceLevel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenPiiVisibilityExcludesEmail_ReturnsNullEmail()
    {
        _summerEbtCaseService
            .GetHouseholdByGuardianEmailAsync(Arg.Any<string>(), Arg.Any<PluginPiiVisibility>(), Arg.Any<PluginIdentityAssuranceLevel>(), Arg.Any<CancellationToken>())
            .Returns(new PluginHouseholdData { Email = "u@e.com", Phone = "555", Applications = new List<PluginApplication>() });

        var noEmailPii = new PiiVisibility(IncludeAddress: true, IncludeEmail: false, IncludePhone: true);
        var result = await _repository.GetHouseholdByEmailAsync("u@e.com", noEmailPii, UserIalLevel.IAL1plus);

        Assert.NotNull(result);
        Assert.Null(result.Email);
        Assert.Equal("555", result.Phone);
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_WhenEmailIdentifier_DelegatesToPlugin()
    {
        var email = "guardian@example.com";
        var pluginData = new PluginHouseholdData { Email = email, Applications = new List<PluginApplication>() };
        _summerEbtCaseService
            .GetHouseholdByGuardianEmailAsync(email, Arg.Any<PluginPiiVisibility>(), Arg.Any<PluginIdentityAssuranceLevel>(), Arg.Any<CancellationToken>())
            .Returns(pluginData);

        var result = await _repository.GetHouseholdByIdentifierAsync(
            HouseholdIdentifier.Email(email), NoAddressPii, UserIalLevel.None);

        Assert.NotNull(result);
        await _summerEbtCaseService.Received(1)
            .GetHouseholdByGuardianEmailAsync(email, Arg.Is<PluginPiiVisibility>(p => !p.IncludeAddress), Arg.Any<PluginIdentityAssuranceLevel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_WhenNonEmailIdentifier_ReturnsNullWithoutCallingPlugin()
    {
        var result = await _repository.GetHouseholdByIdentifierAsync(
            HouseholdIdentifier.SnapId("SNAP123"), FullPii, UserIalLevel.IAL1plus);

        Assert.Null(result);
        await _summerEbtCaseService.DidNotReceive()
            .GetHouseholdByGuardianEmailAsync(Arg.Any<string>(), Arg.Any<PluginPiiVisibility>(), Arg.Any<PluginIdentityAssuranceLevel>(), Arg.Any<CancellationToken>());
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
