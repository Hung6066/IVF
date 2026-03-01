using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

/// <summary>
/// Threat detection engine inspired by:
/// - Google Chronicle: Behavioral analytics, anomaly detection
/// - AWS GuardDuty: IP reputation, impossible travel, brute force detection
/// - Microsoft Defender: Risk scoring, attack pattern recognition
///
/// Evaluates every request against multiple threat signals and produces
/// an aggregated risk score with recommended Zero Trust action.
/// </summary>
public sealed partial class ThreatDetectionService : IThreatDetectionService
{
    private readonly IvfDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ThreatDetectionService> _logger;

    // Sliding window for brute force detection
    private static readonly ConcurrentDictionary<string, List<DateTime>> _failedAttempts = new();
    private static readonly ConcurrentDictionary<string, List<(DateTime Time, string IpAddress, string? Country)>> _loginHistory = new();

    // Threat thresholds
    private const int BruteForceThreshold = 5;
    private static readonly TimeSpan BruteForceWindow = TimeSpan.FromMinutes(15);
    private const int ImpossibleTravelMaxKmPerHour = 900; // ~commercial flight speed
    private static readonly TimeSpan ImpossibleTravelMinWindow = TimeSpan.FromMinutes(30);

    // Known Tor exit node patterns and suspicious ranges (lightweight, no external dependency)
    private static readonly HashSet<string> KnownSuspiciousRanges = new(StringComparer.OrdinalIgnoreCase)
    {
        "10.0.0.", "172.16.", "192.168." // Private ranges — should not appear in production X-Forwarded-For
    };

