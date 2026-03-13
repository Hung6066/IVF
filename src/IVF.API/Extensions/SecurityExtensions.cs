using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IVF.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Polly;

namespace IVF.API.Extensions;

/// <summary>
/// Enterprise Security Extensions
/// Provides cached security rule evaluation, rate limiting, and threat protection
/// </summary>
public static class SecurityExtensions
{
    /// <summary>
    /// Adds enterprise security services
    /// </summary>
    public static IServiceCollection AddEnterpriseSecurity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Security rule cache
        services.AddSingleton<ISecurityRuleCache, SecurityRuleCache>();

        // IP intelligence service
        services.AddSingleton<IIpIntelligenceService, IpIntelligenceService>();

        // Device fingerprint validator
        services.AddSingleton<IDeviceFingerprintValidator, DeviceFingerprintValidator>();

        // Session security service
        services.AddSingleton<ISessionSecurityService, SessionSecurityService>();

        // Threat intelligence aggregator
        services.AddSingleton<IThreatIntelligenceAggregator, ThreatIntelligenceAggregator>();

        return services;
    }
}

#region Security Rule Cache

/// <summary>
/// Caches IP whitelist and geo-blocking rules to avoid database queries on every request
/// </summary>
public interface ISecurityRuleCache
{
    Task<bool> IsIpWhitelistedAsync(string ipAddress, CancellationToken ct = default);
    Task<bool> IsCountryBlockedAsync(string countryCode, CancellationToken ct = default);
    Task<bool> HasActiveWhitelistAsync(CancellationToken ct = default);
    Task InvalidateCacheAsync(CancellationToken ct = default);
}

