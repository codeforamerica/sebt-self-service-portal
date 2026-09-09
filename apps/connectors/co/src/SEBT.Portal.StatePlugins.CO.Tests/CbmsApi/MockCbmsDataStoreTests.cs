using System.Text.Json;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Mocks;

namespace SEBT.Portal.StatePlugins.CO.Tests.CbmsApi;

public class MockCbmsDataStoreTests
{
    private static HybridCache CreateInMemoryHybridCache()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<HybridCache>();
    }

    [Fact]
    public async Task GetResponseForPhone_returns_household_json_for_known_phone()
    {
        var cache = CreateInMemoryHybridCache();
        var store = new MockCbmsDataStore(cache);

        var result = await store.GetResponseForPhoneAsync("7198004382");

        Assert.NotNull(result);
        Assert.Contains("stdntEnrollDtls", result);
        Assert.Contains("respCd", result);
    }

    [Fact]
    public async Task GetResponseForPhone_returns_empty_success_for_unknown_phone()
    {
        var cache = CreateInMemoryHybridCache();
        var store = new MockCbmsDataStore(cache);

        var result = await store.GetResponseForPhoneAsync("9999999999");

        Assert.NotNull(result);
        Assert.Contains("\"stdntEnrollDtls\":[]", result);
        Assert.Contains("\"respCd\":\"00\"", result);
    }

    [Fact]
    public async Task GetResponseForPhone_seeds_cache_on_first_access()
    {
        var cache = CreateInMemoryHybridCache();
        var store = new MockCbmsDataStore(cache);

        var result1 = await store.GetResponseForPhoneAsync("7198004382");
        var result2 = await store.GetResponseForPhoneAsync("7198004382");

        Assert.Equal(result1, result2);
    }

    [Fact]
    public async Task ApplyPatchAsync_updates_address_on_matching_student()
    {
        var cache = CreateInMemoryHybridCache();
        var store = new MockCbmsDataStore(cache);

        // Seed by reading first
        var before = await store.GetResponseForPhoneAsync("7198004382");
        var beforeDoc = JsonDocument.Parse(before);
        var firstStudent = beforeDoc.RootElement.GetProperty("stdntEnrollDtls")[0];
        var sebtChldId = firstStudent.GetProperty("sebtChldId").GetInt32().ToString();

        var patchBody = JsonSerializer.Serialize(new
        {
            sebtChldId,
            addr = new
            {
                addrLn1 = "999 New Street",
                addrLn2 = "Unit 7",
                cty = "Boulder",
                staCd = "CO",
                zip = "80301",
                zip4 = "1111"
            }
        });

        var result = await store.ApplyPatchAsync(patchBody);
        Assert.Contains("\"respCd\":\"00\"", result);

        // Verify mutation persisted
        var after = await store.GetResponseForPhoneAsync("7198004382");
        Assert.Contains("999 New Street", after);
        Assert.Contains("Boulder", after);
    }

    [Fact]
    public async Task ApplyPatchAsync_updates_guardian_fields_on_matching_student()
    {
        var cache = CreateInMemoryHybridCache();
        var store = new MockCbmsDataStore(cache);

        var before = await store.GetResponseForPhoneAsync("7198004382");
        var beforeDoc = JsonDocument.Parse(before);
        var firstStudent = beforeDoc.RootElement.GetProperty("stdntEnrollDtls")[0];
        var sebtChldId = firstStudent.GetProperty("sebtChldId").GetInt32().ToString();

        var patchBody = JsonSerializer.Serialize(new
        {
            sebtChldId,
            gurdFstNm = "UpdatedFirst",
            gurdLstNm = "UpdatedLast",
            gurdEmailAddr = "updated@example.com"
        });

        var result = await store.ApplyPatchAsync(patchBody);
        Assert.Contains("\"respCd\":\"00\"", result);

        var after = await store.GetResponseForPhoneAsync("7198004382");
        Assert.Contains("UpdatedFirst", after);
        Assert.Contains("UpdatedLast", after);
        Assert.Contains("updated@example.com", after);
    }

    [Fact]
    public async Task ApplyPatchAsync_returns_404_for_unknown_sebtChldId()
    {
        var cache = CreateInMemoryHybridCache();
        var store = new MockCbmsDataStore(cache);

        // Seed
        await store.GetResponseForPhoneAsync("7198004382");

        var patchBody = JsonSerializer.Serialize(new
        {
            sebtChldId = "999999999",
            gurdFstNm = "Nobody"
        });

        var result = await store.ApplyPatchAsync(patchBody);
        Assert.Contains("404", result);
        Assert.Contains("Not Found", result);
    }
}
