using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Infrastructure.Repositories;
using ISummerEbtCaseService = SEBT.Portal.StatesPlugins.Interfaces.ISummerEbtCaseService;
using PluginHouseholdData = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.HouseholdData;
using PluginHouseholdIdentifierType = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.HouseholdIdentifierType;
using PluginIdentityAssuranceLevel = SEBT.Portal.StatesPlugins.Interfaces.Models.IdentityAssuranceLevel;
using PluginPiiVisibility = SEBT.Portal.StatesPlugins.Interfaces.Models.PiiVisibility;
using PluginSummerEbtCase = SEBT.Portal.StatesPlugins.Interfaces.Data.Cases.SummerEbtCase;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Repositories;

/// <summary>
/// Pins the parts of the plugin-path household read contract that callers depend
/// on regardless of how <c>SummerEBTCaseID</c> is encoded:
/// <list type="bullet">
/// <item><c>SummerEBTCaseID</c> is a deterministic key — the frontend uses it to
/// merge two GET responses and as a lookup key across page navigations, so the
/// same source record must yield a byte-identical value on every fetch.</item>
/// <item>Guardian-facing display fields (<c>EbtCaseNumber</c>,
/// <c>CaseDisplayNumber</c>, <c>EbtCardLastFour</c>) carry the state's raw wire
/// values untouched.</item>
/// </list>
/// </summary>
public class HouseholdReadCaseIdentityTests
{
    private const string GuardianEmail = "user@example.com";
    private const string RawCaseId = "STATE-CASE-123";

    private static readonly PiiVisibility FullPii =
        new(IncludeAddress: true, IncludeEmail: true, IncludePhone: true);

    private static PluginHouseholdData CreatePluginHousehold() => new()
    {
        Email = GuardianEmail,
        SummerEbtCases = new List<PluginSummerEbtCase>
        {
            new PluginSummerEbtCase
            {
                SummerEBTCaseID = RawCaseId,
                ApplicationId = "APP-9",
                ApplicationStudentId = "STU-7",
                ChildFirstName = "Maria",
                ChildLastName = "Garcia",
                ChildDateOfBirth = new DateOnly(2015, 5, 15),
                HouseholdType = "DirectCert",
                EligibilityType = "SNAP",
                EbtCaseNumber = "EBT-555",
                CaseDisplayNumber = "DISP-42",
                EbtCardLastFour = "4321"
            }
        }
    };

    private static HouseholdRepository CreateRepository()
    {
        var pluginService = Substitute.For<ISummerEbtCaseService>();
        // Fresh object graph per call — determinism must hold across separate
        // fetches of the same source record, not just for a cached instance.
        pluginService
            .GetHouseholdByIdentifierAsync(
                Arg.Any<PluginHouseholdIdentifierType>(),
                Arg.Any<string>(),
                Arg.Any<PluginPiiVisibility>(),
                Arg.Any<PluginIdentityAssuranceLevel>(),
                Arg.Any<Guid?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => CreatePluginHousehold());
        return new HouseholdRepository(pluginService, NullLogger<HouseholdRepository>.Instance);
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_SameRecordFetchedTwice_YieldsByteIdenticalCaseId()
    {
        var repository = CreateRepository();
        var identifier = HouseholdIdentifier.Email(GuardianEmail);

        var first = await repository.GetHouseholdByIdentifierAsync(identifier, FullPii, UserIalLevel.IAL1plus);
        var second = await repository.GetHouseholdByIdentifierAsync(identifier, FullPii, UserIalLevel.IAL1plus);

        Assert.NotNull(first);
        Assert.NotNull(second);
        var firstId = Assert.Single(first.SummerEbtCases).SummerEBTCaseID;
        var secondId = Assert.Single(second.SummerEbtCases).SummerEBTCaseID;
        Assert.NotNull(firstId);
        Assert.Equal(firstId, secondId);
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_DisplayFields_CarryRawWireValues()
    {
        var repository = CreateRepository();

        var household = await repository.GetHouseholdByIdentifierAsync(
            HouseholdIdentifier.Email(GuardianEmail), FullPii, UserIalLevel.IAL1plus);

        Assert.NotNull(household);
        var summerEbtCase = Assert.Single(household.SummerEbtCases);
        Assert.Equal("EBT-555", summerEbtCase.EbtCaseNumber);
        Assert.Equal("DISP-42", summerEbtCase.CaseDisplayNumber);
        Assert.Equal("4321", summerEbtCase.EbtCardLastFour);
    }
}
