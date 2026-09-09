using SEBT.Portal.Api.Composition.Defaults;
using SEBT.Portal.StatesPlugins.Interfaces.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

namespace SEBT.Portal.Tests.Unit.Api.Composition.Defaults;

public class DefaultSummerEbtCaseServiceTests
{
    private static readonly PiiVisibility FullPiiVisibility = new(IncludeAddress: true, IncludeEmail: true, IncludePhone: true);

    private readonly DefaultSummerEbtCaseService _service = new();

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_ReturnsNull()
    {
        var household = await _service.GetHouseholdByIdentifierAsync(
            HouseholdIdentifierType.Email,
            "guardian@example.com",
            FullPiiVisibility,
            IdentityAssuranceLevel.IAL1);

        Assert.Null(household);
    }

    [Fact]
    public async Task GetHouseholdByGuardianEmailAsync_ReturnsNull()
    {
        var household = await _service.GetHouseholdByGuardianEmailAsync(
            "guardian@example.com",
            FullPiiVisibility,
            IdentityAssuranceLevel.IAL1);

        Assert.Null(household);
    }

    [Fact]
    public async Task TryMatchCoLoadedGuardianByBenefitIdAndDobAsync_ReturnsFalse()
    {
        var matched = await _service.TryMatchCoLoadedGuardianByBenefitIdAndDobAsync(
            "1B23456",
            new DateOnly(1990, 1, 1),
            Guid.NewGuid());

        Assert.False(matched);
    }

    [Fact]
    public async Task GetHouseholdByBenefitIdentifierAndDobAsync_ReturnsNull()
    {
        var household = await _service.GetHouseholdByBenefitIdentifierAndDobAsync(
            "1B23456",
            new DateOnly(1990, 1, 1),
            "guardian@example.com",
            FullPiiVisibility,
            IdentityAssuranceLevel.IAL1,
            Guid.NewGuid());

        Assert.Null(household);
    }
}