public class SecurityRuleCache : ISecurityRuleCache
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache? _distributedCache;
    private readonly ILogger<SecurityRuleCache> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private const string WhitelistCacheKey = "security:ip_whitelist";
    private const string GeoBlockCacheKey = "security:geo_blocks";
    private const string HasWhitelistKey = "security:has_whitelist";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MemoryCacheDuration = TimeSpan.FromMinutes(1);

    // In-memory bloom filter for fast negative lookups
    private HashSet<string> _whitelistedIps = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<(string BaseIp, int PrefixLength)> _whitelistedCidrs = new();
    private HashSet<string> _blockedCountries = new(StringComparer.OrdinalIgnoreCase);
    private bool? _hasActiveWhitelist;
    private DateTime _lastRefresh = DateTime.MinValue;

    public SecurityRuleCache(
        IServiceScopeFactory scopeFactory,
        IMemoryCache memoryCache,
        IDistributedCache? distributedCache,
        ILogger<SecurityRuleCache> logger)
    {
        _scopeFactory = scopeFactory;
        _memoryCache = memoryCache;
        _distributedCache = distributedCache;
        _logger = logger;
    }

    public async Task<bool> HasActiveWhitelistAsync(CancellationToken ct = default)
    {
        await EnsureCacheLoadedAsync(ct);
        return _hasActiveWhitelist ?? false;
    }

    public async Task<bool> IsIpWhitelistedAsync(string ipAddress, CancellationToken ct = default)
    {
        await EnsureCacheLoadedAsync(ct);

        // Fast exact match
        if (_whitelistedIps.Contains(ipAddress))
            return true;

        // CIDR range check
        if (!IPAddress.TryParse(ipAddress, out var parsedIp))
            return false;

        foreach (var (baseIp, prefixLength) in _whitelistedCidrs)
        {
            if (IsIpInCidr(parsedIp, baseIp, prefixLength))
                return true;
        }

        return false;
    }

    public async Task<bool> IsCountryBlockedAsync(string countryCode, CancellationToken ct = default)
    {
        await EnsureCacheLoadedAsync(ct);
        return _blockedCountries.Contains(countryCode);
    }

    public async Task InvalidateCacheAsync(CancellationToken ct = default)
    {
        await _refreshLock.WaitAsync(ct);
        try
        {
            _lastRefresh = DateTime.MinValue;
            _hasActiveWhitelist = null;
            _whitelistedIps.Clear();
            _whitelistedCidrs.Clear();
            _blockedCountries.Clear();

            _memoryCache.Remove(WhitelistCacheKey);
            _memoryCache.Remove(GeoBlockCacheKey);
            _memoryCache.Remove(HasWhitelistKey);

            if (_distributedCache != null)
            {
                await _distributedCache.RemoveAsync(WhitelistCacheKey, ct);
                await _distributedCache.RemoveAsync(GeoBlockCacheKey, ct);
                await _distributedCache.RemoveAsync(HasWhitelistKey, ct);
            }

            _logger.LogInformation("Security rule cache invalidated");
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task EnsureCacheLoadedAsync(CancellationToken ct)
    {
        // Fast path - cache is fresh
        if (DateTime.UtcNow - _lastRefresh < MemoryCacheDuration)
            return;

        await _refreshLock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            if (DateTime.UtcNow - _lastRefresh < MemoryCacheDuration)
                return;

            await RefreshCacheAsync(ct);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task RefreshCacheAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IVF.Infrastructure.Persistence.IvfDbContext>();

            // Load whitelist entries
            var whitelistEntries = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .ToListAsync(db.IpWhitelistEntries
                    .Where(e => e.IsActive && !e.IsDeleted &&
                           (!e.ExpiresAt.HasValue || e.ExpiresAt > DateTime.UtcNow)), ct);

            var newIps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var newCidrs = new HashSet<(string, int)>();

            foreach (var entry in whitelistEntries)
            {
                if (!string.IsNullOrEmpty(entry.CidrRange) && int.TryParse(entry.CidrRange.TrimStart('/'), out var prefix))
                {
                    newCidrs.Add((entry.IpAddress, prefix));
                }
                else
                {
                    newIps.Add(entry.IpAddress);
                }
            }

            // Load geo-block rules
            var geoBlockRules = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .ToListAsync(db.GeoBlockRules
                    .Where(r => r.IsBlocked && r.IsEnabled && !r.IsDeleted)
                    .Select(r => r.CountryCode), ct);

            var newBlockedCountries = new HashSet<string>(geoBlockRules, StringComparer.OrdinalIgnoreCase);

            // Atomically update cache
            _whitelistedIps = newIps;
            _whitelistedCidrs = newCidrs;
            _blockedCountries = newBlockedCountries;
            _hasActiveWhitelist = whitelistEntries.Count > 0;
            _lastRefresh = DateTime.UtcNow;

            _logger.LogDebug(
                "Security cache refreshed: {IpCount} IPs, {CidrCount} CIDRs, {BlockCount} blocked countries",
                newIps.Count, newCidrs.Count, newBlockedCountries.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh security rule cache");
            // Keep stale cache on failure
        }
    }

    private static bool IsIpInCidr(IPAddress clientIp, string baseIpStr, int prefixLength)
    {
        if (!IPAddress.TryParse(baseIpStr, out var baseIp))
            return false;

        var baseBytes = baseIp.GetAddressBytes();
        var clientBytes = clientIp.GetAddressBytes();

        if (baseBytes.Length != clientBytes.Length)
            return false;

        var totalBits = baseBytes.Length * 8;
        if (prefixLength > totalBits)
            return false;

        for (int i = 0; i < prefixLength; i++)
        {
            var byteIndex = i / 8;
            var bitIndex = 7 - (i % 8);
            var mask = 1 << bitIndex;

            if ((baseBytes[byteIndex] & mask) != (clientBytes[byteIndex] & mask))
                return false;
        }

        return true;
    }
}

#endregion

#region IP Intelligence

/// <summary>
/// Provides IP reputation and intelligence data
/// </summary>
public interface IIpIntelligenceService
{
    Task<IpIntelligence> GetIntelligenceAsync(string ipAddress, CancellationToken ct = default);
}

public record IpIntelligence(
    string IpAddress,
    bool IsKnownVpn,
    bool IsKnownProxy,
    bool IsKnownTor,
    bool IsKnownDatacenter,
    bool IsKnownBotnet,
    int ReputationScore,
    string? Country,
    string? City,
    string? Isp,
    string? Asn,
    DateTime LastUpdated
);

public class IpIntelligenceService : IIpIntelligenceService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<IpIntelligenceService> _logger;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    // Known datacenter/cloud IP ranges (simplified - in production use a proper IP database)
    private static readonly HashSet<string> KnownDatacenterPrefixes = new()
    {
        "35.", "34.", "104.196.", "104.197.",  // Google Cloud
        "52.", "54.", "18.", "3.",              // AWS
        "40.", "20.", "13.",                     // Azure
        "45.33.", "96.126.", "172.104.",        // Linode
        "167.99.", "159.65.", "68.183."         // DigitalOcean
    };

    public IpIntelligenceService(IMemoryCache cache, ILogger<IpIntelligenceService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<IpIntelligence> GetIntelligenceAsync(string ipAddress, CancellationToken ct = default)
    {
        var cacheKey = $"ip_intel:{ipAddress}";

        if (_cache.TryGetValue<IpIntelligence>(cacheKey, out var cached))
            return cached!;

        // Basic heuristics (in production, integrate with MaxMind, IPinfo, etc.)
        var intel = new IpIntelligence(
            IpAddress: ipAddress,
            IsKnownVpn: false,
            IsKnownProxy: false,
            IsKnownTor: await IsTorExitNodeAsync(ipAddress, ct),
            IsKnownDatacenter: IsDatacenterIp(ipAddress),
            IsKnownBotnet: false,
            ReputationScore: 100,
            Country: null,
            City: null,
            Isp: null,
            Asn: null,
            LastUpdated: DateTime.UtcNow
        );

        _cache.Set(cacheKey, intel, CacheDuration);
        return intel;
    }

    private static bool IsDatacenterIp(string ip)
    {
        foreach (var prefix in KnownDatacenterPrefixes)
        {
            if (ip.StartsWith(prefix))
                return true;
        }
        return false;
    }

    private static Task<bool> IsTorExitNodeAsync(string ip, CancellationToken ct)
    {
        // In production, check against Tor exit node list
        return Task.FromResult(false);
    }
}

#endregion

#region Device Fingerprint Validation

/// <summary>
/// Validates and tracks device fingerprints for session binding
/// </summary>
public interface IDeviceFingerprintValidator
{
    Task<DeviceFingerprintResult> ValidateAsync(Guid userId, string fingerprint, string? sessionId, CancellationToken ct = default);
    Task RegisterDeviceAsync(Guid userId, string fingerprint, string sessionId, CancellationToken ct = default);
}

public record DeviceFingerprintResult(
    bool IsValid,
    bool IsNewDevice,
    bool IsSuspicious,
    string? Reason,
    int DeviceAge
);

public class DeviceFingerprintValidator : IDeviceFingerprintValidator
{
    private readonly IDistributedCache? _cache;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<DeviceFingerprintValidator> _logger;

    private static readonly TimeSpan DeviceCacheDuration = TimeSpan.FromDays(30);

    public DeviceFingerprintValidator(
        IDistributedCache? cache,
        IMemoryCache memoryCache,
        ILogger<DeviceFingerprintValidator> logger)
    {
        _cache = cache;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<DeviceFingerprintResult> ValidateAsync(
        Guid userId,
        string fingerprint,
        string? sessionId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(fingerprint))
            return new DeviceFingerprintResult(false, true, true, "Missing fingerprint", 0);

        var deviceKey = GetDeviceKey(userId, fingerprint);
        var userDevicesKey = $"user_devices:{userId}";

        // Check if device is known
        var deviceData = await GetDeviceDataAsync(deviceKey, ct);

        if (deviceData == null)
        {
            // New device
            return new DeviceFingerprintResult(true, true, false, null, 0);
        }

        // Check for session mismatch
        if (!string.IsNullOrEmpty(sessionId) && deviceData.LastSessionId != sessionId)
        {
            // Could be session hijacking attempt
            var timeSinceLastUse = DateTime.UtcNow - deviceData.LastUsed;
            if (timeSinceLastUse < TimeSpan.FromMinutes(5))
            {
                _logger.LogWarning(
                    "Suspicious device activity: User {UserId}, Device {Fingerprint}, Multiple sessions",
                    userId, fingerprint[..8]);
                return new DeviceFingerprintResult(false, false, true, "Concurrent session detected", deviceData.Age);
            }
        }

        var age = (int)(DateTime.UtcNow - deviceData.FirstSeen).TotalDays;
        return new DeviceFingerprintResult(true, false, false, null, age);
    }

    public async Task RegisterDeviceAsync(Guid userId, string fingerprint, string sessionId, CancellationToken ct = default)
    {
        var deviceKey = GetDeviceKey(userId, fingerprint);
        var existing = await GetDeviceDataAsync(deviceKey, ct);

        var deviceData = new DeviceData(
            FirstSeen: existing?.FirstSeen ?? DateTime.UtcNow,
            LastUsed: DateTime.UtcNow,
            LastSessionId: sessionId,
            UsageCount: (existing?.UsageCount ?? 0) + 1,
            Age: existing?.Age ?? 0
        );

        await SetDeviceDataAsync(deviceKey, deviceData, ct);
    }

    private string GetDeviceKey(Guid userId, string fingerprint)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{userId}:{fingerprint}")));
        return $"device:{hash[..16]}";
    }

    private async Task<DeviceData?> GetDeviceDataAsync(string key, CancellationToken ct)
    {
        if (_memoryCache.TryGetValue<DeviceData>(key, out var cached))
            return cached;

        if (_cache != null)
        {
            var data = await _cache.GetStringAsync(key, ct);
            if (!string.IsNullOrEmpty(data))
            {
                var device = JsonSerializer.Deserialize<DeviceData>(data);
                _memoryCache.Set(key, device, TimeSpan.FromMinutes(5));
                return device;
            }
        }

        return null;
    }

    private async Task SetDeviceDataAsync(string key, DeviceData data, CancellationToken ct)
    {
        _memoryCache.Set(key, data, TimeSpan.FromMinutes(5));

        if (_cache != null)
        {
            await _cache.SetStringAsync(key, JsonSerializer.Serialize(data), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = DeviceCacheDuration
            }, ct);
        }
    }

    private record DeviceData(DateTime FirstSeen, DateTime LastUsed, string LastSessionId, int UsageCount, int Age);
}

