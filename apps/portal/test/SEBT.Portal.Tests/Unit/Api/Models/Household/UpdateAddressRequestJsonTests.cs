using System.Text.Json;
using SEBT.Portal.Api.Models.Household;
using Xunit;

namespace SEBT.Portal.Tests.Unit.Api.Models.Household;

public class UpdateAddressRequestJsonTests
{
    [Fact]
    public void Deserialize_Web_defaults_Map_acceptEnteredAddress_To_AcceptEnteredAddress()
    {
        var json =
            """{"streetAddress1":"2207 Orchard Creek Drive","city":"Newman","state":"CA","postalCode":"95360-2424","acceptEnteredAddress":true}""";

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var dto = JsonSerializer.Deserialize<UpdateAddressRequest>(json, options);

        Assert.NotNull(dto);
        Assert.True(dto.AcceptEnteredAddress);
        Assert.Equal("2207 Orchard Creek Drive", dto.StreetAddress1);
    }
}
