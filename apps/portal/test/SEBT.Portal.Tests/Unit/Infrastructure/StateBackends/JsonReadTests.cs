using System.Text.Json;
using SEBT.Portal.Infrastructure.StateBackends.Mapping;

namespace SEBT.Portal.Tests.Unit.Infrastructure.StateBackends;

/// <summary>
/// Coercion contract for <see cref="JsonRead.AsString(JsonElement, string)"/>: numbers read as
/// raw JSON text, bools as "true"/"false", and every miss is <c>null</c> — never a throw.
/// </summary>
public class JsonReadTests
{
    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    [Theory]
    [InlineData("""{ "field": "value" }""", "field", "value")]
    [InlineData("""{ "count": 3 }""", "count", "3")]
    // Raw read preserves the exact source text of a numeric token, unreformatted.
    [InlineData("""{ "score": 0.50 }""", "score", "0.50")]
    [InlineData("""{ "isEligible": true }""", "isEligible", "true")]
    [InlineData("""{ "isEligible": false }""", "isEligible", "false")]
    public void AsString_CoercesValueToRawText(string json, string field, string expected)
    {
        JsonElement root = Parse(json);

        Assert.Equal(expected, JsonRead.AsString(root, field));
    }

    [Theory]
    [InlineData("""{ "present": "value" }""", "absent")] // property absent
    [InlineData("""{ "field": null }""", "field")] // value is JSON null
    [InlineData("""[ "a", "b" ]""", "field")] // parent is not an object
    public void AsString_ReturnsNull_OnAnyMiss(string json, string field)
    {
        JsonElement root = Parse(json);

        Assert.Null(JsonRead.AsString(root, field));
    }

    [Fact]
    public void AsString_ReturnsNull_WhenNullableParentIsNull()
    {
        JsonElement? body = null;

        Assert.Null(JsonRead.AsString(body, "field"));
    }

    [Fact]
    public void AsString_ReadsFromNullableParent_WhenPresent()
    {
        JsonElement? body = Parse("""{ "respCd": "00" }""");

        Assert.Equal("00", JsonRead.AsString(body, "respCd"));
    }
}
