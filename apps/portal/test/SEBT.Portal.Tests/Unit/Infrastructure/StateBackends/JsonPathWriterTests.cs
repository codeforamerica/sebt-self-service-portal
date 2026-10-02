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
}
