using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Configuration for the Socure identity verification integration.
/// In non-Development environments, WebhookSecret is required at startup.
/// </summary>
public class SocureSettings
{
    public static readonly string SectionName = "Socure";

    /// <summary>
    /// When false, Socure integration is disabled entirely — validation is skipped
    /// and a no-op client is registered. States that don't use Socure leave this false.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// When true, uses the StubSocureClient instead of the real HTTP client.
    /// Automatically true in Development when no API key is configured.
    /// </summary>
    [DefaultValue(true)]
    public bool UseStub { get; set; } = true;

    /// <summary>
    /// Socure API key for authenticating backend requests.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Base URL for the Socure API (sandbox or production).
    /// </summary>
    public string BaseUrl { get; set; } = "https://riskos.sandbox.socure.com";

    /// <summary>
    /// Secret used to validate incoming webhook signatures.
    /// Required in non-Development environments.
    /// </summary>
    public string? WebhookSecret { get; set; }

    /// <summary>
    /// How long a challenge remains valid before expiring, in minutes.
    /// </summary>
    [Range(1, 1440, ErrorMessage = "ChallengeExpirationMinutes must be between 1 and 1440.")]
    [DefaultValue(30)]
    public int ChallengeExpirationMinutes { get; set; } = 30;

    /// <summary>
    /// Socure API version header value.
    /// </summary>
    public string ApiVersion { get; set; } = "2025-01-01.orion";

    /// <summary>
    /// Socure workflow name for evaluation requests.
    /// Configurable per environment — may differ between sandbox and production.
    /// </summary>
    public string Workflow { get; set; } = "consumer_onboarding";

    /// <summary>
    /// Identifier used to locate the DocV enrichment in the evaluation response.
    /// Matched against the <c>enrichment_provider</c> field — not <c>enrichment_name</c>,
    /// which varies by workflow (e.g. "Socure Document Request - Default Flow" in sandbox).
    /// </summary>
    public string DocvEnrichmentName { get; set; } = "SocureDocRequest";
}