#endregion

#region Session Security

/// <summary>
/// Enhanced session security with drift detection
/// </summary>
public interface ISessionSecurityService
{
    Task<SessionSecurityResult> ValidateSessionContextAsync(
        string sessionId,
        string ipAddress,
        string? userAgent,
        string? deviceFingerprint,
        CancellationToken ct = default);
    Task RecordSessionContextAsync(
        string sessionId,
        Guid userId,
        string ipAddress,
        string? userAgent,
        string? deviceFingerprint,
        CancellationToken ct = default);
}

public record SessionSecurityResult(
    bool IsValid,
    bool IpChanged,
    bool UserAgentChanged,
    bool DeviceChanged,
    double DriftScore,
    string? ViolationReason
);

public class SessionSecurityService : ISessionSecurityService
{
    private readonly IDistributedCache? _cache;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<SessionSecurityService> _logger;

    private const double MaxAllowedDrift = 0.5;

    public SessionSecurityService(
        IDistributedCache? cache,
        IMemoryCache memoryCache,
        ILogger<SessionSecurityService> logger)
    {
        _cache = cache;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<SessionSecurityResult> ValidateSessionContextAsync(
        string sessionId,
        string ipAddress,
        string? userAgent,
        string? deviceFingerprint,
        CancellationToken ct = default)
    {
        var context = await GetSessionContextAsync(sessionId, ct);
        if (context == null)
        {
            // No previous context - first request in session
            return new SessionSecurityResult(true, false, false, false, 0, null);
        }

        var ipChanged = !string.Equals(context.IpAddress, ipAddress, StringComparison.OrdinalIgnoreCase);
        var uaChanged = !string.Equals(context.UserAgent, userAgent, StringComparison.OrdinalIgnoreCase);
        var deviceChanged = !string.IsNullOrEmpty(deviceFingerprint) &&
                           !string.IsNullOrEmpty(context.DeviceFingerprint) &&
                           !string.Equals(context.DeviceFingerprint, deviceFingerprint);

        // Calculate drift score (0 = no drift, 1 = complete drift)
        double driftScore = 0;
        if (ipChanged) driftScore += 0.4;
        if (uaChanged) driftScore += 0.2;
        if (deviceChanged) driftScore += 0.5;

        // IP change + device change = likely hijacking
        if (ipChanged && deviceChanged)
        {
            _logger.LogWarning(
                "Session drift detected: SessionId={SessionId}, IP {OldIp} -> {NewIp}, Device changed",
                sessionId[..8], context.IpAddress, ipAddress);
            return new SessionSecurityResult(false, ipChanged, uaChanged, deviceChanged, driftScore, "IP and device changed");
        }

        // High drift without device fingerprint match
        if (driftScore > MaxAllowedDrift && deviceChanged)
        {
            return new SessionSecurityResult(false, ipChanged, uaChanged, deviceChanged, driftScore, "Session context drift exceeded threshold");
        }

        return new SessionSecurityResult(true, ipChanged, uaChanged, deviceChanged, driftScore, null);
    }

    public async Task RecordSessionContextAsync(
        string sessionId,
        Guid userId,
        string ipAddress,
        string? userAgent,
        string? deviceFingerprint,
        CancellationToken ct = default)
    {
        var context = new SessionContext(
            SessionId: sessionId,
            UserId: userId,
            IpAddress: ipAddress,
            UserAgent: userAgent,
            DeviceFingerprint: deviceFingerprint,
            CreatedAt: DateTime.UtcNow
        );

        var key = $"session_ctx:{sessionId}";
        var json = JsonSerializer.Serialize(context);

        _memoryCache.Set(key, context, TimeSpan.FromHours(24));

        if (_cache != null)
        {
            await _cache.SetStringAsync(key, json, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
            }, ct);
        }
    }

