using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Enums;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

public class WafService : IWafService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDistributedCache _distributedCache;
    private readonly IMemoryCache _memoryCache;
    private readonly WafEventChannel _eventChannel;
    private readonly ILogger<WafService> _logger;

    private const string CacheKey = "waf:active_rules";
    private const string MemoryCacheKey = "waf:active_rules:memory";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    public WafService(
        IServiceScopeFactory scopeFactory,
        IDistributedCache distributedCache,
        IMemoryCache memoryCache,
        WafEventChannel eventChannel,
        ILogger<WafService> logger)
    {
        _scopeFactory = scopeFactory;
        _distributedCache = distributedCache;
        _memoryCache = memoryCache;
        _eventChannel = eventChannel;
        _logger = logger;
    }

    public async Task<WafEvaluationResult> EvaluateRequestAsync(WafRequestContext context, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var rules = await GetActiveRulesAsync(ct);

            foreach (var rule in rules.OrderBy(r => r.Priority))
            {
                if (MatchesRule(rule, context))
                {
                    // Rate limit check
                    if (rule.Action == WafAction.RateLimit)
                    {
                        var isLimited = await CheckRateLimitAsync(rule, context.ClientIp, ct);
                        if (!isLimited)
                            continue; // Under limit, skip
                    }

                    sw.Stop();
                    return new WafEvaluationResult(
                        IsBlocked: rule.Action == WafAction.Block,
                        IsChallenge: rule.Action == WafAction.Challenge,
                        IsRateLimited: rule.Action == WafAction.RateLimit,
                        IsLogged: rule.Action == WafAction.Log,
                        RuleName: rule.Name,
                        RuleId: rule.Id,
                        Action: rule.Action,
                        BlockMessage: rule.BlockResponseMessage ?? "Request blocked by WAF",
                        ProcessingTimeMs: sw.Elapsed.TotalMilliseconds);
                }
            }

            sw.Stop();
            return new WafEvaluationResult(
                IsBlocked: false,
                IsChallenge: false,
                IsRateLimited: false,
                IsLogged: false,
                RuleName: null,
                RuleId: null,
                Action: WafAction.Log,
                BlockMessage: null,
                ProcessingTimeMs: sw.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WAF evaluation error — failing open");
            sw.Stop();
            return new WafEvaluationResult(false, false, false, false, null, null, WafAction.Log, null, sw.Elapsed.TotalMilliseconds);
        }
    }

    public async Task<List<WafRuleCacheEntry>> GetActiveRulesAsync(CancellationToken ct = default)
    {
        // L1: Memory cache
        if (_memoryCache.TryGetValue(MemoryCacheKey, out List<WafRuleCacheEntry>? cached) && cached is not null)
            return cached;

        // L2: Redis cache
        try
        {
            var json = await _distributedCache.GetStringAsync(CacheKey, ct);
            if (!string.IsNullOrEmpty(json))
            {
                cached = JsonSerializer.Deserialize<List<WafRuleCacheEntry>>(json);
                if (cached is not null)
                {
                    _memoryCache.Set(MemoryCacheKey, cached, CacheTtl);
                    return cached;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache unavailable for WAF rules, falling back to DB");
        }

        // L3: Database
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        var rules = await db.WafRules
            .Where(r => r.IsEnabled)
            .OrderBy(r => r.Priority)
            .Select(r => new WafRuleCacheEntry(
                r.Id, r.Name, r.Priority, r.RuleGroup, r.Action, r.MatchType, r.NegateMatch,
                r.UriPathPatterns, r.QueryStringPatterns, r.HeaderPatterns, r.BodyPatterns,
                r.Methods, r.IpCidrList, r.CountryCodes, r.UserAgentPatterns,
                r.RateLimitRequests, r.RateLimitWindowSeconds, r.BlockResponseMessage))
            .ToListAsync(ct);

        // Populate caches
        _memoryCache.Set(MemoryCacheKey, rules, CacheTtl);
        try
        {
            await _distributedCache.SetStringAsync(CacheKey, JsonSerializer.Serialize(rules),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write WAF rules to Redis cache");
        }

        return rules;
    }

    public async Task InvalidateCacheAsync(CancellationToken ct = default)
    {
        _memoryCache.Remove(MemoryCacheKey);
        try
        {
            await _distributedCache.RemoveAsync(CacheKey, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate Redis WAF cache");
        }
    }

    public void RecordEvent(WafEventData eventData)
    {
        _eventChannel.Writer.TryWrite(eventData);
    }

    private bool MatchesRule(WafRuleCacheEntry rule, WafRequestContext ctx)
    {
        var conditions = new List<bool>();

        if (rule.Methods is { Count: > 0 })
            conditions.Add(rule.Methods.Contains(ctx.RequestMethod, StringComparer.OrdinalIgnoreCase));

        if (rule.UriPathPatterns is { Count: > 0 })
            conditions.Add(MatchesAnyPattern(rule.UriPathPatterns, ctx.RequestPath));

        if (rule.QueryStringPatterns is { Count: > 0 })
            conditions.Add(MatchesAnyPattern(rule.QueryStringPatterns, ctx.QueryString));

        if (rule.BodyPatterns is { Count: > 0 })
            conditions.Add(MatchesAnyPattern(rule.BodyPatterns, ctx.Body));

        if (rule.UserAgentPatterns is { Count: > 0 })
            conditions.Add(MatchesAnyPattern(rule.UserAgentPatterns, ctx.UserAgent));

        if (rule.HeaderPatterns is { Count: > 0 })
            conditions.Add(MatchesAnyHeaders(rule.HeaderPatterns, ctx.Headers));

        if (rule.IpCidrList is { Count: > 0 })
            conditions.Add(MatchesAnyCidr(rule.IpCidrList, ctx.ClientIp));

        if (rule.CountryCodes is { Count: > 0 })
            conditions.Add(rule.CountryCodes.Contains(ctx.Country ?? "", StringComparer.OrdinalIgnoreCase));

        if (conditions.Count == 0)
            return false;

        var result = rule.MatchType == WafMatchType.All
            ? conditions.All(c => c)
            : conditions.Any(c => c);

        return rule.NegateMatch ? !result : result;
    }

    private bool MatchesAnyPattern(List<string> patterns, string? input)
    {
        if (string.IsNullOrEmpty(input)) return false;

        foreach (var pattern in patterns)
        {
            try
            {
                if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase, RegexTimeout))
                    return true;
            }
            catch (RegexMatchTimeoutException)
            {
                _logger.LogWarning("WAF regex timeout for pattern: {Pattern}", pattern);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid WAF regex pattern: {Pattern}", pattern);
            }
        }

        return false;
    }

    private bool MatchesAnyHeaders(List<string> patterns, Dictionary<string, string>? headers)
    {
        if (headers is null || headers.Count == 0) return false;

        var headerString = string.Join(" ", headers.Select(h => $"{h.Key}: {h.Value}"));
        return MatchesAnyPattern(patterns, headerString);
    }

    private static bool MatchesAnyCidr(List<string> cidrList, string clientIp)
    {
        if (!IPAddress.TryParse(clientIp, out var ip)) return false;

        foreach (var cidr in cidrList)
        {
            try
            {
                if (cidr.Contains('/'))
                {
                    var parts = cidr.Split('/');
                    if (IPAddress.TryParse(parts[0], out var network) && int.TryParse(parts[1], out var prefixLength))
                    {
                        if (IsInSubnet(ip, network, prefixLength))
                            return true;
                    }
                }
                else if (IPAddress.TryParse(cidr, out var exactIp))
                {
                    if (ip.Equals(exactIp))
                        return true;
                }
            }
            catch
            {
                // Skip invalid CIDR entries
            }
        }

        return false;
    }

    private static bool IsInSubnet(IPAddress address, IPAddress network, int prefixLength)
    {
        var addressBytes = address.GetAddressBytes();
        var networkBytes = network.GetAddressBytes();

        if (addressBytes.Length != networkBytes.Length) return false;

        var wholeBytes = prefixLength / 8;
        var remainingBits = prefixLength % 8;

        for (var i = 0; i < wholeBytes; i++)
        {
            if (addressBytes[i] != networkBytes[i]) return false;
        }

        if (remainingBits > 0 && wholeBytes < addressBytes.Length)
        {
            var mask = (byte)(0xFF << (8 - remainingBits));
            if ((addressBytes[wholeBytes] & mask) != (networkBytes[wholeBytes] & mask))
                return false;
        }

        return true;
    }

    private async Task<bool> CheckRateLimitAsync(WafRuleCacheEntry rule, string clientIp, CancellationToken ct)
    {
        if (rule.RateLimitRequests is null || rule.RateLimitWindowSeconds is null)
            return false;

        var key = $"waf:rl:{rule.Id}:{clientIp}";

        try
        {
            var countStr = await _distributedCache.GetStringAsync(key, ct);
            var count = string.IsNullOrEmpty(countStr) ? 0 : int.Parse(countStr);

            count++;
            await _distributedCache.SetStringAsync(key, count.ToString(),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(rule.RateLimitWindowSeconds.Value)
                }, ct);

            return count > rule.RateLimitRequests.Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Rate limit check failed for WAF rule {RuleId}, allowing request", rule.Id);
            return false;
        }
    }
}
