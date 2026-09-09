using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Caching.Hybrid;

namespace SEBT.Portal.StatePlugins.CO.CbmsApi.Mocks;

/// <summary>
/// Cache-backed mock data store for CBMS API responses. Seeds from embedded
/// JSON files on first access, indexed by phone number via a manifest file.
/// Supports read (phone lookup) and write (PATCH mutation) operations.
/// Thread safety is provided by HybridCache.
/// </summary>
public sealed class MockCbmsDataStore
{
    private const string CacheKeyPrefix = "cbms-mock:";
    private const string ManifestResourceName = "SEBT.Portal.StatePlugins.CO.CbmsApi.TestData.CbmsMocks.mock-manifest.json";
    private const string ResourcePrefix = "SEBT.Portal.StatePlugins.CO.CbmsApi.TestData.CbmsMocks.";

    // L2 (Redis): 30 days — mock data should survive long-running deployments.
    // L1 (in-memory): default (~1 minute) — short enough that PATCH mutations
    // from other instances propagate quickly via Redis.
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromDays(30)
    };

    // Embedded JSON files may contain JavaScript-style comments (// …).
    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly string EmptySuccessResponse =
        JsonSerializer.Serialize(new { stdntEnrollDtls = Array.Empty<object>(), respCd = "00", respMsg = "Success" });

    private static readonly string NotFoundResponse = JsonSerializer.Serialize(new
    {
        apiName = "cbms-sebt-eapi-impl",
        correlationId = "mock",
        timestamp = DateTimeOffset.UtcNow.ToString("o"),
        errorDetails = new[] { new { code = "404", message = "Not Found" } }
    });

    private static readonly string SuccessResponse =
        JsonSerializer.Serialize(new { respCd = "00", respMsg = "Success" });

    private readonly HybridCache _cache;
    private readonly SemaphoreSlim _seedLock = new(1, 1);
    private volatile bool _seeded;

    // Phone numbers from the manifest, populated during seeding.
    // Used by ApplyPatchAsync to search across all households.
    private IReadOnlyList<string> _knownPhones = [];

    public MockCbmsDataStore(HybridCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        _cache = cache;
    }

    public async Task<string> GetResponseForPhoneAsync(string normalizedPhone, CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken).ConfigureAwait(false);

        var cacheKey = CacheKeyPrefix + normalizedPhone;
        var result = await _cache.GetOrCreateAsync(
            cacheKey,
            cancellationToken: cancellationToken,
            options: CacheOptions,
            factory: (ct) => ValueTask.FromResult<string?>(null)).ConfigureAwait(false);

        return result ?? EmptySuccessResponse;
    }

    public async Task<string> ApplyPatchAsync(string requestBodyJson, CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken).ConfigureAwait(false);

        using var patchDoc = JsonDocument.Parse(requestBodyJson, JsonOptions);
        var root = patchDoc.RootElement;

        if (root.ValueKind == JsonValueKind.Array)
        {
            var any = false;
            foreach (var el in root.EnumerateArray())
            {
                if (await TryApplySinglePatchAsync(el, cancellationToken).ConfigureAwait(false))
                    any = true;
            }

            return any ? SuccessResponse : NotFoundResponse;
        }

        return await TryApplySinglePatchAsync(root, cancellationToken).ConfigureAwait(false)
            ? SuccessResponse
            : NotFoundResponse;
    }

    private async Task<bool> TryApplySinglePatchAsync(JsonElement patchRoot, CancellationToken cancellationToken)
    {
        var sebtChldId = TryGetSebtChldIdString(patchRoot);
        if (string.IsNullOrEmpty(sebtChldId))
            return false;

        foreach (var phone in _knownPhones)
        {
            var cacheKey = CacheKeyPrefix + phone;
            var json = await _cache.GetOrCreateAsync(
                cacheKey,
                cancellationToken: cancellationToken,
                options: CacheOptions,
                factory: (ct) => ValueTask.FromResult<string?>(null)).ConfigureAwait(false);

            if (json == null) continue;

            using var householdDoc = JsonDocument.Parse(json, JsonOptions);
            var students = householdDoc.RootElement.GetProperty("stdntEnrollDtls");

            for (var i = 0; i < students.GetArrayLength(); i++)
            {
                var student = students[i];
                var studentChldId = student.TryGetProperty("sebtChldId", out var idEl)
                    ? idEl.ValueKind == JsonValueKind.Number ? idEl.GetInt32().ToString() : idEl.GetString()
                    : null;

                if (studentChldId != sebtChldId) continue;

                var mutated = ApplyMutations(json, i, patchRoot);
                await _cache.SetAsync(cacheKey, mutated, options: CacheOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
                return true;
            }
        }

        return false;
    }

    private static string? TryGetSebtChldIdString(JsonElement el)
    {
        if (!el.TryGetProperty("sebtChldId", out var chldIdEl)) return null;
        return chldIdEl.ValueKind == JsonValueKind.Number
            ? chldIdEl.GetInt32().ToString()
            : chldIdEl.GetString();
    }

    private async Task EnsureSeededAsync(CancellationToken cancellationToken)
    {
        if (_seeded) return;

        await _seedLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_seeded) return;

            var manifest = LoadManifest();
            var phones = new List<string>();

            foreach (var (phone, fileName) in manifest)
            {
                var rawJson = LoadEmbeddedJson(fileName);
                // Re-serialize to strip comments from embedded JSON files.
                var cleanJson = NormalizeJson(rawJson);
                var cacheKey = CacheKeyPrefix + phone;
                await _cache.SetAsync(cacheKey, cleanJson, options: CacheOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
                phones.Add(phone);
            }

            _knownPhones = phones;
            _seeded = true;
        }
        finally
        {
            _seedLock.Release();
        }
    }

    private static Dictionary<string, string> LoadManifest()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ManifestResourceName)
            ?? throw new InvalidOperationException($"Mock manifest not found: {ManifestResourceName}");
        using var doc = JsonDocument.Parse(stream);
        var households = doc.RootElement.GetProperty("households");

        var result = new Dictionary<string, string>();
        foreach (var prop in households.EnumerateObject())
        {
            result[prop.Name] = prop.Value.GetString()
                ?? throw new InvalidOperationException($"Null filename for phone {prop.Name} in manifest");
        }
        return result;
    }

    private static string LoadEmbeddedJson(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = ResourcePrefix + fileName;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Mock household JSON not found: {resourceName}. Ensure TestData/CbmsMocks/{fileName} is an EmbeddedResource.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Parse JSON with comment/trailing-comma tolerance, then re-serialize
    /// to produce clean, standards-compliant JSON for cache storage.
    /// </summary>
    private static string NormalizeJson(string rawJson)
    {
        using var doc = JsonDocument.Parse(rawJson, JsonOptions);
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            doc.RootElement.WriteTo(writer);
        }
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    private static string ApplyMutations(string householdJson, int studentIndex, JsonElement patch)
    {
        using var doc = JsonDocument.Parse(householdJson, JsonOptions);
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            writer.WriteStartObject();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name == "stdntEnrollDtls")
                {
                    writer.WritePropertyName("stdntEnrollDtls");
                    writer.WriteStartArray();
                    var idx = 0;
                    foreach (var student in prop.Value.EnumerateArray())
                    {
                        if (idx == studentIndex)
                            WriteStudentWithMutations(writer, student, patch);
                        else
                            student.WriteTo(writer);
                        idx++;
                    }
                    writer.WriteEndArray();
                }
                else
                {
                    prop.WriteTo(writer);
                }
            }
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void WriteStudentWithMutations(Utf8JsonWriter writer, JsonElement student, JsonElement patch)
    {
        var directFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "gurdFstNm", "gurdLstNm", "gurdEmailAddr"
        };

        var addressFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "addrLn1", "addrLn2", "cty", "staCd", "zip", "zip4"
        };

        var overrides = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in directFields)
        {
            if (patch.TryGetProperty(field, out var val))
                overrides[field] = val;
        }
        if (patch.TryGetProperty("addr", out var addrEl))
        {
            foreach (var field in addressFields)
            {
                if (addrEl.TryGetProperty(field, out var val))
                    overrides[field] = val;
            }
        }

        writer.WriteStartObject();
        foreach (var prop in student.EnumerateObject())
        {
            if (overrides.TryGetValue(prop.Name, out var overrideVal))
            {
                writer.WritePropertyName(prop.Name);
                overrideVal.WriteTo(writer);
            }
            else
            {
                prop.WriteTo(writer);
            }
        }
        writer.WriteEndObject();
    }
}
