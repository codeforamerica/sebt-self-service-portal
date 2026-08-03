namespace SEBT.Portal.Infrastructure.StateBackends.Configuration.Converters;

using SEBT.Portal.Core.StateBackends.Configuration.Operations;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

/// <summary>
/// Hydrates a <see cref="FieldSources"/> from either a scalar (<c>from: X</c>) or a sequence
/// (<c>from: [X, Y]</c>). Most fields bind a single source; the keyword-rules primitive may list more.
/// </summary>
internal sealed class FieldSourcesYamlTypeConverter : IYamlTypeConverter
{
    public bool Accepts(Type type)
    {
        return type == typeof(FieldSources);
    }

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        if (parser.Current is SequenceStart)
        {
            parser.Consume<SequenceStart>();
            var sources = new List<string>();
            while (parser.Current is not SequenceEnd)
            {
                sources.Add(parser.Consume<Scalar>().Value);
            }

            parser.Consume<SequenceEnd>();
            return new FieldSources(sources);
        }

        Scalar scalar = parser.Consume<Scalar>();
        return new FieldSources(new[] { scalar.Value });
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        throw new NotSupportedException("State-backend configs are never serialized to YAML.");
    }
}
