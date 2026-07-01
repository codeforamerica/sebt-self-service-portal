using System.Text.Json;
using Microsoft.Kiota.Abstractions.Serialization;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;

namespace SEBT.Portal.StatePlugins.CO.Tests.CbmsApi;

public class GetAccountDetailsSerializationTests
{
    [Fact]
    public async Task Deserialize_GetAccountDetailsResponse()
    {
        // Arrange — JSON from the CBMS API spec's example response
        var json = """
            {
              "stdntEnrollDtls": [
                {
                  "gurdFstNm": "Jane",
                  "gurdLstNm": "Smith",
                  "gurdPhnNm": "555-0123",
                  "gurdEmailAddr": "jane.smith@example.com",
                  "sebtYear": 2024,
                  "sebtAppId": 556677,
                  "stdFstNm": "Johnny",
                  "stdLstNm": "Smith",
                  "stdDob": "2016-10-20",
                  "mtchCnfd": 100,
                  "stdntEligSts": "AP",
                  "sebtAppSts": "PW",
                  "eligSrc": "DIRECT_CERT",
                  "sebtChldId": 990011,
                  "sebtChldCwin": 123456789,
                  "addrLn1": "123 Maple Avenue",
                  "addrLn2": "Apt 4B",
                  "cty": "Denver",
                  "staCd": "CO",
                  "zip": "80202",
                  "zip4": "1234",
                  "ebtCardLastFour": "4422",
                  "benAvalDt": "2024-06-01",
                  "benExpDt": "2024-08-31",
                  "ebtCardSts": "ACTIVE",
                  "cardIssDt": "2024-05-20",
                  "cardBal": 120.50,
                  "cbmsCsId": "C8887727",
                  "dircEligSrc": "SNAP"
                }
              ],
              "respCd": "00",
              "respMsg": "Success"
            }
            """;

        // Act
        var result = await KiotaJsonSerializer.DeserializeAsync<GetAccountDetailsResponse>(
            json, GetAccountDetailsResponse.CreateFromDiscriminatorValue);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.StdntEnrollDtls);
        var student = Assert.Single(result.StdntEnrollDtls);

        // Guardian details
        Assert.Equal("Jane", student.GurdFstNm);
        Assert.Equal("Smith", student.GurdLstNm);
        Assert.Equal("555-0123", student.GurdPhnNm);
        Assert.Equal("jane.smith@example.com", student.GurdEmailAddr);

        // Student details
        Assert.Equal("Johnny", student.StdFstNm);
        Assert.Equal("Smith", student.StdLstNm);
        Assert.Equal("2016-10-20", student.StdDob);
        Assert.Equal(100d, student.MtchCnfd);
        Assert.Equal("AP", student.StdntEligSts);

        // Enrollment details
        Assert.Equal(2024, student.SebtYear);
        Assert.Equal(556677, student.SebtAppId);
        Assert.Equal("PW", student.SebtAppSts);
        Assert.Equal("DIRECT_CERT", student.EligSrc);
        Assert.Equal(990011, student.SebtChldId);
        Assert.Equal(123456789, student.SebtChldCwin);

        // Address
        Assert.Equal("123 Maple Avenue", student.AddrLn1);
        Assert.Equal("Apt 4B", student.AddrLn2);
        Assert.Equal("Denver", student.Cty);
        Assert.Equal("CO", student.StaCd);
        Assert.Equal("80202", student.Zip);
        Assert.Equal("1234", student.Zip4);

        // Card and benefit details
        Assert.Equal("4422", student.EbtCardLastFour);
        Assert.Equal("2024-06-01", student.BenAvalDt);
        Assert.Equal("2024-08-31", student.BenExpDt);
        Assert.Equal("ACTIVE", student.EbtCardSts);
        Assert.Equal("2024-05-20", student.CardIssDt);
        Assert.Equal(120.5d, student.CardBal);
        Assert.Equal("C8887727", student.CbmsCsId);
        Assert.Equal("SNAP", student.DircEligSrc);

        // Response metadata
        Assert.Equal("00", result.RespCd);
        Assert.Equal("Success", result.RespMsg);

        // Unmapped fields land in AdditionalData — fail if the spec adds new attributes
        Assert.Empty(result.AdditionalData);
        Assert.Empty(student.AdditionalData);
    }

    [Fact]
    public async Task Serialize_GetAccountDetailsRequest()
    {
        // Arrange
        var request = new GetAccountDetailsRequest
        {
            PhnNm = "3035550199"
        };

        // Act
        var json = await KiotaJsonSerializer.SerializeAsStringAsync(request);

        // Assert — verify wire-format field name matches the spec
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("3035550199", root.GetProperty("phnNm").GetString());
    }
}
