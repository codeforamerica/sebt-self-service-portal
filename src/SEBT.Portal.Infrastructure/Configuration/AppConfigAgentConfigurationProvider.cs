using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace SEBT.Portal.Infrastructure.Configuration;

/// <summary>
/// Configuration provider that reads feature flags from AWS AppConfig Agent.
/// Uses the agent's HTTP API instead of direct AWS SDK calls
/// </summary>
public sealed class AppConfigAgentConfigurationProvider : ConfigurationProvider, IDisposable
{
    private const int LockReleaseTimeout = 3_000;

    private readonly HttpClient _httpClient;
    private readonly AppConfigAgentProfile _profile;
    private readonly SemaphoreSlim _lock;
    private readonly ILogger<AppConfigAgentConfigurationProvider>? _logger;
    private readonly bool _ownsHttpClient;

    private IDisposable? _reloadChangeToken;
    private CancellationTokenSource? _reloadTokenSource;
    private int _isLoading; // 0 = not loading, 1 = loading

    public AppConfigAgentConfigurationProvider(
        HttpClient httpClient,
        AppConfigAgentProfile profile,
        ILogger<AppConfigAgentConfigurationProvider>? logger = null,
        bool ownsHttpClient = false)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _logger = logger;
        _ownsHttpClient = ownsHttpClient;
        _lock = new SemaphoreSlim(1, 1);
    }

    public override void Load()
    {
        // Prevent recursive Load when OnReload() causes ConfigurationRoot to call Load again
        if (Interlocked.CompareExchange(ref _isLoading, 1, 0) != 0)
            return;

        try
        {
            LoadAsync().GetAwaiter().GetResult();

            if (_reloadChangeToken is null && _profile.ReloadAfterSeconds.HasValue)
            {
                var delay = TimeSpan.FromSeconds(_profile.ReloadAfterSeconds.Value);

                // Dispose previous token source if it exists
                _reloadTokenSource?.Dispose();
                _reloadTokenSource = new CancellationTokenSource(delay);

                _reloadChangeToken = ChangeToken.OnChange(
                    () => new CancellationChangeToken(_reloadTokenSource.Token),
                    Load
                );
            }
        }
        finally
        {
            Interlocked.Exchange(ref _isLoading, 0);
        }
    }

    private async Task LoadAsync()
    {
        if (!await _lock.WaitAsync(LockReleaseTimeout))
        {
            return;
        }

        try
        {
            var endpointUrl = _profile.GetEndpointUrl();
            _logger?.LogDebug("Fetching configuration from AppConfig Agent: {EndpointUrl}", endpointUrl);

            using var response = await _httpClient.GetAsync(endpointUrl, HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning(
                    "AppConfig Agent returned status {StatusCode} for {EndpointUrl}. Configuration will not be updated.",
                    response.StatusCode,
                    endpointUrl);
                return;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            _logger?.LogDebug("AppConfig Agent returned content type: {ContentType}", contentType);

            await using var stream = await response.Content.ReadAsStreamAsync();

            // Parse the configuration from the AppConfig Agent response
            var parsedData = ParseConfig(stream, contentType);

            if (parsedData.Count > 0)
            {
                Data = parsedData;
                OnReload();
                _logger?.LogInformation(
                    "Loaded {Count} configuration items from AppConfig Agent for profile {ProfileId}",
                    parsedData.Count,
                    _profile.ProfileId);
            }
            else
            {
                _logger?.LogDebug("AppConfig Agent returned empty configuration for profile {ProfileId}", _profile.ProfileId);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogWarning(ex, "Failed to fetch configuration from AppConfig Agent. Configuration will not be updated.");
            // Don't throw - allow app to continue with existing config
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error loading configuration from AppConfig Agent");
            // Don't throw - allow app to continue with existing config
        }
        finally
        {
            _lock.Release();
        }
    }

    private IDictionary<string, string?> ParseConfig(Stream stream, string? contentType)
    {
        if (!string.IsNullOrEmpty(contentType))
        {
            contentType = contentType.Split(";")[0].Trim();
        }

        return contentType switch
        {
            "application/json" when _profile.IsFeatureFlag => ParseFeatureFlagsJson(stream),
            "application/json" => ParseJson(stream),
            _ => throw new FormatException($"AppConfig Agent configuration provider does not support content type: {contentType ?? "Unknown"}")
        };
    }

    private IDictionary<string, string?> ParseFeatureFlagsJson(Stream stream)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var doc = JsonDocument.Parse(stream);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("Feature flags JSON must be an object");
            }

            foreach (var property in root.EnumerateObject())
            {
                var flagName = property.Name;
                var flagValue = property.Value;

                // AppConfig feature flag format: { "flag_name": { "enabled": true } }
                if (flagValue.ValueKind == JsonValueKind.Object)
                {
                    if (flagValue.TryGetProperty("enabled", out var enabledProperty))
                    {
                        if (enabledProperty.ValueKind == JsonValueKind.True || enabledProperty.ValueKind == JsonValueKind.False)
                        {
                            var isEnabled = enabledProperty.GetBoolean();
                            // Keep original flag name (AppConfig uses snake_case, which matches our convention)
                            result[$"FeatureManagement:{flagName}"] = isEnabled.ToString().ToLowerInvariant();
                        }
                    }
                }
                // Simple boolean format: { "flag_name": true }
                else if (flagValue.ValueKind == JsonValueKind.True || flagValue.ValueKind == JsonValueKind.False)
                {
                    var isEnabled = flagValue.GetBoolean();
                    // Keep original flag name
                    result[$"FeatureManagement:{flagName}"] = isEnabled.ToString().ToLowerInvariant();
                }
            }
        }
        catch (JsonException ex)
        {
            _logger?.LogError(ex, "Failed to parse feature flags JSON from AppConfig Agent");
            throw new FormatException("Invalid JSON format in AppConfig Agent response", ex);
        }

        return result;
    }

    private IDictionary<string, string?> ParseJson(Stream stream)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var doc = JsonDocument.Parse(stream);
            FlattenJsonObject(doc.RootElement, result, "FeatureManagement");
        }
        catch (JsonException ex)
        {
            _logger?.LogError(ex, "Failed to parse JSON from AppConfig Agent");
            throw new FormatException("Invalid JSON format in AppConfig Agent response", ex);
        }

        return result;
    }

    private void FlattenJsonObject(JsonElement element, Dictionary<string, string?> result, string prefix)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var key = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}:{property.Name}";
                FlattenJsonElement(property.Value, result, key);
            }
        }
    }

    private void FlattenJsonElement(JsonElement element, Dictionary<string, string?> result, string key)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                FlattenJsonObject(element, result, key);
                break;
            case JsonValueKind.Array:
                // Arrays are not supported in configuration
                _logger?.LogWarning("JSON arrays are not supported in configuration. Skipping key: {Key}", key);
                break;
            case JsonValueKind.String:
                result[key] = element.GetString();
                break;
            case JsonValueKind.Number:
                result[key] = element.GetRawText();
                break;
            case JsonValueKind.True:
                result[key] = "true";
                break;
            case JsonValueKind.False:
                result[key] = "false";
                break;
            case JsonValueKind.Null:
                result[key] = null;
                break;
        }
    }


    public void Dispose()
    {
        _reloadChangeToken?.Dispose();
        _reloadTokenSource?.Dispose();
        _lock?.Dispose();

        // Dispose HttpClient if we own it
        if (_ownsHttpClient)
        {
            _httpClient?.Dispose();
        }
    }

    public override string ToString()
    {
        var className = GetType().Name;
        var profile = $"{_profile.ApplicationId}:{_profile.EnvironmentId}:{_profile.ProfileId}:{_profile.ReloadAfterSeconds}";
        var isFeatureFlag = _profile.IsFeatureFlag ? " (Feature Flag)" : string.Empty;

        return $"{className} - {profile}{isFeatureFlag}";
    }
}
