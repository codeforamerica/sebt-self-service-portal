using System.Text.Json;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Kiota.Abstractions.Serialization;
using SEBT.Portal.StatePlugins.CO.CbmsApi;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Mocks;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;

namespace SEBT.Portal.StatePlugins.CO.Tests.CbmsApi;

public class UpdateStudentDetailsSerializationTests
{
    private static HybridCache CreateInMemoryHybridCache()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    [Fact]
    public async Task Deserialize_UpdateStudentDetailsResponse()
    {
        // Arrange — JSON from the CBMS API spec's example response
        var json = """
            {
              "respCd": "200",
              "respMsg": "Success"
            }
            """;

        // Act
        var result = await KiotaJsonSerializer.DeserializeAsync<UpdateStudentDetailsResponse>(
            json, UpdateStudentDetailsResponse.CreateFromDiscriminatorValue);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("200", result.RespCd);
        Assert.Equal("Success", result.RespMsg);

        // Unmapped fields land in AdditionalData — fail if the spec adds new attributes
        Assert.Empty(result.AdditionalData);
    }

    [Fact]
    public async Task Serialize_UpdateStudentDetailsRequest()
    {
        // Arrange
        var request = new UpdateStudentDetailsRequest
        {
            SebtChldId = "CH-88291",
            SebtAppId = "APP-12345",
            Addr = new Address
            {
                AddrLn1 = "456 Oak Street",
                AddrLn2 = "Suite 200",
                Cty = "Denver",
                StaCd = "CO",
                Zip = "80202",
                Zip4 = "4421"
            },
            ReqNewCard = "Y",
            OptOut = "N",
            GurdFstNm = "Jane",
            GurdLstNm = "Smith",
            GurdEmailAddr = "jane.smith@example.com",
            NtfnOptInSw = "Y",
            NtfnSrc = "email"
        };

        // Act
        var json = await KiotaJsonSerializer.SerializeAsStringAsync(request);

        // Assert — verify wire-format field names match the spec
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("CH-88291", root.GetProperty("sebtChldId").GetString());
        Assert.Equal("APP-12345", root.GetProperty("sebtAppId").GetString());
        Assert.Equal("Y", root.GetProperty("reqNewCard").GetString());
        Assert.Equal("N", root.GetProperty("optOut").GetString());
        Assert.Equal("Jane", root.GetProperty("gurdFstNm").GetString());
        Assert.Equal("Smith", root.GetProperty("gurdLstNm").GetString());
        Assert.Equal("jane.smith@example.com", root.GetProperty("gurdEmailAddr").GetString());
        Assert.Equal("Y", root.GetProperty("ntfnOptInSw").GetString());
        Assert.Equal("email", root.GetProperty("ntfnSrc").GetString());

        // Verify nested address object
        var addr = root.GetProperty("addr");
        Assert.Equal("456 Oak Street", addr.GetProperty("addrLn1").GetString());
        Assert.Equal("Suite 200", addr.GetProperty("addrLn2").GetString());
        Assert.Equal("Denver", addr.GetProperty("cty").GetString());
        Assert.Equal("CO", addr.GetProperty("staCd").GetString());
        Assert.Equal("80202", addr.GetProperty("zip").GetString());
        Assert.Equal("4421", addr.GetProperty("zip4").GetString());
    }

    [Fact]
    public async Task Serialize_UpdateStudentDetailsRequestList_producesJsonArray()
    {
        var requests = new List<UpdateStudentDetailsRequest>
        {
            new()
            {
                SebtChldId = "CH-1",
                SebtAppId = "APP-1",
                Addr = new Address { AddrLn1 = "1 A", Cty = "Denver", StaCd = "CO", Zip = "80202" }
            },
            new()
            {
                SebtChldId = "CH-2",
                SebtAppId = "APP-2",
                Addr = new Address { AddrLn1 = "2 B", Cty = "Denver", StaCd = "CO", Zip = "80203" }
            }
        };

        var json = await KiotaJsonSerializer.SerializeAsStringAsync(requests);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(2, doc.RootElement.GetArrayLength());
        Assert.Equal("CH-1", doc.RootElement[0].GetProperty("sebtChldId").GetString());
        Assert.Equal("CH-2", doc.RootElement[1].GetProperty("sebtChldId").GetString());
    }

