namespace IVF.Application.Common.Interfaces;

/// <summary>
/// Distributed locking service using Redis SETNX pattern.
/// Prevents concurrent execution of background jobs across multiple replicas.
/// </summary>
public interface IDistributedLockService
{
    /// <summary>
    /// Tries to acquire a distributed lock. Returns an IAsyncDisposable that releases the lock when disposed.
    /// Returns null if the lock is already held by another instance.
    /// </summary>
    /// <param name="lockKey">Unique key for the lock (e.g., "lock:backup-scheduler")</param>
    /// <param name="expiry">Auto-expiry to prevent deadlocks if the holder crashes</param>
    /// <param name="ct">Cancellation token</param>
    Task<IAsyncDisposable?> TryAcquireAsync(string lockKey, TimeSpan expiry, CancellationToken ct = default);
}
