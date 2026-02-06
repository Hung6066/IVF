using System.Collections.Concurrent;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IVF.API.Services;

/// <summary>
/// Singleton service that loads all fingerprint templates into memory
/// and provides high-performance 1:N matching using Parallel processing.
/// </summary>
public class BiometricMatcherService : BackgroundService, IBiometricMatcher
{
    private readonly ILogger<BiometricMatcherService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<Guid, byte[]> _templateCache = new();
    private readonly ConcurrentDictionary<Guid, string> _fingerTypeCache = new();
    
    // DPFP Verification Control
    private readonly DPFP.Verification.Verification _verificator = new();

    private readonly StackExchange.Redis.IConnectionMultiplexer? _redis;
    private readonly IConfiguration _configuration;
    
    // Sharding Config
    private int _shardId = 0;
    private int _totalShards = 1;

    public BiometricMatcherService(
        ILogger<BiometricMatcherService> logger, 
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        StackExchange.Redis.IConnectionMultiplexer? redis = null)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _redis = redis;
        
        _shardId = configuration.GetValue<int>("Matcher:ShardId", 0);
        _totalShards = configuration.GetValue<int>("Matcher:TotalShards", 1);
    }

    public int TemplateCount => _templateCache.Count;
    public bool IsLoaded { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BiometricMatcherService starting...");
        await LoadTemplatesAsync(stoppingToken);
    }

    private async Task LoadTemplatesAsync(CancellationToken stoppingToken)
    {
        // 1. Try Load from Redis
        if (_redis != null && _redis.IsConnected)
        {
            try 
            {
                var db = _redis.GetDatabase();
                var hashEntries = await db.HashGetAllAsync("fingerprints:all");

                if (hashEntries.Length > 0)
                {
                    _logger.LogInformation("Found {Count} templates in Redis. Loading...", hashEntries.Length);
                    
                    int loadedCount = 0;
                    foreach (var entry in hashEntries)
                    {
                        var patientIdStr = entry.Name.ToString();
                        if (Guid.TryParse(patientIdStr, out var patientId))
                        {
                            // Sharding Filter
                            if (Math.Abs(patientId.GetHashCode()) % _totalShards != _shardId) continue;

                            try 
                            {
                                // Format: "FingerType|Base64Data"
                                var parts = entry.Value.ToString().Split('|', 2);
                                if (parts.Length == 2)
                                {
                                    var fingerType = parts[0];
                                    var fingerprintData = Convert.FromBase64String(parts[1]);

                                    _templateCache[patientId] = fingerprintData;
                                    _fingerTypeCache[patientId] = fingerType;
                                    loadedCount++;
                                }
                            }
                            catch { /* invalid data */ }
                        }
                    }
                    
                    IsLoaded = true;
                    _logger.LogInformation("Loaded {Count} templates from Redis (Shard {ShardId}/{Total}).", loadedCount, _shardId, _totalShards);
                    return; 
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load from Redis. Falling back to DB.");
            }
        }

        // 2. Fallback: Load from DB and Populate Redis
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IPatientBiometricsRepository>();
            
            _logger.LogInformation("Loading fingerprint templates from database...");
            var templates = await repo.GetAllFingerprintsAsync(stoppingToken);

            var batchForRedis = new List<StackExchange.Redis.HashEntry>();

            foreach (var t in templates)
            {
                if (t.FingerprintData != null && t.FingerprintData.Length > 0)
                {
                    // Prepare for Redis (Store ALL data regardless of shard)
                    var redisValue = $"{t.FingerType}|{Convert.ToBase64String(t.FingerprintData)}";
                    batchForRedis.Add(new StackExchange.Redis.HashEntry(t.PatientId.ToString(), redisValue));

                    // Load into Memory (Respect Sharding)
                    if (Math.Abs(t.PatientId.GetHashCode()) % _totalShards == _shardId)
                    {
                        _templateCache[t.PatientId] = t.FingerprintData;
                        _fingerTypeCache[t.PatientId] = t.FingerType.ToString();
                    }
                }
            }

            // Populate Redis Async
            if (_redis != null && _redis.IsConnected && batchForRedis.Any())
            {
                _ = Task.Run(async () => 
                {
                    try {
                        var db = _redis.GetDatabase();
                        await db.HashSetAsync("fingerprints:all", batchForRedis.ToArray());
                        _logger.LogInformation("Populated Redis with {Count} templates.", batchForRedis.Count);
                    } catch (Exception ex) {
                        _logger.LogError(ex, "Failed to populate Redis.");
                    }
                }, stoppingToken);
            }

            IsLoaded = true;
            _logger.LogInformation("Loaded {Count} templates from Database (Shard {ShardId}/{Total}).", _templateCache.Count, _shardId, _totalShards);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load fingerprint templates.");
        }
    }

    public async Task SyncToRedis(Guid patientId, FingerprintType fingerType, byte[] fingerprintData)
    {
        if (_redis == null || !_redis.IsConnected) return;

        try
        {
            var db = _redis.GetDatabase();
            var redisValue = $"{fingerType}|{Convert.ToBase64String(fingerprintData)}";
            await db.HashSetAsync("fingerprints:all", patientId.ToString(), redisValue);
            
            // Also update local cache if it belongs to this shard
            if (Math.Abs(patientId.GetHashCode()) % _totalShards == _shardId)
            {
                _templateCache[patientId] = fingerprintData;
                _fingerTypeCache[patientId] = fingerType.ToString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync fingerprint to Redis.");
        }
    }

    /// <summary>
    /// Performs a 1:N identification search against the in-memory cache.
    /// Uses Parallel.ForEach for performance.
    /// </summary>
    public (bool Match, Guid? PatientId, int Score) Identify(byte[] featureSetData)
    {
        if (!IsLoaded)
        {
            _logger.LogWarning("Matcher not yet loaded.");
            return (false, null, 0);
        }

        // Deserialize features once
        var features = new DPFP.FeatureSet();
        try
        {
            features.DeSerialize(featureSetData);
        }
        catch (Exception ex)
        {
             _logger.LogError("Invalid feature set data: {Message}", ex.Message);
             return (false, null, 0);
        }

        var matchResult = new DPFP.Verification.Verification.Result();
        Guid? matchedPatient = null;
        int bestScore = 0;
        object lockObj = new object();

        // Parallel Search
        Parallel.ForEach(_templateCache, (item, state) =>
        {
            // Performance optimization: Stop if we found a high confidence match
            if (matchedPatient != null) state.Stop();

            var template = new DPFP.Template();
            try
            {
                template.DeSerialize(item.Value);
                
                // Thread-local verification result
                var localResult = new DPFP.Verification.Verification.Result();
                _verificator.Verify(features, template, ref localResult);

                if (localResult.Verified)
                {
                    lock (lockObj)
                    {
                        // Keep the best match
                        if (localResult.FARAchieved < bestScore || matchedPatient == null)
                        {
                            bestScore = localResult.FARAchieved; // Lower FAR is better? Wait, usually Score probability
                            // DPFP FARAchieved: Represents the probability that the two fingerprints are NOT the same. 
                            // So LOWER is BETTER match.
                            
                            matchedPatient = item.Key;
                            state.Stop(); // Stop other threads
                        }
                    }
                }
            }
            catch { /* Invalid template in cache, skip */ }
        });

        if (matchedPatient.HasValue)
        {
            return (true, matchedPatient, bestScore);
        }

        return (false, null, 0);
    }
}
