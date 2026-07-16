using SEBT.Portal.Infrastructure.Services.Socure;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Services.Socure;

public class SocureExcludedIdentifierTypesTests
{
    [Theory]
    [InlineData("snapAccountId")]
    [InlineData("snapPersonId")]
    [InlineData("tanfAccountId")]
    [InlineData("tanfPersonId")]
    public void IsExcludedFromSocurePayload_ReturnsTrue_ForSnapOrTanfPortalSelections(string idType)
    {
        Assert.True(SocureExcludedIdentifierTypes.IsExcludedFromSocurePayload(idType));
    }

    [Fact]
    public void IsExcludedFromSocurePayload_ReturnsTrue_ForMedicaidId()
    {
        // Medicaid IDs (7 or 8 digits per DC content) cannot satisfy Socure's
        // national_id schema requirement of exactly 4 or 9 digits, so they are
        // excluded from the outbound evaluation payload.
        Assert.True(SocureExcludedIdentifierTypes.IsExcludedFromSocurePayload("medicaidId"));
    }

    [Theory]
    [InlineData("ssn")]
    [InlineData("itin")]
    public void IsExcludedFromSocurePayload_ReturnsFalse_ForNonBenefitIdentifierTypes(string idType)
    {
        Assert.False(SocureExcludedIdentifierTypes.IsExcludedFromSocurePayload(idType));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsExcludedFromSocurePayload_ReturnsFalse_ForNullOrEmpty(string? idType)
    {
        Assert.False(SocureExcludedIdentifierTypes.IsExcludedFromSocurePayload(idType));
    }

    [Fact]
    public void IsExcludedFromSocurePayload_IsCaseSensitive()
    {
        // Comparison uses StringComparer.Ordinal to match the existing HashSet
        // convention in IdProofingBenefitIdentifierTypes. A differently-cased
        // value (e.g., "MedicaidId") is not recognized as the portal idType.
        Assert.False(SocureExcludedIdentifierTypes.IsExcludedFromSocurePayload("MedicaidId"));
    }
}