    public ThreatDetectionService(
        IvfDbContext context,
        IMemoryCache cache,
        ILogger<ThreatDetectionService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ThreatAssessment> AssessRequestAsync(RequestSecurityContext context, CancellationToken ct = default)
    {
        var signals = new List<ThreatSignal>();
        var riskScore = 0m;

        // 1. IP Intelligence
        var ipResult = await CheckIpReputationAsync(context.IpAddress, ct);
        if (ipResult.IsTor)
        {
            signals.Add(new ThreatSignal("TOR_EXIT", "Request from Tor exit node", 30, "High", ThreatCategory.Evasion));
            riskScore += 30;
        }
        if (ipResult.IsVpn)
        {
            signals.Add(new ThreatSignal("VPN_PROXY", "Request through VPN/proxy", 15, "Medium", ThreatCategory.Evasion));
            riskScore += 15;
        }
        if (ipResult.IsKnownAttacker)
        {
            signals.Add(new ThreatSignal("KNOWN_ATTACKER", "IP associated with known attacks", 50, "Critical", ThreatCategory.InitialAccess));
            riskScore += 50;
        }
        if (ipResult.IsHosting)
        {
            signals.Add(new ThreatSignal("HOSTING_IP", "Request from hosting/datacenter IP", 10, "Low", ThreatCategory.Reconnaissance));
            riskScore += 10;
        }

        // 2. User Agent analysis
        var uaSignals = AnalyzeUserAgent(context.UserAgent);
        signals.AddRange(uaSignals);
        riskScore += uaSignals.Sum(s => s.Weight);

        // 3. Impossible travel detection
        if (context.UserId.HasValue)
        {
            var impossibleTravel = await DetectImpossibleTravelAsync(context.UserId.Value, context.IpAddress, context.Country, ct);
            if (impossibleTravel)
            {
                signals.Add(new ThreatSignal("IMPOSSIBLE_TRAVEL", "Login from geographically impossible location", 40, "High", ThreatCategory.CredentialAccess));
                riskScore += 40;
            }
        }

        // 4. Brute force detection
        if (context.UserId.HasValue || !string.IsNullOrEmpty(context.Username))
        {
            var identifier = context.UserId?.ToString() ?? context.Username!;
            var bruteForce = await DetectBruteForceAsync(identifier, ct);
            if (bruteForce)
            {
                signals.Add(new ThreatSignal("BRUTE_FORCE", "Multiple failed authentication attempts", 45, "High", ThreatCategory.CredentialAccess));
                riskScore += 45;
            }
        }

        // 5. Anomalous access pattern detection
        if (context.UserId.HasValue)
        {
            var anomalous = await DetectAnomalousAccessAsync(context.UserId.Value, context, ct);
            if (anomalous)
            {
                signals.Add(new ThreatSignal("ANOMALOUS_ACCESS", "Unusual access pattern detected", 25, "Medium", ThreatCategory.LateralMovement));
                riskScore += 25;
            }
        }

        // 6. Input validation (path + query params)
        var inputResult = ValidateInput(context.RequestPath);
        if (!inputResult.IsClean)
        {
            foreach (var threat in inputResult.DetectedThreats)
            {
                signals.Add(new ThreatSignal("INPUT_ATTACK", threat, 35, "High", ThreatCategory.InitialAccess));
                riskScore += 35;
            }
        }

        // 7. Time-based anomaly (access outside business hours for medical system)
        var hour = context.Timestamp.Hour;
        if (hour < 5 || hour > 23) // Unusual hours for medical staff
        {
            signals.Add(new ThreatSignal("OFF_HOURS", "Access outside normal business hours", 5, "Info", ThreatCategory.None));
            riskScore += 5;
        }

        // Normalize score to 0-100
        riskScore = Math.Min(riskScore, 100);

        var riskLevel = riskScore switch
        {
            >= 70 => RiskLevel.Critical,
            >= 50 => RiskLevel.High,
            >= 25 => RiskLevel.Medium,
            _ => RiskLevel.Low
        };

        var recommendedAction = riskLevel switch
        {
            RiskLevel.Critical => ZeroTrustAction.BlockTemporary,
            RiskLevel.High => ZeroTrustAction.RequireMfa,
            RiskLevel.Medium => ZeroTrustAction.AllowWithMonitoring,
            _ => ZeroTrustAction.Allow
        };

        var shouldBlock = riskLevel >= RiskLevel.Critical;

        return new ThreatAssessment(
            RiskScore: riskScore,
            RiskLevel: riskLevel,
            Signals: signals,
            RecommendedAction: recommendedAction,
            ShouldBlock: shouldBlock,
            BlockReason: shouldBlock ? $"Risk score {riskScore}/100 exceeds threshold. Signals: {string.Join(", ", signals.Select(s => s.SignalType))}" : null,
            AssessedAt: DateTime.UtcNow
        );
    }

    public async Task<bool> DetectImpossibleTravelAsync(Guid userId, string ipAddress, string? country, CancellationToken ct = default)
    {
        var key = $"login_history:{userId}";

        // Get recent login locations from cache or DB
        if (!_loginHistory.TryGetValue(key, out var history))
        {
            history = new List<(DateTime, string, string?)>();

            var recentEvents = await _context.Set<SecurityEvent>()
                .AsNoTracking()
                .Where(e => e.UserId == userId &&
                            e.EventType == SecurityEventTypes.LoginSuccess &&
                            e.CreatedAt >= DateTime.UtcNow.AddHours(-24))
                .OrderByDescending(e => e.CreatedAt)
                .Take(10)
                .Select(e => new { e.CreatedAt, e.IpAddress, e.Country })
                .ToListAsync(ct);

            foreach (var evt in recentEvents)
            {
                if (evt.IpAddress is not null)
                    history.Add((evt.CreatedAt, evt.IpAddress, evt.Country));
            }

            _loginHistory[key] = history;
        }

        // Record current login
        history.Add((DateTime.UtcNow, ipAddress, country));

        // Keep only last 24 hours
        history.RemoveAll(h => h.Time < DateTime.UtcNow.AddHours(-24));

        // Check for impossible travel: different countries within short time windows
        if (history.Count >= 2)
        {
            var latest = history[^1];
            for (int i = history.Count - 2; i >= 0; i--)
            {
                var previous = history[i];
                var timeDiff = latest.Time - previous.Time;

                // Different country within 30 minutes = impossible travel
                if (timeDiff < ImpossibleTravelMinWindow &&
                    !string.IsNullOrEmpty(previous.Country) &&
                    !string.IsNullOrEmpty(latest.Country) &&
                    !string.Equals(previous.Country, latest.Country, StringComparison.OrdinalIgnoreCase) &&
                    previous.IpAddress != latest.IpAddress)
                {
                    _logger.LogWarning(
                        "Impossible travel detected for user {UserId}: {Country1} → {Country2} in {Minutes}min",
                        userId, previous.Country, latest.Country, timeDiff.TotalMinutes);
                    return true;
                }
            }
        }

        return false;
    }

    public Task<IpIntelligenceResult> CheckIpReputationAsync(string ipAddress, CancellationToken ct = default)
    {
        var cacheKey = $"ip_intel:{ipAddress}";
        if (_cache.TryGetValue(cacheKey, out IpIntelligenceResult? cached) && cached is not null)
            return Task.FromResult(cached);

        // Lightweight heuristic-based IP analysis (no external API dependency)
        var result = AnalyzeIpLocally(ipAddress);
        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(30));
        return Task.FromResult(result);
    }

