namespace SEBT.Portal.Tests.Unit.Models;

using SEBT.Portal.Core.Models.Household;

public class CardStatusTests
{
    [Theory]
    [InlineData(CardStatus.Unknown, 4)]
    [InlineData(CardStatus.Processed, 5)]
    [InlineData(CardStatus.Lost, 6)]
    [InlineData(CardStatus.Stolen, 7)]
    [InlineData(CardStatus.Damaged, 8)]
    [InlineData(CardStatus.DeactivatedByState, 9)]
    [InlineData(CardStatus.NotActivated, 10)]
    [InlineData(CardStatus.Frozen, 11)]
    [InlineData(CardStatus.Undeliverable, 12)]
    public void ExtendedCardStatus_HasExpectedValue(CardStatus status, int expected)
    {
        Assert.Equal(expected, (int)status);
    }

    [Theory]
    [InlineData("Lost", true)]
    [InlineData("LOST", true)]
    [InlineData("lost", true)]
    [InlineData("Active", true)]
    [InlineData("InvalidStatus", false)]
    public void CardStatus_ParsesFromString_CaseInsensitive(string input, bool shouldParse)
    {
        var result = Enum.TryParse<CardStatus>(input, ignoreCase: true, out _);
        Assert.Equal(shouldParse, result);
    }
}