    /// <summary>
    /// Live CBMS JSON uses numeric ids; the OpenAPI model types them as integers.
    /// </summary>
    [Fact]
    public async Task Deserialize_GetAccountStudentDetail_with_numeric_ids()
    {
        var json = """
            {
              "sebtChldId": 1200507,
              "sebtAppId": 1198782,
              "sebtYear": 2026,
              "sebtChldCwin": 10615599,
              "stdFstNm": "CHILD"
            }
            """;

        var row = await KiotaJsonSerializer.DeserializeAsync<GetAccountStudentDetail>(
            json,
            GetAccountStudentDetail.CreateFromDiscriminatorValue);

        Assert.NotNull(row);
        Assert.Equal("CHILD", row.StdFstNm);
        Assert.Equal(1200507, row.SebtChldId);
        Assert.Equal(1198782, row.SebtAppId);
        Assert.Equal(2026, row.SebtYear);
        Assert.Equal(10615599, row.SebtChldCwin);
        Assert.Empty(row.AdditionalData);
    }

    /// <summary>
    /// Shape from live CBMS / sandbox (string ids, addr + reqNewCard). Two-array-element example from integration debugging.
    /// </summary>
    [Fact]
    public async Task Deserialize_example_live_update_array_two_entries()
    {
        var json = """
            [{
                "sebtChldId": "1200507",
                "sebtAppId": "1198782",
                "addr": {
                    "addrLn1": "1480 S SEEME ST",
                    "addrLn2": "3",
                    "cty": "DENVER",
                    "staCd": "CO",
                    "zip": "80219"
                },
                "reqNewCard": "Y"
            },
            {
                "sebtChldId": "1200507",
                "sebtAppId": "1198782",
                "addr": {
                    "addrLn1": "1480 S SEEMETHREE ST",
                    "addrLn2": "3",
                    "cty": "DENVER",
                    "staCd": "CO",
                    "zip": "80219"
                },
                "reqNewCard": "Y"
            }]
            """;

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        var list = new List<UpdateStudentDetailsRequest>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            var item = await KiotaJsonSerializer.DeserializeAsync<UpdateStudentDetailsRequest>(
                el.GetRawText(),
                UpdateStudentDetailsRequest.CreateFromDiscriminatorValue);
            Assert.NotNull(item);
            list.Add(item);
        }

        Assert.Equal(2, list.Count);
        Assert.Equal("1200507", list[0].SebtChldId);
        Assert.Equal("1198782", list[0].SebtAppId);
        Assert.Equal("Y", list[0].ReqNewCard);
        Assert.NotNull(list[0].Addr);
        Assert.Equal("1480 S SEEME ST", list[0].Addr!.AddrLn1);
        Assert.Equal("1480 S SEEMETHREE ST", list[1].Addr!.AddrLn1);
        Assert.Empty(list[0].AdditionalData);
        Assert.Empty(list[1].AdditionalData);
    }

    /// <summary>
    /// End-to-end Kiota call: OAuth + PATCH body serialization against <see cref="MockCbmsHttpHandler"/> (no real UAT).
    /// </summary>
    [Fact]
    public async Task PatchAsync_example_body_succeeds_against_mock_handler()
    {
        var cache = CreateInMemoryHybridCache();
        var handler = new MockCbmsHttpHandler(new MockCbmsDataStore(cache));
        var client = CbmsSebtApiClientFactory.Create(
            "mock-client-id",
            "mock-client-secret",
            CbmsDefaults.SandboxApiBaseUrl,
            CbmsDefaults.SandboxTokenEndpointUrl,
            handler);

        var json = """
            [{
                "sebtChldId": "1200507",
                "sebtAppId": "1198782",
                "addr": {
                    "addrLn1": "1480 S SEEME ST",
                    "addrLn2": "3",
                    "cty": "DENVER",
                    "staCd": "CO",
                    "zip": "80219"
                },
                "reqNewCard": "Y"
            },
            {
                "sebtChldId": "1200507",
                "sebtAppId": "1198782",
                "addr": {
                    "addrLn1": "1480 S SEEMETHREE ST",
                    "addrLn2": "3",
                    "cty": "DENVER",
                    "staCd": "CO",
                    "zip": "80219"
                },
                "reqNewCard": "Y"
            }]
            """;

        using var doc = JsonDocument.Parse(json);
        var bodies = new List<UpdateStudentDetailsRequest>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            bodies.Add((await KiotaJsonSerializer.DeserializeAsync<UpdateStudentDetailsRequest>(
                el.GetRawText(),
                UpdateStudentDetailsRequest.CreateFromDiscriminatorValue))!);
        }

        var response = await client.Sebt.UpdateStdDtls.PatchAsync(bodies);

        Assert.NotNull(response);
        Assert.Equal("00", response.RespCd);
        Assert.Equal("Success", response.RespMsg);
    }
}