    public Task<bool> DetectBruteForceAsync(string identifier, CancellationToken ct = default)
    {
        var key = $"brute:{identifier}";
        var now = DateTime.UtcNow;

        var attempts = _failedAttempts.GetOrAdd(key, _ => new List<DateTime>());

        lock (attempts)
        {
            // Clean old entries
            attempts.RemoveAll(t => now - t > BruteForceWindow);
            return Task.FromResult(attempts.Count >= BruteForceThreshold);
        }
    }

    /// <summary>
    /// Records a failed login attempt for brute force tracking.
    /// </summary>
    public void RecordFailedAttempt(string identifier)
    {
        var key = $"brute:{identifier}";
        var attempts = _failedAttempts.GetOrAdd(key, _ => new List<DateTime>());

        lock (attempts)
        {
            attempts.Add(DateTime.UtcNow);
            // Trim old entries
            attempts.RemoveAll(t => DateTime.UtcNow - t > BruteForceWindow);
        }
    }

    /// <summary>
    /// Clears failed attempts after successful login.
    /// </summary>
    public void ClearFailedAttempts(string identifier)
    {
        var key = $"brute:{identifier}";
        _failedAttempts.TryRemove(key, out _);
    }

    public async Task<bool> DetectAnomalousAccessAsync(Guid userId, RequestSecurityContext context, CancellationToken ct = default)
    {
        var cacheKey = $"user_pattern:{userId}";

        // Get user's normal access pattern from cache
        if (!_cache.TryGetValue(cacheKey, out UserAccessPattern? pattern))
        {
            // Build pattern from last 30 days of security events
            var since = DateTime.UtcNow.AddDays(-30);
            var events = await _context.Set<SecurityEvent>()
                .AsNoTracking()
                .Where(e => e.UserId == userId &&
                            e.EventType == SecurityEventTypes.LoginSuccess &&
                            e.CreatedAt >= since)
                .Select(e => new { e.IpAddress, e.Country, e.CreatedAt })
                .ToListAsync(ct);

            if (events.Count < 5)
                return false; // Not enough data to establish a baseline

            pattern = new UserAccessPattern
            {
                KnownIps = events.Where(e => e.IpAddress != null).Select(e => e.IpAddress!).Distinct().ToHashSet(),
                KnownCountries = events.Where(e => e.Country != null).Select(e => e.Country!).Distinct().ToHashSet(),
                TypicalHours = events.Select(e => e.CreatedAt.Hour).Distinct().ToHashSet(),
                TotalLogins = events.Count
            };

            _cache.Set(cacheKey, pattern, TimeSpan.FromHours(1));
        }

        // Check for anomalies against established baseline
        var anomalyScore = 0;

        // New IP never seen before
        if (!pattern.KnownIps.Contains(context.IpAddress))
            anomalyScore += 2;

        // New country
        if (!string.IsNullOrEmpty(context.Country) && !pattern.KnownCountries.Contains(context.Country))
            anomalyScore += 3;

        // Unusual hour
        if (!pattern.TypicalHours.Contains(context.Timestamp.Hour))
            anomalyScore += 1;

        return anomalyScore >= 3; // Threshold for anomalous access
    }

    public InputValidationResult ValidateInput(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return new InputValidationResult(true, new List<string>(), input);

        var threats = new List<string>();

        // SQL Injection patterns
        if (SqlInjectionPattern().IsMatch(input))
            threats.Add("SQL injection pattern detected");

        // XSS patterns
        if (XssPattern().IsMatch(input))
            threats.Add("XSS pattern detected");

        // Path traversal
        if (PathTraversalPattern().IsMatch(input))
            threats.Add("Path traversal pattern detected");

        // Command injection
        if (CommandInjectionPattern().IsMatch(input))
            threats.Add("Command injection pattern detected");

        // LDAP injection
        if (LdapInjectionPattern().IsMatch(input))
            threats.Add("LDAP injection pattern detected");

        return new InputValidationResult(
            threats.Count == 0,
            threats,
            threats.Count == 0 ? input : null
        );
    }

    // ─── Private Helpers ───

