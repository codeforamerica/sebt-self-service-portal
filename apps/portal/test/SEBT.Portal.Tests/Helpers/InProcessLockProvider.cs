using System.Collections.Concurrent;
using Medallion.Threading;

namespace SEBT.Portal.Tests.Helpers;

/// <summary>
/// In-process <see cref="IDistributedLockProvider"/> backed by per-key <see cref="SemaphoreSlim"/>s.
/// Used in tests that need real lock semantics (blocking, not mock) without an external service
/// like Redis or SQL Server. Provides the same mutual-exclusion guarantees as
/// <c>RedisDistributedSynchronizationProvider</c> or <c>SqlDistributedSynchronizationProvider</c>
/// for tests where the lock semantics matter but a distributed backing store does not.
/// </summary>
public sealed class InProcessLockProvider : IDistributedLockProvider
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new();

    public IDistributedLock CreateLock(string name) =>
        new InProcessLock(name, _semaphores.GetOrAdd(name, _ => new SemaphoreSlim(1, 1)));

    private sealed class InProcessLock(string name, SemaphoreSlim semaphore) : IDistributedLock
    {
        public string Name => name;

        public IDistributedSynchronizationHandle? TryAcquire(TimeSpan timeout = default, CancellationToken cancellationToken = default)
        {
            if (!semaphore.Wait(timeout, cancellationToken))
                return null;
            return new Handle(semaphore);
        }

        public IDistributedSynchronizationHandle Acquire(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            if (timeout.HasValue)
            {
                if (!semaphore.Wait(timeout.Value, cancellationToken))
                    throw new TimeoutException($"Could not acquire lock '{name}'");
            }
            else
            {
                semaphore.Wait(cancellationToken);
            }
            return new Handle(semaphore);
        }

        public async ValueTask<IDistributedSynchronizationHandle?> TryAcquireAsync(TimeSpan timeout = default, CancellationToken cancellationToken = default)
        {
            var acquired = await semaphore.WaitAsync(timeout, cancellationToken);
            return acquired ? new Handle(semaphore) : null;
        }

        public async ValueTask<IDistributedSynchronizationHandle> AcquireAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            if (timeout.HasValue)
            {
                var acquired = await semaphore.WaitAsync(timeout.Value, cancellationToken);
                if (!acquired)
                    throw new TimeoutException($"Could not acquire lock '{name}'");
            }
            else
            {
                await semaphore.WaitAsync(cancellationToken);
            }
            return new Handle(semaphore);
        }
    }

    private sealed class Handle(SemaphoreSlim semaphore) : IDistributedSynchronizationHandle
    {
        public CancellationToken HandleLostToken => CancellationToken.None;
        public void Dispose() => semaphore.Release();
        public ValueTask DisposeAsync()
        {
            semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }
}
