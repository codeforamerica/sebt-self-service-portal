using System.Runtime.CompilerServices;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Serialization.Json;

namespace SEBT.Portal.StatePlugins.CO.Tests.CbmsApi;

internal static class KiotaSerializerSetup
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        ApiClientBuilder.RegisterDefaultSerializer<JsonSerializationWriterFactory>();
        ApiClientBuilder.RegisterDefaultDeserializer<JsonParseNodeFactory>();
    }
}