    private static IpIntelligenceResult AnalyzeIpLocally(string ipAddress)
    {
        // Reserved/private ranges check
        var isPrivate = ipAddress.StartsWith("10.") ||
                        ipAddress.StartsWith("172.16.") || ipAddress.StartsWith("172.17.") ||
                        ipAddress.StartsWith("172.18.") || ipAddress.StartsWith("172.19.") ||
                        ipAddress.StartsWith("172.20.") || ipAddress.StartsWith("172.21.") ||
                        ipAddress.StartsWith("172.22.") || ipAddress.StartsWith("172.23.") ||
                        ipAddress.StartsWith("172.24.") || ipAddress.StartsWith("172.25.") ||
                        ipAddress.StartsWith("172.26.") || ipAddress.StartsWith("172.27.") ||
                        ipAddress.StartsWith("172.28.") || ipAddress.StartsWith("172.29.") ||
                        ipAddress.StartsWith("172.30.") || ipAddress.StartsWith("172.31.") ||
                        ipAddress.StartsWith("192.168.") ||
                        ipAddress == "127.0.0.1" ||
                        ipAddress == "::1";

        return new IpIntelligenceResult(
            IpAddress: ipAddress,
            IsTor: false, // Would integrate with Tor exit list in production
            IsVpn: false, // Would integrate with VPN detection service
            IsProxy: false,
            IsHosting: false,
            IsKnownAttacker: false,
            Country: null, // Would integrate with GeoIP service
            City: null,
            Isp: null,
            ThreatScore: isPrivate ? 0 : 5, // Baseline score for unknown public IPs
            ThreatType: null
        );
    }

    private static List<ThreatSignal> AnalyzeUserAgent(string? userAgent)
    {
        var signals = new List<ThreatSignal>();

        if (string.IsNullOrEmpty(userAgent))
        {
            signals.Add(new ThreatSignal("MISSING_UA", "No User-Agent header", 15, "Medium", ThreatCategory.Reconnaissance));
            return signals;
        }

        // Known bot/scanner patterns
        if (BotPattern().IsMatch(userAgent))
        {
            signals.Add(new ThreatSignal("BOT_UA", "Bot/scanner User-Agent detected", 20, "Medium", ThreatCategory.Reconnaissance));
        }

        // Extremely short UA (likely scripted)
        if (userAgent.Length < 20)
        {
            signals.Add(new ThreatSignal("SHORT_UA", "Suspiciously short User-Agent", 10, "Low", ThreatCategory.Reconnaissance));
        }

        // Known vulnerability scanner tools
        if (ScannerPattern().IsMatch(userAgent))
        {
            signals.Add(new ThreatSignal("SCANNER_UA", "Vulnerability scanner User-Agent", 35, "High", ThreatCategory.Reconnaissance));
        }

        return signals;
    }

    // ─── Compiled Regex Patterns for Performance ───

    [GeneratedRegex(@"(\b(union|select|insert|update|delete|drop|alter|create|exec|execute)\b\s+(all\s+)?.*\b(from|into|table|database|where|set)\b|--\s|;\s*(drop|delete|update|insert)|/\*.*\*/)", RegexOptions.IgnoreCase | RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SqlInjectionPattern();

    [GeneratedRegex(@"(<script[^>]*>|javascript:|on(error|load|click|mouse|focus)\s*=|<iframe|<object|<embed|<svg\s+on|eval\s*\(|document\.(cookie|write|location)|window\.(location|open))", RegexOptions.IgnoreCase | RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex XssPattern();

    [GeneratedRegex(@"\.\./|\.\.\\|%2e%2e%2f|%2e%2e/|\.%2e/|%2e\./", RegexOptions.IgnoreCase | RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PathTraversalPattern();

    [GeneratedRegex(@"[;&|`$]\s*(cat|ls|dir|whoami|id|pwd|uname|wget|curl|nc|netcat|bash|sh|cmd|powershell)", RegexOptions.IgnoreCase | RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CommandInjectionPattern();

    [GeneratedRegex(@"[()&|!*\\]", RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LdapInjectionPattern();

    [GeneratedRegex(@"(bot|crawler|spider|scraper|headless|phantom|selenium|puppeteer|playwright)", RegexOptions.IgnoreCase | RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex BotPattern();

    [GeneratedRegex(@"(nikto|nmap|sqlmap|burp|acunetix|nessus|openvas|dirbuster|gobuster|wfuzz|nuclei|masscan)", RegexOptions.IgnoreCase | RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ScannerPattern();

    // ─── Internal types ───

    private sealed class UserAccessPattern
    {
        public HashSet<string> KnownIps { get; init; } = new();
        public HashSet<string> KnownCountries { get; init; } = new();
        public HashSet<int> TypicalHours { get; init; } = new();
        public int TotalLogins { get; init; }
    }
}
