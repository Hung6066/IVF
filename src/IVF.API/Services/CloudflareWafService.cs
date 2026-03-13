using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IVF.API.Services;

/// <summary>
/// Service for monitoring and managing Cloudflare WAF rulesets via API v4.
/// Provides status checks, rule enumeration, and security event publishing
/// for the IVF system's edge WAF protection layer.
/// </summary>
public sealed class CloudflareWafService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<CloudflareWafService> logger)
{
    private const string BaseUrl = "https://api.cloudflare.com/client/v4";
    private readonly string _apiToken = configuration["Cloudflare:ApiToken"] ?? "";
    private readonly string _zoneId = configuration["Cloudflare:ZoneId"] ?? "";

    public bool IsConfigured => !string.IsNullOrEmpty(_apiToken) && !string.IsNullOrEmpty(_zoneId);

    /// <summary>Returns comprehensive WAF status including managed rulesets, custom rules, and rate limiting.</summary>
    public async Task<WafStatus> GetStatusAsync(CancellationToken ct = default)
    {
        if (!IsConfigured)
            return new WafStatus { Configured = false };

        var status = new WafStatus { Configured = true };

        var tasks = new[]
        {
            GetPhaseRulesAsync("http_request_firewall_managed", ct),
            GetPhaseRulesAsync("http_request_firewall_custom", ct),
            GetPhaseRulesAsync("http_ratelimit", ct)
        };

        var results = await Task.WhenAll(tasks);
        status.ManagedRules = results[0];
        status.CustomRules = results[1];
        status.RateLimitRules = results[2];

        // Get security level
        status.SecurityLevel = await GetSettingAsync("security_level", ct);
        status.MinTlsVersion = await GetSettingAsync("min_tls_version", ct);

        return status;
    }

    /// <summary>Returns recent WAF events/firewall events from the last hour.</summary>
    public async Task<List<WafEvent>> GetRecentEventsAsync(int limit = 50, CancellationToken ct = default)
    {
        if (!IsConfigured) return [];

        try
        {
            // GraphQL Analytics API for firewall events
            var client = CreateClient();
            var query = new
            {
                query = """
                    query {
                      viewer {
                        zones(filter: { zoneTag: $zone }) {
                          firewallEventsAdaptive(
                            filter: { datetime_gt: $since }
                            limit: $limit
                            orderBy: [datetime_DESC]
                          ) {
                            action
                            clientIP
                            clientRequestPath
                            clientRequestMethod
                            ruleId
                            source
                            datetime
                            userAgent
                            clientCountryName
                          }
                        }
                      }
                    }
                    """.Replace("$zone", $"\"{_zoneId}\"")
                     .Replace("$since", $"\"{DateTime.UtcNow.AddHours(-1):O}\"")
                     .Replace("$limit", limit.ToString())
            };

            var response = await client.PostAsJsonAsync(
                "https://api.cloudflare.com/client/v4/graphql", query, ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to fetch WAF events: {Status}", response.StatusCode);
                return [];
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var events = new List<WafEvent>();

            if (json.TryGetProperty("data", out var data) &&
                data.TryGetProperty("viewer", out var viewer) &&
                viewer.TryGetProperty("zones", out var zones) &&
                zones.GetArrayLength() > 0)
            {
                var fwEvents = zones[0].GetProperty("firewallEventsAdaptive");
                foreach (var evt in fwEvents.EnumerateArray())
                {
                    events.Add(new WafEvent
                    {
                        Action = evt.GetProperty("action").GetString() ?? "",
                        ClientIp = evt.GetProperty("clientIP").GetString() ?? "",
                        Path = evt.GetProperty("clientRequestPath").GetString() ?? "",
                        Method = evt.GetProperty("clientRequestMethod").GetString() ?? "",
                        RuleId = evt.GetProperty("ruleId").GetString() ?? "",
                        Source = evt.GetProperty("source").GetString() ?? "",
                        Timestamp = evt.GetProperty("datetime").GetDateTime(),
                        UserAgent = evt.GetProperty("userAgent").GetString() ?? "",
                        Country = evt.GetProperty("clientCountryName").GetString() ?? ""
                    });
                }
            }

            return events;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error fetching WAF events");
            return [];
        }
    }

    private async Task<List<WafRule>> GetPhaseRulesAsync(string phase, CancellationToken ct)
    {
        try
        {
            var client = CreateClient();
            var response = await client.GetAsync(
                $"{BaseUrl}/zones/{_zoneId}/rulesets/phases/{phase}/entrypoint", ct);

            if (!response.IsSuccessStatusCode) return [];

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var rules = new List<WafRule>();

            if (json.TryGetProperty("result", out var result) &&
                result.TryGetProperty("rules", out var rulesArray))
            {
                foreach (var rule in rulesArray.EnumerateArray())
                {
                    rules.Add(new WafRule
                    {
                        Id = rule.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                        Description = rule.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
                        Action = rule.TryGetProperty("action", out var action) ? action.GetString() ?? "" : "",
                        Enabled = !rule.TryGetProperty("enabled", out var enabled) || enabled.GetBoolean(),
                        Expression = rule.TryGetProperty("expression", out var expr) ? expr.GetString() ?? "" : ""
                    });
                }
            }

            return rules;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get {Phase} rules", phase);
            return [];
        }
    }

    private async Task<string> GetSettingAsync(string setting, CancellationToken ct)
    {
        try
        {
            var client = CreateClient();
            var response = await client.GetFromJsonAsync<JsonElement>(
                $"{BaseUrl}/zones/{_zoneId}/settings/{setting}", ct);

            if (response.TryGetProperty("result", out var result) &&
                result.TryGetProperty("value", out var value))
                return value.GetString() ?? "unknown";
        }
        catch { /* non-critical */ }
        return "unknown";
    }

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient("cloudflare-waf");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {_apiToken}");
        return client;
    }

    // ─── DTOs ───

    public sealed class WafStatus
    {
        public bool Configured { get; set; }
        public List<WafRule> ManagedRules { get; set; } = [];
        public List<WafRule> CustomRules { get; set; } = [];
        public List<WafRule> RateLimitRules { get; set; } = [];
        public string SecurityLevel { get; set; } = "unknown";
        public string MinTlsVersion { get; set; } = "unknown";

        [JsonIgnore]
        public int TotalRuleCount =>
            ManagedRules.Count + CustomRules.Count + RateLimitRules.Count;
    }

    public sealed class WafRule
    {
        public string Id { get; set; } = "";
        public string Description { get; set; } = "";
        public string Action { get; set; } = "";
        public bool Enabled { get; set; }
        public string Expression { get; set; } = "";
    }

    public sealed class WafEvent
    {
        public string Action { get; set; } = "";
        public string ClientIp { get; set; } = "";
        public string Path { get; set; } = "";
        public string Method { get; set; } = "";
        public string RuleId { get; set; } = "";
        public string Source { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public string UserAgent { get; set; } = "";
        public string Country { get; set; } = "";
    }
}
