using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SEBT.Portal.Infrastructure.Configuration;

/// <summary>
/// Configuration provider that reads feature flags from AWS AppConfig Agent.
/// Uses the agent's HTTP API instead of direct AWS SDK calls
/// </summary>
public sealed class AppConfigAgentConfigurationProvider : ConfigurationProvider, IDisposable
{
    private const int LockReleaseTimeout = 3_000;
    private const int InitialLoadMaxRetries = 10;
    private static readonly TimeSpan InitialLoadRetryDelay = TimeSpan.FromSeconds(1);

    private readonly HttpClient _httpClient;
    private readonly AppConfigAgentProfile _profile;
    private readonly SemaphoreSlim _lock;
    private readonly ILogger<AppConfigAgentConfigurationProvider>? _logger;
    private readonly bool _ownsHttpClient;

    private int _isLoading; // 0 = not loading, 1 = loading
    private volatile bool _disposed;
    private bool _initialLoadCompleted;

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
        if (_disposed)
            return;

        if (Interlocked.CompareExchange(ref _isLoading, 1, 0) != 0)
            return;

        try
        {
            if (!_initialLoadCompleted)
            {
                _logger?.LogInformation(
                    "Initial load starting for profile {ProfileId} (will retry up to {MaxRetries} times if agent is not ready)",
                    _profile.ProfileId,
                    InitialLoadMaxRetries);
                LoadWithRetryAsync().GetAwaiter().GetResult();
                _initialLoadCompleted = true;
            }
            else
            {
                LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
        }
        finally
        {
            Interlocked.Exchange(ref _isLoading, 0);
        }
    }

    /// <summary>
    /// Re-fetches configuration from the AppConfig Agent. Called by the reload background service
    /// on its polling interval. Only raises a change notification when the configuration actually
    /// changed.
    /// </summary>
    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }

        await LoadAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Attempts to load configuration with retries. Used only for the initial load
    /// to handle the race condition where the AppConfig Agent sidecar may not be
    /// ready when the API container starts.
    /// </summary>
    private async Task LoadWithRetryAsync()
    {
        for (var attempt = 1; attempt <= InitialLoadMaxRetries; attempt++)
        {
            try
            {
                await LoadAsync(CancellationToken.None).ConfigureAwait(false);

                // If Data has items, the load succeeded.
                if (Data.Count > 0)
                    return;
            }
            catch
            {
                // LoadAsync handles its own exceptions — this is a safety net.
            }

            if (attempt < InitialLoadMaxRetries)
            {
                _logger?.LogInformation(
                    "AppConfig Agent not ready (attempt {Attempt}/{MaxRetries}). Retrying in {Delay}s...",
                    attempt,
                    InitialLoadMaxRetries,
                    InitialLoadRetryDelay.TotalSeconds);
                await Task.Delay(InitialLoadRetryDelay).ConfigureAwait(false);
            }
        }

        _logger?.LogError(
            "AppConfig Agent not available after {MaxRetries} attempts for profile {ProfileId}. Starting without AppConfig values.",
            InitialLoadMaxRetries,
            _profile.ProfileId);
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        // ConfigureAwait(false) throughout to avoid deadlock when Load() is called synchronously (e.g. from tests or config build).
        if (!await _lock.WaitAsync(LockReleaseTimeout, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            var endpointUrl = _profile.GetEndpointUrl();
            _logger?.LogDebug("Fetching configuration from AppConfig Agent: {EndpointUrl}", endpointUrl);

            using var response = await _httpClient.GetAsync(endpointUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

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

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            // Parse the configuration from the AppConfig Agent response
            var parsedData = ParseConfig(stream, contentType);

            if (parsedData.Count > 0)
            {
                var changed = !ConfigurationEquals(parsedData, Data);
                Data = parsedData;
                if (changed)
                {
                    OnReload();
                    _logger?.LogInformation(
                        "Loaded {Count} configuration items from AppConfig Agent for profile {ProfileId}",
                        parsedData.Count,
                        _profile.ProfileId);
                }
                else
                {
                    _logger?.LogDebug(
                        "No configuration changes for profile {ProfileId} ({Count} items)",
                        _profile.ProfileId,
                        parsedData.Count);
                }
            }
            else
            {
                _logger?.LogWarning(
                    "AppConfig Agent returned empty configuration for profile {ProfileId}. Configuration will not be updated.",
                    _profile.ProfileId);
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

    private static bool ConfigurationEquals(IDictionary<string, string?> incomingData,
        IDictionary<string, string?> existingData)
    {
        if (incomingData.Count != existingData.Count)
        {
            return false;
        }

        foreach (var kvp in incomingData)
        {
            if (!existingData.TryGetValue(kvp.Key, out var existingValue) || existingValue != kvp.Value)
            {
                return false;
            }
        }
        return true;
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
            FlattenJsonObject(doc.RootElement, result, "");
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
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    FlattenJsonElement(item, result, $"{key}:{index}");
                    index++;
                }
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
        if (_disposed)
            return;

        _disposed = true;

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
        var profile = $"{_profile.ApplicationId}:{_profile.EnvironmentId}:{_profile.ProfileId}";
        var isFeatureFlag = _profile.IsFeatureFlag ? " (Feature Flag)" : string.Empty;

        return $"{className} - {profile}{isFeatureFlag}";
    }
}
