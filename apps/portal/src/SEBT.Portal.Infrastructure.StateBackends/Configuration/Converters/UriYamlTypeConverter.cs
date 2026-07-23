namespace SEBT.Portal.Infrastructure.StateBackends.Configuration.Converters;

using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

/// <summary>
/// YamlDotNet v18 IYamlTypeConverter: ReadYaml now takes (IParser, Type, ObjectDeserializer).
/// </summary>
internal sealed class UriYamlTypeConverter : IYamlTypeConverter
{
    public bool Accepts(Type type)
    {
        return type == typeof(Uri);
    }

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        Scalar scalar = parser.Consume<Scalar>();
        return new Uri(scalar.Value);
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        Uri? uri = value as Uri;
        emitter.Emit(new Scalar(uri?.ToString() ?? string.Empty));
    }
}
