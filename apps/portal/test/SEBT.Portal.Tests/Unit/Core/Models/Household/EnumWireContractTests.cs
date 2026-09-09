using Xunit;

using ApplicationStatus = global::SEBT.Portal.Core.Models.Household.ApplicationStatus;
using IssuanceType = global::SEBT.Portal.Core.Models.Household.IssuanceType;

namespace SEBT.Portal.Tests.Unit.Core.Models.Household;

/// <summary>
/// Pins the numeric values of the enums the API serializes as integers.
///
/// Neither enum carries a JsonStringEnumConverter, so the ordinal is what reaches the browser.
/// The frontend decodes it against a hardcoded table in
/// src/SEBT.Portal.Web/src/features/household/api/schema.ts (APPLICATION_STATUS_MAP,
/// ISSUANCE_TYPE_MAP), which cannot import this enum. Reordering a member here therefore
/// mislabels every household on the dashboard, and Zod cannot detect it because the shifted
/// ordinal is still a valid integer.
///
/// EnumParityTests guards the Core-to-plugin-contract half of the seam. This class guards the
/// Core-to-browser half, and it lives in this repo on purpose: the plugin contract's own
/// ordinal test (EnumContractTests) sits in the state-connector repo and does not run in this
/// repo's CI.
///
/// If a test here fails, update schema.ts and its table in schema.test.ts in the same change.
/// </summary>
public class EnumWireContractTests
{
    [Theory]
    [InlineData(ApplicationStatus.Unknown, 0)]
    [InlineData(ApplicationStatus.Pending, 1)]
    [InlineData(ApplicationStatus.Approved, 2)]
    [InlineData(ApplicationStatus.Denied, 3)]
    [InlineData(ApplicationStatus.UnderReview, 4)]
    [InlineData(ApplicationStatus.Cancelled, 5)]
    public void ApplicationStatus_HasExpectedWireValue(ApplicationStatus value, int expected)
        => Assert.Equal(expected, (int)value);

    [Theory]
    [InlineData(IssuanceType.Unknown, 0)]
    [InlineData(IssuanceType.SummerEbt, 1)]
    [InlineData(IssuanceType.TanfEbtCard, 2)]
    [InlineData(IssuanceType.SnapEbtCard, 3)]
    public void IssuanceType_HasExpectedWireValue(IssuanceType value, int expected)
        => Assert.Equal(expected, (int)value);

    // Member-count checks catch an insertion, which the per-member theories above cannot:
    // a new member appended at the end leaves every existing ordinal intact and would
    // otherwise ship without a matching entry in the frontend's decode table.

    [Fact]
    public void ApplicationStatus_HasNoUnpinnedMembers()
        => Assert.Equal(6, Enum.GetValues<ApplicationStatus>().Length);

    [Fact]
    public void IssuanceType_HasNoUnpinnedMembers()
        => Assert.Equal(4, Enum.GetValues<IssuanceType>().Length);
}
