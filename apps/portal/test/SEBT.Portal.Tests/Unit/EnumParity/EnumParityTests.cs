using Xunit;

using CoreCardStatus = SEBT.Portal.Core.Models.Household.CardStatus;
using CoreApplicationStatus = SEBT.Portal.Core.Models.Household.ApplicationStatus;
using CoreIssuanceType = SEBT.Portal.Core.Models.Household.IssuanceType;
using CoreBenefitIssuanceType = SEBT.Portal.Core.Models.Household.BenefitIssuanceType;
using InterfaceCardStatus = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CardStatus;
using InterfaceApplicationStatus = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.ApplicationStatus;
using InterfaceIssuanceType = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.IssuanceType;
using InterfaceBenefitIssuanceType = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.BenefitIssuanceType;

namespace SEBT.Portal.Tests.Unit.EnumParity;

/// <summary>
/// Asserts that enums declared in both StatesPlugins.Interfaces and Core have identical
/// members. PluginHouseholdDataMapper translates between the two layers by name; any
/// drift causes silent data loss.
/// </summary>
public class EnumParityTests
{
    [Fact]
    public void CardStatus_InterfaceAndCore_HaveIdenticalMembers()
        => AssertEnumNamesEqual<InterfaceCardStatus, CoreCardStatus>();

    [Fact]
    public void ApplicationStatus_InterfaceAndCore_HaveIdenticalMembers()
        => AssertEnumNamesEqual<InterfaceApplicationStatus, CoreApplicationStatus>();

    [Fact]
    public void IssuanceType_InterfaceAndCore_HaveIdenticalMembers()
        => AssertEnumNamesEqual<InterfaceIssuanceType, CoreIssuanceType>();

    [Fact]
    public void BenefitIssuanceType_InterfaceAndCore_HaveIdenticalMembers()
        => AssertEnumNamesEqual<InterfaceBenefitIssuanceType, CoreBenefitIssuanceType>();

    private static void AssertEnumNamesEqual<TInterface, TCore>()
        where TInterface : struct, Enum
        where TCore : struct, Enum
    {
        var interfaceNames = Enum.GetNames<TInterface>().OrderBy(n => n).ToArray();
        var coreNames = Enum.GetNames<TCore>().OrderBy(n => n).ToArray();
        Assert.Equal(interfaceNames, coreNames);
    }
}
