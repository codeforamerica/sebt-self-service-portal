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

    [Fact]
    public void Select_ReturnsDefault_WhenIndexIsInRangeOfGrammarButMissing()
    {
        JsonElement root = Parse("""{ "rows": [ { "id": "a" } ] }""");

        JsonElement result = JsonPathSelector.Select(root, "rows[5]");

        Assert.Equal(JsonValueKind.Undefined, result.ValueKind);
    }

    // Negative, non-numeric, overflowing, unclosed, and empty segments throw — they used to
    // surface as IndexOutOfRange / Format / Overflow on the first request.
    [Theory]
    [InlineData("[-1]")]
    [InlineData("[x]")]
    [InlineData("[2147483648]")]
    [InlineData("resultSets[0")]
    [InlineData("a..b")]
    public void Select_Throws_WhenPathIsMalformed(string path)
    {
        JsonElement root = Parse("""{ "a": { "b": 1 }, "resultSets": [ {} ] }""");

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => JsonPathSelector.Select(root, path));
        Assert.Contains(path, ex.Message);
    }
}
