using System.Text.Json;
using SEBT.Portal.Infrastructure.StateBackends.Mapping;

namespace SEBT.Portal.Tests.Unit.Infrastructure.StateBackends;

public class JsonPathSelectorTests
{
    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    [Fact]
    public void Select_ResolvesNestedDottedPath()
    {
        JsonElement root = Parse("""{ "outer": { "inner": "value" } }""");

        JsonElement result = JsonPathSelector.Select(root, "outer.inner");

        Assert.Equal(JsonValueKind.String, result.ValueKind);
        Assert.Equal("value", result.GetString());
    }

    [Fact]
    public void Select_ResolvesArrayIndexSelector()
    {
        JsonElement root = Parse("""{ "rows": [ { "id": "a" }, { "id": "b" } ] }""");

        JsonElement result = JsonPathSelector.Select(root, "rows[1].id");

        Assert.Equal("b", result.GetString());
    }

    [Fact]
    public void Select_ResolvesDollarRootedPath()
    {
        JsonElement root = Parse("""{ "data": { "count": 3 } }""");

        JsonElement result = JsonPathSelector.Select(root, "$.data.count");

        Assert.Equal(3, result.GetInt32());
    }

    [Fact]
    public void Select_ReturnsDefault_WhenPathMissing()
    {
        JsonElement root = Parse("""{ "present": "value" }""");

        JsonElement result = JsonPathSelector.Select(root, "absent.child");

        Assert.Equal(JsonValueKind.Undefined, result.ValueKind);
    }
}
