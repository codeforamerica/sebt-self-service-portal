using Microsoft.Kiota.Abstractions.Serialization;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;

namespace SEBT.Portal.StatePlugins.CO.Tests.CbmsApi;

public class ErrorResponseSerializationTests
{
    [Fact]
    public async Task Deserialize_ErrorResponse()
    {
        // Arrange — JSON from the CBMS API spec's 404 example response
        var json = """
            {
              "apiName": "cbms-sebt-eapi-impl",
              "correlationId": "11174770-a6a1-4949-b216-622e363e872e",
              "timestamp": "2026-01-30T16:00:35.143Z",
              "errorDetails": [
                {
                  "code": "404",
                  "message": "Not Found"
                }
              ]
            }
            """;

        // Act
        var result = await KiotaJsonSerializer.DeserializeAsync<ErrorResponse>(
            json, ErrorResponse.CreateFromDiscriminatorValue);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("cbms-sebt-eapi-impl", result.ApiName);
        Assert.Equal("11174770-a6a1-4949-b216-622e363e872e", result.CorrelationId);
        Assert.Equal("2026-01-30T16:00:35.143Z", result.Timestamp);
        Assert.NotNull(result.ErrorDetails);
        var error = Assert.Single(result.ErrorDetails);
        Assert.Equal("404", error.Code);
        Assert.Equal("Not Found", error.Message);

        // Unmapped fields land in AdditionalData — fail if the spec adds new attributes
        Assert.Empty(result.AdditionalData);
        Assert.Empty(error.AdditionalData);
    }
}