    private async Task<SessionContext?> GetSessionContextAsync(string sessionId, CancellationToken ct)
    {
        var key = $"session_ctx:{sessionId}";

        if (_memoryCache.TryGetValue<SessionContext>(key, out var cached))
            return cached;

        if (_cache != null)
        {
            var json = await _cache.GetStringAsync(key, ct);
            if (!string.IsNullOrEmpty(json))
            {
                var context = JsonSerializer.Deserialize<SessionContext>(json);
                if (context != null)
                    _memoryCache.Set(key, context, TimeSpan.FromMinutes(5));
                return context;
            }
        }

        return null;
    }

    private record SessionContext(
        string SessionId,
        Guid UserId,
        string IpAddress,
        string? UserAgent,
        string? DeviceFingerprint,
        DateTime CreatedAt
    );
}

#endregion

#region Threat Intelligence Aggregator

/// <summary>
/// Aggregates threat signals from multiple sources
/// </summary>
public interface IThreatIntelligenceAggregator
{
    Task<AggregatedThreatAssessment> AssessAsync(ThreatAssessmentRequest request, CancellationToken ct = default);
}

public record ThreatAssessmentRequest(
    string IpAddress,
    string? UserAgent,
    string? DeviceFingerprint,
    Guid? UserId,
    string RequestPath,
    string RequestMethod
);

