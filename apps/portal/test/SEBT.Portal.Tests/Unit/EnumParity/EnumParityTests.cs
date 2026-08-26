using Xunit;

using CoreCardStatus = SEBT.Portal.Core.Models.Household.CardStatus;
using CoreApplicationStatus = SEBT.Portal.Core.Models.Household.ApplicationStatus;
using CoreIssuanceType = SEBT.Portal.Core.Models.Household.IssuanceType;
using CoreBenefitIssuanceType = SEBT.Portal.Core.Models.Household.BenefitIssuanceType;
using CoreEnrollmentStatus = SEBT.Portal.Core.StateConnector.EnrollmentStatus;
using CoreEligibilityType = SEBT.Portal.Core.StateConnector.EligibilityType;
using CoreCardReplacementReason = SEBT.Portal.Core.StateConnector.CardReplacementReason;
using InterfaceCardStatus = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CardStatus;
using InterfaceApplicationStatus = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.ApplicationStatus;
using InterfaceIssuanceType = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.IssuanceType;
using InterfaceBenefitIssuanceType = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.BenefitIssuanceType;
using InterfaceEnrollmentStatus = SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck.EnrollmentStatus;
using InterfaceEligibilityType = SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck.EligibilityType;
using InterfaceCardReplacementReason = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CardReplacementReason;

namespace SEBT.Portal.Tests.Unit.EnumParity;

/// <summary>
/// Asserts that enums declared in both StatesPlugins.Interfaces and Core stay in step.
/// Any drift causes silent data loss or, worse, silently wrong labels.
///
/// Two different translation mechanisms are in play, and they need different guards:
///
/// <list type="bullet">
/// <item>
/// <b>By value.</b> ApplicationStatus, IssuanceType, and BenefitIssuanceType cross the plugin
/// boundary as a direct enum cast (PluginHouseholdDataMapper lines 25, 87, 99, 133, 137). The
/// CLR permits unboxing between enums that share an underlying type, so the numeric value is
/// what carries over, not the member name. Reorder either enum and an Approved application
/// silently becomes Pending. Names AND values must match.
/// </item>
/// <item>
/// <b>By name.</b> CardStatus crosses via ConvertEnum, which is Enum.TryParse on the member
/// name, so its ordinals are free to move. Only names must match.
/// </item>
/// <item>
/// <b>By explicit switch.</b> The Core StateConnector port enums (EnrollmentStatus,
/// EligibilityType, CardReplacementReason) cross through adapter switches that throw on
/// unmapped members, but that only fails when the unmapped value actually flows through at
/// runtime. These tests surface drift as soon as either declaration changes; names and
/// values are both asserted so the port and contract declarations stay interchangeable.
/// </item>
/// </list>
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

    [Fact]
    public void ApplicationStatus_InterfaceAndCore_HaveIdenticalValues()
        => AssertEnumValuesEqual<InterfaceApplicationStatus, CoreApplicationStatus>();

    [Fact]
    public void IssuanceType_InterfaceAndCore_HaveIdenticalValues()
        => AssertEnumValuesEqual<InterfaceIssuanceType, CoreIssuanceType>();

    [Fact]
    public void BenefitIssuanceType_InterfaceAndCore_HaveIdenticalValues()
        => AssertEnumValuesEqual<InterfaceBenefitIssuanceType, CoreBenefitIssuanceType>();

    [Fact]
    public void EnrollmentStatus_InterfaceAndCore_HaveIdenticalMembers()
        => AssertEnumNamesEqual<InterfaceEnrollmentStatus, CoreEnrollmentStatus>();

    [Fact]
    public void EligibilityType_InterfaceAndCore_HaveIdenticalMembers()
        => AssertEnumNamesEqual<InterfaceEligibilityType, CoreEligibilityType>();

    [Fact]
    public void CardReplacementReason_InterfaceAndCore_HaveIdenticalMembers()
        => AssertEnumNamesEqual<InterfaceCardReplacementReason, CoreCardReplacementReason>();

    [Fact]
    public void EnrollmentStatus_InterfaceAndCore_HaveIdenticalValues()
        => AssertEnumValuesEqual<InterfaceEnrollmentStatus, CoreEnrollmentStatus>();

    [Fact]
    public void EligibilityType_InterfaceAndCore_HaveIdenticalValues()
        => AssertEnumValuesEqual<InterfaceEligibilityType, CoreEligibilityType>();

    [Fact]
    public void CardReplacementReason_InterfaceAndCore_HaveIdenticalValues()
        => AssertEnumValuesEqual<InterfaceCardReplacementReason, CoreCardReplacementReason>();

    // CardStatus is deliberately absent from the value-parity set: ConvertEnum matches it by
    // name, and it is serialized to the browser as a string.

    private static void AssertEnumNamesEqual<TInterface, TCore>()
        where TInterface : struct, Enum
        where TCore : struct, Enum
    {
        var interfaceNames = Enum.GetNames<TInterface>().OrderBy(n => n).ToArray();
        var coreNames = Enum.GetNames<TCore>().OrderBy(n => n).ToArray();
        Assert.Equal(interfaceNames, coreNames);
    }

    private static void AssertEnumValuesEqual<TInterface, TCore>()
        where TInterface : struct, Enum
        where TCore : struct, Enum
    {
        var interfaceValues = Enum.GetNames<TInterface>()
            .OrderBy(n => n)
            .ToDictionary(n => n, n => Convert.ToInt32(Enum.Parse<TInterface>(n)));
        var coreValues = Enum.GetNames<TCore>()
            .OrderBy(n => n)
            .ToDictionary(n => n, n => Convert.ToInt32(Enum.Parse<TCore>(n)));

        Assert.Equal(interfaceValues, coreValues);
    }
}
