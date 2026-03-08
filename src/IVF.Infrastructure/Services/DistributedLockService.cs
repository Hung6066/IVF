using IVF.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace IVF.Infrastructure.Services;

/// <summary>
/// Redis-backed distributed lock using SETNX (SET if Not eXists) with TTL.
/// Inspired by Redlock algorithm but simplified for single-Redis deployments.
/// Uses a unique owner ID per lock to ensure only the owner can release.
/// </summary>
public sealed class DistributedLockService : IDistributedLockService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<DistributedLockService> _logger;

    public DistributedLockService(
        IConnectionMultiplexer redis,
        ILogger<DistributedLockService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<IAsyncDisposable?> TryAcquireAsync(string lockKey, TimeSpan expiry, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var ownerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

        // SETNX with expiry — atomic operation
        var acquired = await db.StringSetAsync(lockKey, ownerId, expiry, When.NotExists);

        if (!acquired)
        {
            _logger.LogDebug("Failed to acquire distributed lock {LockKey} — already held", lockKey);
            return null;
        }

        _logger.LogDebug("Acquired distributed lock {LockKey} with owner {OwnerId}, expiry {Expiry}", lockKey, ownerId, expiry);
        return new LockHandle(db, lockKey, ownerId, _logger);
    }

    private sealed class LockHandle : IAsyncDisposable
    {
        private readonly IDatabase _db;
        private readonly string _lockKey;
        private readonly string _ownerId;
        private readonly ILogger _logger;
        private int _disposed;

        // Lua script to atomically check owner and delete (prevents releasing someone else's lock)
        private const string ReleaseLuaScript = """
            if redis.call('get', KEYS[1]) == ARGV[1] then
                return redis.call('del', KEYS[1])
            else
                return 0
            end
            """;

        public LockHandle(IDatabase db, string lockKey, string ownerId, ILogger logger)
        {
            _db = db;
            _lockKey = lockKey;
            _ownerId = ownerId;
            _logger = logger;
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                return;

            try
            {
                var result = await _db.ScriptEvaluateAsync(
                    ReleaseLuaScript,
                    [(RedisKey)_lockKey],
                    [(RedisValue)_ownerId]);

                if ((int)result == 1)
                    _logger.LogDebug("Released distributed lock {LockKey}", _lockKey);
                else
                    _logger.LogWarning("Lock {LockKey} was not released — owner mismatch (lock expired or stolen)", _lockKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to release distributed lock {LockKey}", _lockKey);
            }
        }
    }
}