public record AggregatedThreatAssessment(
    int RiskScore,
    string RiskLevel,
    bool ShouldBlock,
    bool RequiresStepUp,
    List<ThreatSignal> Signals,
    string? BlockReason
);

public record ThreatSignal(
    string Type,
    string Description,
    int Score,
    string Severity
);

public class ThreatIntelligenceAggregator : IThreatIntelligenceAggregator
{
    private readonly IIpIntelligenceService _ipIntelligence;
    private readonly ISecurityRuleCache _securityRuleCache;
    private readonly ILogger<ThreatIntelligenceAggregator> _logger;

    private const int BlockThreshold = 80;
    private const int StepUpThreshold = 50;

    public ThreatIntelligenceAggregator(
        IIpIntelligenceService ipIntelligence,
        ISecurityRuleCache securityRuleCache,
        ILogger<ThreatIntelligenceAggregator> logger)
    {
        _ipIntelligence = ipIntelligence;
        _securityRuleCache = securityRuleCache;
        _logger = logger;
    }

    public async Task<AggregatedThreatAssessment> AssessAsync(ThreatAssessmentRequest request, CancellationToken ct = default)
    {
        var signals = new List<ThreatSignal>();
        var totalScore = 0;

        // 1. IP Intelligence
        var ipIntel = await _ipIntelligence.GetIntelligenceAsync(request.IpAddress, ct);

        if (ipIntel.IsKnownTor)
        {
            signals.Add(new ThreatSignal("TOR_EXIT", "Request from Tor exit node", 40, "High"));
            totalScore += 40;
        }

        if (ipIntel.IsKnownProxy)
        {
            signals.Add(new ThreatSignal("PROXY", "Request from known proxy", 20, "Medium"));
            totalScore += 20;
        }

        if (ipIntel.IsKnownDatacenter)
        {
            signals.Add(new ThreatSignal("DATACENTER", "Request from datacenter IP", 15, "Low"));
            totalScore += 15;
        }

        if (ipIntel.IsKnownBotnet)
        {
            signals.Add(new ThreatSignal("BOTNET", "Request from known botnet IP", 90, "Critical"));
            totalScore += 90;
        }

        // 2. User Agent Analysis
        if (string.IsNullOrEmpty(request.UserAgent))
        {
            signals.Add(new ThreatSignal("NO_UA", "Missing User-Agent header", 25, "Medium"));
            totalScore += 25;
        }
        else if (IsSuspiciousUserAgent(request.UserAgent))
        {
            signals.Add(new ThreatSignal("SUSPICIOUS_UA", "Suspicious User-Agent pattern", 20, "Medium"));
            totalScore += 20;
        }

        // 3. Device fingerprint
        if (string.IsNullOrEmpty(request.DeviceFingerprint))
        {
            signals.Add(new ThreatSignal("NO_FINGERPRINT", "Missing device fingerprint", 10, "Low"));
            totalScore += 10;
        }

        // 4. Request pattern analysis
        if (IsSensitivePath(request.RequestPath))
        {
            // Sensitive paths have elevated baseline risk
            totalScore += 5;
        }

        // Determine risk level
        var riskLevel = totalScore switch
        {
            >= 80 => "Critical",
            >= 60 => "High",
            >= 40 => "Medium",
            >= 20 => "Low",
            _ => "None"
        };

        var shouldBlock = totalScore >= BlockThreshold;
        var requiresStepUp = totalScore >= StepUpThreshold && !shouldBlock;

        string? blockReason = null;
        if (shouldBlock)
        {
            blockReason = signals.OrderByDescending(s => s.Score).FirstOrDefault()?.Description;
        }

        return new AggregatedThreatAssessment(
            RiskScore: totalScore,
            RiskLevel: riskLevel,
            ShouldBlock: shouldBlock,
            RequiresStepUp: requiresStepUp,
            Signals: signals,
            BlockReason: blockReason
        );
    }

    private static bool IsSuspiciousUserAgent(string userAgent)
    {
        var suspicious = new[] { "curl", "wget", "python-requests", "go-http-client", "java/", "okhttp" };
        var ua = userAgent.ToLowerInvariant();
        return suspicious.Any(s => ua.Contains(s));
    }

    private static bool IsSensitivePath(string path)
    {
        var sensitive = new[] { "/api/users", "/api/keyvault", "/api/backup", "/api/audit", "/api/signing" };
        return sensitive.Any(s => path.StartsWith(s, StringComparison.OrdinalIgnoreCase));
    }
}

#endregion
