using System.Text.Json;
using Microsoft.Kiota.Abstractions.Serialization;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;

namespace SEBT.Portal.StatePlugins.CO.Tests.CbmsApi;

public class CheckEnrollmentSerializationTests
{
    [Fact]
    public async Task Deserialize_CheckEnrollmentResponse()
    {
        // Arrange — JSON from the CBMS API spec's example response
        var json = """
            {
              "respCd": "200",
              "respMsg": "Success",
              "stdntDtls": [
                {
                  "stdFstNm": "John",
                  "stdLstNm": "Doe",
                  "stdDob": "2015-05-15",
                  "mtchCnfd": 95,
                  "stdntEligSts": "ELIGIBLE",
                  "sebtEligSts": "Y",
                  "sebtYear": "2024",
                  "sebtAppId": "APP12345",
                  "sebtChldId": "CHLD9876",
                  "stdReqInd": "REQ-001"
                }
              ]
            }
            """;

        // Act
        var result = await KiotaJsonSerializer.DeserializeAsync<CheckEnrollmentResponse>(
            json, CheckEnrollmentResponse.CreateFromDiscriminatorValue);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("200", result.RespCd);
        Assert.Equal("Success", result.RespMsg);
        Assert.NotNull(result.StdntDtls);
        var student = Assert.Single(result.StdntDtls);
        Assert.Equal("John", student.StdFstNm);
        Assert.Equal("Doe", student.StdLstNm);
        Assert.Equal("2015-05-15", student.StdDob);
        Assert.Equal(95d, student.MtchCnfd);
        Assert.Equal("ELIGIBLE", student.StdntEligSts);
        Assert.Equal("2024", student.SebtYear);
        Assert.Equal("APP12345", student.SebtAppId);
        Assert.Equal("CHLD9876", student.SebtChldId);
        Assert.Equal("REQ-001", student.StdReqInd);

        // sebtEligSts has no typed property yet — verify it lands in AdditionalData
        Assert.True(student.AdditionalData.ContainsKey("sebtEligSts"));
        Assert.Equal("Y", student.AdditionalData["sebtEligSts"]?.ToString());

        Assert.Empty(result.AdditionalData);
    }

    [Fact]
    public async Task Serialize_CheckEnrollmentRequest()
    {
        // Arrange
        var request = new CheckEnrollmentRequest
        {
            StdFirstName = "John",
            StdLastName = "Smith",
            StdDob = "2012-04-20",
            CbmsCsId = "C12345",
            StdSasId = "S98765",
            StdSchlCd = "SCH44",
            SebtYear = "2024",
            StdReqInd = "REQ-001"
        };

        // Act
        var json = await KiotaJsonSerializer.SerializeAsStringAsync(request);

        // Assert — verify wire-format field names match the spec
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("John", root.GetProperty("stdFirstName").GetString());
        Assert.Equal("Smith", root.GetProperty("stdLastName").GetString());
        Assert.Equal("2012-04-20", root.GetProperty("stdDob").GetString());
        Assert.Equal("C12345", root.GetProperty("cbmsCsId").GetString());
        Assert.Equal("S98765", root.GetProperty("stdSasId").GetString());
        Assert.Equal("SCH44", root.GetProperty("stdSchlCd").GetString());
        Assert.Equal("2024", root.GetProperty("sebtYear").GetString());
        Assert.Equal("REQ-001", root.GetProperty("stdReqInd").GetString());
    }
}
