
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

internal static class DistributedCacheExtensions
{
    public static Task SetAsync<T>(
        this IDistributedCache cache,
        string key, 
        T value, 
        DistributedCacheEntryOptions options,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(value);
        var bytes = Encoding.UTF8.GetBytes(json);
        return cache.SetAsync(key, bytes, options, cancellationToken);
    }

    public static async Task<T?> GetAsync<T>(
        this IDistributedCache cache,
        string key,
        CancellationToken cancellationToken = default
    ) where T : class
    {
        var bytes = await cache.GetAsync(key, cancellationToken);
      
        if (bytes is null)
        {
            return null;
        }

        var json = Encoding.UTF8.GetString(bytes);

        return JsonSerializer.Deserialize<T>(json);
    }
}