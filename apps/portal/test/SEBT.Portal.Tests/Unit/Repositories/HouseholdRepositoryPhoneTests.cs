using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Infrastructure.Repositories;
using ISummerEbtCaseService = SEBT.Portal.StatesPlugins.Interfaces.ISummerEbtCaseService;
using PluginHouseholdIdentifierType = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.HouseholdIdentifierType;
using PluginIdentityAssuranceLevel = SEBT.Portal.StatesPlugins.Interfaces.Models.IdentityAssuranceLevel;
using PluginPiiVisibility = SEBT.Portal.StatesPlugins.Interfaces.Models.PiiVisibility;
using PluginHouseholdData = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.HouseholdData;

namespace SEBT.Portal.Tests.Unit.Repositories;

/// <summary>
/// Phone lookups leave the repository as a 10-digit national number.
/// </summary>
public class HouseholdRepositoryPhoneTests
{
    private static readonly PiiVisibility FullPii = new(IncludeAddress: true, IncludeEmail: true, IncludePhone: true);

    private readonly ISummerEbtCaseService _summerEbtCaseService = Substitute.For<ISummerEbtCaseService>();
    private readonly HouseholdRepository _repository;

    public HouseholdRepositoryPhoneTests()
    {
        _repository = new HouseholdRepository(
            _summerEbtCaseService,
            NullLogger<HouseholdRepository>.Instance);
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_Phone_PassesNationalDigits()
    {
        _summerEbtCaseService
            .GetHouseholdByIdentifierAsync(
                PluginHouseholdIdentifierType.Phone,
                "8185558437",
                Arg.Any<PluginPiiVisibility>(),
                Arg.Any<PluginIdentityAssuranceLevel>(),
                Arg.Any<Guid?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns((PluginHouseholdData?)null);

        await _repository.GetHouseholdByIdentifierAsync(
            HouseholdIdentifier.Phone("+18185558437"),
            FullPii,
            UserIalLevel.IAL1plus);

        await _summerEbtCaseService.Received(1).GetHouseholdByIdentifierAsync(
            PluginHouseholdIdentifierType.Phone,
            "8185558437",
            Arg.Any<PluginPiiVisibility>(),
            Arg.Any<PluginIdentityAssuranceLevel>(),
            Arg.Any<Guid?>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_UnusablePhone_DoesNotCallThePlugin()
    {
        HouseholdData? result = await _repository.GetHouseholdByIdentifierAsync(
            HouseholdIdentifier.Phone("not-a-phone"),
            FullPii,
            UserIalLevel.IAL1plus);

        Assert.Null(result);
        await _summerEbtCaseService.DidNotReceive().GetHouseholdByIdentifierAsync(
            Arg.Any<PluginHouseholdIdentifierType>(),
            Arg.Any<string>(),
            Arg.Any<PluginPiiVisibility>(),
            Arg.Any<PluginIdentityAssuranceLevel>(),
            Arg.Any<Guid?>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }
}
