extern alias statePlugin;

using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Infrastructure.Repositories;
using ISummerEbtCaseService = statePlugin::SEBT.Portal.StatesPlugins.Interfaces.ISummerEbtCaseService;
using PluginHouseholdData = statePlugin::SEBT.Portal.StatesPlugins.Interfaces.Models.Household.HouseholdData;
using PluginApplication = statePlugin::SEBT.Portal.StatesPlugins.Interfaces.Models.Household.Application;
using PluginChild = statePlugin::SEBT.Portal.StatesPlugins.Interfaces.Models.Household.Child;
using PluginApplicationStatus = statePlugin::SEBT.Portal.StatesPlugins.Interfaces.Models.Household.ApplicationStatus;
using PluginCardStatus = statePlugin::SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CardStatus;
using PluginIssuanceType = statePlugin::SEBT.Portal.StatesPlugins.Interfaces.Models.Household.IssuanceType;
using PluginBenefitIssuanceType = statePlugin::SEBT.Portal.StatesPlugins.Interfaces.Models.Household.BenefitIssuanceType;

namespace SEBT.Portal.Tests.Unit.Repositories;

/// <summary>
/// Unit tests for HouseholdRepository.
/// </summary>
public class HouseholdRepositoryTests
{
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
            .GetHouseholdByGuardianEmailAsync(email, false, Arg.Any<CancellationToken>())
            .Returns(pluginData);

        var result = await _repository.GetHouseholdByEmailAsync(email, includeAddress: false);

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
            .GetHouseholdByGuardianEmailAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((PluginHouseholdData?)null);

        var result = await _repository.GetHouseholdByEmailAsync("ishouldnotexist@example.com");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenEmailIsNull_ReturnsNull()
    {
        var result = await _repository.GetHouseholdByEmailAsync(null!);

        Assert.Null(result);
        await _summerEbtCaseService.DidNotReceive()
            .GetHouseholdByGuardianEmailAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_WhenEmailIsWhitespace_ReturnsNull()
    {
        var result = await _repository.GetHouseholdByEmailAsync("   ");

        Assert.Null(result);
        await _summerEbtCaseService.DidNotReceive()
            .GetHouseholdByGuardianEmailAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_NormalizesEmail()
    {
        _summerEbtCaseService
            .GetHouseholdByGuardianEmailAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new PluginHouseholdData { Email = "user@example.com", Applications = new List<PluginApplication>() });

        await _repository.GetHouseholdByEmailAsync("  USER@EXAMPLE.COM  ");

        await _summerEbtCaseService.Received(1)
            .GetHouseholdByGuardianEmailAsync("user@example.com", false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHouseholdByEmailAsync_PassesIncludeAddressToPlugin()
    {
        _summerEbtCaseService
            .GetHouseholdByGuardianEmailAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new PluginHouseholdData { Email = "u@e.com", Applications = new List<PluginApplication>() });

        await _repository.GetHouseholdByEmailAsync("u@e.com", includeAddress: true);

        await _summerEbtCaseService.Received(1)
            .GetHouseholdByGuardianEmailAsync("u@e.com", true, Arg.Any<CancellationToken>());
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
