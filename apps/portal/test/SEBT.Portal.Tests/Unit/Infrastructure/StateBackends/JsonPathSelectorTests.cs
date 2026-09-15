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

    // Supported grammar: dotted segments, [index] element access, optional $ root.
    [Theory]
    [InlineData("""{ "outer": { "inner": "value" } }""", "outer.inner", "\"value\"")]
    [InlineData("""{ "rows": [ { "id": "a" }, { "id": "b" } ] }""", "rows[1].id", "\"b\"")]
    [InlineData("""{ "data": { "count": 3 } }""", "$.data.count", "3")]
    public void Select_ResolvesSupportedPathGrammar(string json, string path, string expectedRawText)
    {
        JsonElement root = Parse(json);

        JsonElement result = JsonPathSelector.Select(root, path);

        Assert.Equal(expectedRawText, result.GetRawText());
    }

    [Fact]
    public void Select_ReturnsDefault_WhenPathMissing()
    {
        JsonElement root = Parse("""{ "present": "value" }""");

        JsonElement result = JsonPathSelector.Select(root, "absent.child");

        Assert.Equal(JsonValueKind.Undefined, result.ValueKind);
    }
}
