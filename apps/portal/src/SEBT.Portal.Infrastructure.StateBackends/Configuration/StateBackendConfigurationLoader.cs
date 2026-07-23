using SEBT.Portal.Core.StateBackends.Configuration;
using SEBT.Portal.Core.StateBackends.Configuration.Auth;
using SEBT.Portal.Infrastructure.StateBackends.Configuration.Converters;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SEBT.Portal.Infrastructure.StateBackends.Configuration;

internal static class StateBackendConfigurationLoader
{
    private static readonly Lazy<IDeserializer> LazyDeserializer =
        new(BuildDeserializer);

    public static StateBackendConfiguration Load(string yaml) =>
        LazyDeserializer.Value.Deserialize<StateBackendConfiguration>(yaml);

    private static IDeserializer BuildDeserializer()
    {
        return new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithTypeConverter(new UriYamlTypeConverter())
            .WithEnforceRequiredMembers()
            .IgnoreUnmatchedProperties()
            .WithTypeDiscriminatingNodeDeserializer(options =>
            {
                options.AddKeyValueTypeDiscriminator<StateBackendAuthScheme>(
                    "scheme",
                    new Dictionary<string, Type>
                    {
                        ["api_key"] = typeof(StateBackendApiKeyAuthScheme),
                        ["client_credentials"] = typeof(StateBackendOAuthClientCredentialsAuthScheme),
                    });
            })
            .Build();
    }
}
