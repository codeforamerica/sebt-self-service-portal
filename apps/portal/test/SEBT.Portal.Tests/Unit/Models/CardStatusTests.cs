using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Tests.Unit.Models;

/// <summary>
/// Verifies CardStatus enum membership. The frontend deserializes by member name
/// (per JsonStringEnumConverter on the enum), so integer ordinals are not load-bearing
/// across the wire. Parity with the StatesPlugins.Interfaces enum is asserted separately
/// in EnumParityTests.
/// </summary>
public class CardStatusTests
{
    [Fact]
    public void CardStatus_HasExpectedCount()
    {
        var values = Enum.GetValues<CardStatus>();
        Assert.Equal(10, values.Length);
    }

    [Theory]
    [InlineData(CardStatus.Active)]
    [InlineData(CardStatus.Damaged)]
    [InlineData(CardStatus.DeactivatedByState)]
    [InlineData(CardStatus.Frozen)]
    [InlineData(CardStatus.Lost)]
    [InlineData(CardStatus.NotActivated)]
    [InlineData(CardStatus.Processed)]
    [InlineData(CardStatus.Stolen)]
    [InlineData(CardStatus.Undeliverable)]
    [InlineData(CardStatus.Unknown)]
    public void CardStatus_HasExpectedMember(CardStatus status)
    {
        Assert.True(Enum.IsDefined(status));
    }
}
