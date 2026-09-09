using System.Text.Json;
using Microsoft.Kiota.Abstractions.Serialization;
using SEBT.Portal.StatePlugins.CO.Cbms;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using HouseholdAddress = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.Address;

namespace SEBT.Portal.StatePlugins.CO.Tests.Cbms;

public class CbmsAddressUpdateMapperTests
{
    [Theory]
    [InlineData("80202-1234", "80202", "1234")]
    [InlineData("80202", "80202", null)]
    [InlineData(" 80202 - 1234 ", "80202", "1234")]
    public void SplitPostalCode_maps_zip_and_optional_zip4(string input, string? expectedZip, string? expectedZip4)
    {
        var (zip, zip4) = CbmsAddressUpdateMapper.SplitPostalCode(input);
        Assert.Equal(expectedZip, zip);
        Assert.Equal(expectedZip4, zip4);
    }

    [Fact]
    public void ToCbmsAddress_copies_street_city_state_and_splits_postal()
    {
        var portal = new HouseholdAddress
        {
            StreetAddress1 = "1 Main",
            StreetAddress2 = "Apt 2",
            City = "Denver",
            State = "CO",
            PostalCode = "80202-9999"
        };

        var cbms = CbmsAddressUpdateMapper.ToCbmsAddress(portal);

        Assert.Equal("1 Main", cbms.AddrLn1);
        Assert.Equal("Apt 2", cbms.AddrLn2);
        Assert.Equal("Denver", cbms.Cty);
        Assert.Equal("CO", cbms.StaCd);
        Assert.Equal("80202", cbms.Zip);
        Assert.Equal("9999", cbms.Zip4);
    }

    [Fact]
    public void ToUpdateStudentDetailsRequest_includes_only_addr_and_cbms_ids()
    {
        var portal = new HouseholdAddress
        {
            StreetAddress1 = "456 Oak",
            City = "Denver",
            State = "CO",
            PostalCode = "80203"
        };
        var row = new GetAccountStudentDetail
        {
            SebtChldId = 88291,
            SebtAppId = 556677,
            GurdFstNm = "Jane",
            GurdLstNm = "Doe",
            GurdEmailAddr = "j@example.com"
        };

        var body = CbmsAddressUpdateMapper.ToUpdateStudentDetailsRequest(portal, row);

        Assert.Equal("88291", body.SebtChldId);
        Assert.Equal("556677", body.SebtAppId);
        Assert.Null(body.GurdFstNm);
        Assert.Null(body.GurdLstNm);
        Assert.Null(body.GurdEmailAddr);
        Assert.NotNull(body.Addr);
        Assert.Equal("456 Oak", body.Addr!.AddrLn1);
        Assert.Equal("80203", body.Addr.Zip);
    }

    [Fact]
    public async Task ToUpdateStudentDetailsRequest_omits_guardian_fields_from_serialized_json()
    {
        var body = CbmsAddressUpdateMapper.ToUpdateStudentDetailsRequest(
            new HouseholdAddress
            {
                StreetAddress1 = "456 Oak",
                City = "Denver",
                State = "CO",
                PostalCode = "80203"
            },
            new GetAccountStudentDetail
            {
                SebtChldId = 1,
                SebtAppId = 2,
                GurdFstNm = "Jane",
                GurdLstNm = "Doe",
                GurdEmailAddr = "j@example.com"
            });

        var json = await KiotaJsonSerializer.SerializeAsStringAsync(body);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.False(root.TryGetProperty("gurdFstNm", out _));
        Assert.False(root.TryGetProperty("gurdLstNm", out _));
        Assert.False(root.TryGetProperty("gurdEmailAddr", out _));
        Assert.Equal("1", root.GetProperty("sebtChldId").GetString());
        Assert.Equal("2", root.GetProperty("sebtAppId").GetString());
    }

    [Fact]
    public void TryValidatePortalAddress_fails_when_street_missing()
    {
        var address = new HouseholdAddress
        {
            StreetAddress1 = "",
            City = "Denver",
            State = "CO",
            PostalCode = "80202"
        };

        Assert.False(CbmsAddressUpdateMapper.TryValidatePortalAddress(address, out var error));
        Assert.Contains("StreetAddress1", error ?? "");
    }

    [Fact]
    public void TryValidatePortalAddress_succeeds_when_all_required_fields_present()
    {
        var address = new HouseholdAddress
        {
            StreetAddress1 = "1 Main",
            City = "Denver",
            State = "CO",
            PostalCode = "80202"
        };

        Assert.True(CbmsAddressUpdateMapper.TryValidatePortalAddress(address, out var error));
        Assert.Null(error);
    }
}
