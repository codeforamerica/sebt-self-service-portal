using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Tests.Unit.Models;

/// <summary>
/// Verifies CardStatus enum values match the integer mapping expected by the frontend.
/// The frontend CARD_STATUS_MAP in schema.ts maps integer values to string names;
/// if these drift apart, the Zod schema will silently coerce to 'Unknown'.
/// </summary>
public class CardStatusTests
{
    /// <summary>
    /// Each enum value must match the integer the frontend expects.
    /// Source of truth: src/SEBT.Portal.Web/src/features/household/api/schema.ts CARD_STATUS_MAP.
    /// </summary>
    [Theory]
    [InlineData(CardStatus.Requested, 0)]
    [InlineData(CardStatus.Mailed, 1)]
    [InlineData(CardStatus.Active, 2)]
    [InlineData(CardStatus.Deactivated, 3)]
    [InlineData(CardStatus.Unknown, 4)]
    [InlineData(CardStatus.Processed, 5)]
    [InlineData(CardStatus.Lost, 6)]
    [InlineData(CardStatus.Stolen, 7)]
    [InlineData(CardStatus.Damaged, 8)]
    [InlineData(CardStatus.DeactivatedByState, 9)]
    [InlineData(CardStatus.NotActivated, 10)]
    [InlineData(CardStatus.Frozen, 11)]
    [InlineData(CardStatus.Undeliverable, 12)]
    public void CardStatus_IntegerValue_MatchesFrontendExpectation(CardStatus status, int expectedValue)
    {
        Assert.Equal(expectedValue, (int)status);
    }

    [Fact]
    public void CardStatus_HasExpectedCount()
    {
        var values = Enum.GetValues<CardStatus>();
        Assert.Equal(13, values.Length);
    }
}
