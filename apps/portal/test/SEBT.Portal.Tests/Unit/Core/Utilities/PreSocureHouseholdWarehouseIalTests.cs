using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Utilities;

namespace SEBT.Portal.Tests.Unit.Core.Utilities;

public class PreSocureHouseholdWarehouseIalTests
{
    [Theory]
    [InlineData(UserIalLevel.IAL1, true, UserIalLevel.IAL1plus)]
    [InlineData(UserIalLevel.IAL1, false, UserIalLevel.IAL1)]
    [InlineData(UserIalLevel.IAL1plus, true, UserIalLevel.IAL1plus)]
    [InlineData(UserIalLevel.IAL1plus, false, UserIalLevel.IAL1plus)]
    [InlineData(UserIalLevel.None, true, UserIalLevel.IAL1plus)]
    [InlineData(UserIalLevel.None, false, UserIalLevel.None)]
    [InlineData(UserIalLevel.IAL2, true, UserIalLevel.IAL1plus)]
    [InlineData(UserIalLevel.IAL2, false, UserIalLevel.IAL2)]
    public void ForEmailLinkedHouseholdRead_maps_to_ial1plus_when_gate_enabled(
        UserIalLevel actual,
        bool gateEnabled,
        UserIalLevel expected)
    {
        var result = PreSocureHouseholdWarehouseIal.ForEmailLinkedHouseholdRead(actual, gateEnabled);
        Assert.Equal(expected, result);
    }
}
