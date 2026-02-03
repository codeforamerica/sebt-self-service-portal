namespace SEBT.Portal.Infrastructure.Configuration;

/// <summary>
/// Profile configuration for AWS AppConfig Agent.
/// </summary>
public class AppConfigAgentProfile
{
    /// <summary>
    /// Base URL of the AppConfig Agent (default: http://localhost:2772).
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:2772";

    /// <summary>
    /// Application identifier.
    /// </summary>
    public required string ApplicationId { get; set; }

    /// <summary>
    /// Environment identifier.
    /// </summary>
    public required string EnvironmentId { get; set; }

    /// <summary>
    /// Configuration profile identifier.
    /// </summary>
    public required string ProfileId { get; set; }

    /// <summary>
    /// Reload interval in seconds (default: 90).
    /// </summary>
    public int? ReloadAfterSeconds { get; set; } = 90;

    /// <summary>
    /// Whether this is a feature flag profile.
    /// </summary>
    public bool IsFeatureFlag { get; set; } = true;

    /// <summary>
    /// Gets the agent endpoint URL for this profile.
    /// </summary>
    public string GetEndpointUrl()
    {
        var baseUrl = BaseUrl.TrimEnd('/');
        return $"{baseUrl}/applications/{ApplicationId}/environments/{EnvironmentId}/configurations/{ProfileId}";
    }
}
