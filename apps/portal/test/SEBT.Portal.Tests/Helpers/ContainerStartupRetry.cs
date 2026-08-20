using DotNet.Testcontainers.Containers;

namespace SEBT.Portal.Tests.Helpers;

/// <summary>
/// Retries Testcontainers startup with a backoff delay. Pulling images from
/// mcr.microsoft.com occasionally hits a transient network timeout in CI; a single
/// retry-free attempt turns that blip into a full test run failure.
/// </summary>
public static class ContainerStartupRetry
{
    public static async Task StartWithRetryAsync(
        this IContainer container,
        int maxAttempts = 3,
        TimeSpan? delay = null,
        CancellationToken cancellationToken = default)
    {
        var retryDelay = delay ?? TimeSpan.FromSeconds(5);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await container.StartAsync(cancellationToken);
                return;
            }
            catch when (attempt < maxAttempts)
            {
                await Task.Delay(retryDelay * attempt, cancellationToken);
            }
        }
    }
}
