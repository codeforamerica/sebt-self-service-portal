using System.Text.Json.Nodes;
using SEBT.Portal.Infrastructure.StateBackends.Mapping;

namespace SEBT.Portal.Tests.Unit.Infrastructure.StateBackends;

public class JsonPathWriterTests
{
    [Fact]
    public void Write_SetsFlatProperty()
    {
        var root = new JsonObject();

        JsonPathWriter.Write(root, "name", JsonValue.Create("value"));

        Assert.Equal("value", root["name"]!.GetValue<string>());
    }

    [Fact]
    public void Write_CreatesIntermediateObjectsForNestedPath()
    {
        var root = new JsonObject();

        JsonPathWriter.Write(root, "outer.inner", JsonValue.Create("value"));

        JsonObject outer = Assert.IsType<JsonObject>(root["outer"]);
        Assert.Equal("value", outer["inner"]!.GetValue<string>());
    }

    [Fact]
    public void Write_OverwritesExistingValue()
    {
        var root = new JsonObject { ["name"] = "original" };

        JsonPathWriter.Write(root, "name", JsonValue.Create("replacement"));

        Assert.Equal("replacement", root["name"]!.GetValue<string>());
    }

    [Fact]
    public void Write_Throws_WhenPathHasEmptySegment()
    {
        var root = new JsonObject();

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => JsonPathWriter.Write(root, "a..b", JsonValue.Create("value")));
        Assert.Contains("a..b", ex.Message);
    }

    [Fact]
    public void Write_Throws_WhenPathUsesIndexGrammar()
    {
        var root = new JsonObject();

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => JsonPathWriter.Write(root, "rows[0]", JsonValue.Create("value")));
        Assert.Contains("rows[0]", ex.Message);
    }
}
