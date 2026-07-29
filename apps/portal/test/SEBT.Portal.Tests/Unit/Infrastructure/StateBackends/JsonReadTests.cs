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

    [Fact]
    public void AsString_ReadsStringValue()
    {
        JsonElement root = Parse("""{ "field": "value" }""");

        Assert.Equal("value", JsonRead.AsString(root, "field"));
    }

    [Fact]
    public void AsString_ReadsNumberAsRawText()
    {
        JsonElement root = Parse("""{ "count": 3 }""");

        Assert.Equal("3", JsonRead.AsString(root, "count"));
    }

    [Fact]
    public void AsString_PreservesNumericLeadingZerosViaRawText()
    {
        // Raw read preserves the exact source text of a numeric token, unreformatted.
        JsonElement root = Parse("""{ "score": 0.50 }""");

        Assert.Equal("0.50", JsonRead.AsString(root, "score"));
    }

    [Fact]
    public void AsString_ReadsTrueAsLiteralTrue()
    {
        JsonElement root = Parse("""{ "isEligible": true }""");

        Assert.Equal("true", JsonRead.AsString(root, "isEligible"));
    }

    [Fact]
    public void AsString_ReadsFalseAsLiteralFalse()
    {
        JsonElement root = Parse("""{ "isEligible": false }""");

        Assert.Equal("false", JsonRead.AsString(root, "isEligible"));
    }

    [Fact]
    public void AsString_ReturnsNull_WhenPropertyAbsent()
    {
        JsonElement root = Parse("""{ "present": "value" }""");

        Assert.Null(JsonRead.AsString(root, "absent"));
    }

    [Fact]
    public void AsString_ReturnsNull_WhenValueIsJsonNull()
    {
        JsonElement root = Parse("""{ "field": null }""");

        Assert.Null(JsonRead.AsString(root, "field"));
    }

    [Fact]
    public void AsString_ReturnsNull_WhenParentNotObject()
    {
        JsonElement root = Parse("""[ "a", "b" ]""");

        Assert.Null(JsonRead.AsString(root, "field"));
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
