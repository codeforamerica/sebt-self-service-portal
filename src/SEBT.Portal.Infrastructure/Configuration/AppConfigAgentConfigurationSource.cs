using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SEBT.Portal.Infrastructure.Configuration;

/// <summary>
/// Configuration source for AWS AppConfig Agent.
/// </summary>
public class AppConfigAgentConfigurationSource : IConfigurationSource
{
    /// <summary>
    /// Gets or sets the HTTP client to use for agent requests.
    /// </summary>
    public HttpClient? HttpClient { get; set; }

    /// <summary>
    /// Gets or sets the agent profile configuration.
    /// </summary>
    public AppConfigAgentProfile Profile { get; set; } = null!;

    /// <summary>
    /// Gets or sets the optional logger.
    /// </summary>
    public ILogger<AppConfigAgentConfigurationProvider>? Logger { get; set; }

    /// <summary>
    /// Builds the configuration provider for this source.
    /// </summary>
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        var ownsHttpClient = HttpClient == null;
        var httpClient = HttpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        return new AppConfigAgentConfigurationProvider(httpClient, Profile, Logger, ownsHttpClient);
    }
}
