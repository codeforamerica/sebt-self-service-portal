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

    public static StateBackendConfiguration Load(string yaml)
    {
        StateBackendConfiguration configuration =
            LazyDeserializer.Value.Deserialize<StateBackendConfiguration>(yaml);

        // Fail loud at LOAD time on any malformed config shape, folding in every check that used to
        // fire lazily at first-request dispatch — an invalid config never loads clean.
        StateBackendConfigurationValidator.Validate(configuration);

        return configuration;
    }

    private static IDeserializer BuildDeserializer()
    {
        return new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithTypeConverter(new UriYamlTypeConverter())
            .WithTypeConverter(new FieldSourcesYamlTypeConverter())
            // Infer bool/number for unquoted scalars bound to `object` (e.g. a request binding's
            // `const: true` must hydrate as a real bool so the emitted JSON body is `true`, not "true").
            .WithAttemptingUnquotedStringTypeDeserialization()
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
