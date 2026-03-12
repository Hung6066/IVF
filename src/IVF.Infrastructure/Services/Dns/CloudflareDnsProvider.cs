using IVF.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace IVF.Infrastructure.Services.Dns;

public class CloudflareDnsProvider : IDnsProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiToken;
    private readonly string _zoneId;
    private readonly ILogger<CloudflareDnsProvider> _logger;

    private const string BaseUrl = "https://api.cloudflare.com/client/v4";

    public CloudflareDnsProvider(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<CloudflareDnsProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var cloudflareSection = configuration.GetSection("Cloudflare");
        _apiToken = cloudflareSection["ApiToken"] ?? throw new InvalidOperationException("Cloudflare:ApiToken not configured");
        _zoneId = cloudflareSection["ZoneId"] ?? throw new InvalidOperationException("Cloudflare:ZoneId not configured");

        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiToken}");
    }

    public async Task<DnsProviderRecord> CreateRecordAsync(
        string recordType,
        string name,
        string content,
        int ttl,
        CancellationToken ct = default)
    {
        try
        {
            var payload = new
            {
                type = recordType,
                name = name,
                content = content,
                ttl = ttl,
                proxied = false
            };

            var json = JsonSerializer.Serialize(payload);
            var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/zones/{_zoneId}/dns_records")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            var response = await _httpClient.SendAsync(request, ct);
            var responseContent = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Cloudflare API error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                throw new InvalidOperationException($"Cloudflare API error: {response.StatusCode}");
            }

            var result = JsonSerializer.Deserialize<CloudflareResponse>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (result?.Result == null)
                throw new InvalidOperationException("Invalid response from Cloudflare");

            return new DnsProviderRecord
            {
                Id = result.Result.Id,
                Type = result.Result.Type,
                Name = result.Result.Name,
                Content = result.Result.Content,
                Ttl = result.Result.Ttl,
                Proxied = result.Result.Proxied
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create DNS record in Cloudflare");
            throw;
        }
    }

    public async Task DeleteRecordAsync(string recordId, CancellationToken ct = default)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, $"{BaseUrl}/zones/{_zoneId}/dns_records/{recordId}");
            var response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Cloudflare delete error: {StatusCode} - {Content}", response.StatusCode, content);
                throw new InvalidOperationException($"Cloudflare delete error: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete DNS record from Cloudflare");
            throw;
        }
    }

    public async Task<List<DnsProviderRecord>> ListRecordsAsync(CancellationToken ct = default)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/zones/{_zoneId}/dns_records");
            var response = await _httpClient.SendAsync(request, ct);
            var responseContent = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Cloudflare list error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                throw new InvalidOperationException($"Cloudflare list error: {response.StatusCode}");
            }

            var result = JsonSerializer.Deserialize<CloudflareListResponse>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (result?.Result == null)
                return new List<DnsProviderRecord>();

            return result.Result.Select(r => new DnsProviderRecord
            {
                Id = r.Id,
                Type = r.Type,
                Name = r.Name,
                Content = r.Content,
                Ttl = r.Ttl,
                Proxied = r.Proxied
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list DNS records from Cloudflare");
            throw;
        }
    }

    // ── Internal DTOs for Cloudflare API ──
    private class CloudflareResponse
    {
        public CloudflareDnsRecord? Result { get; set; }
        public bool Success { get; set; }
    }

    private class CloudflareListResponse
    {
        public List<CloudflareDnsRecord> Result { get; set; } = new();
        public bool Success { get; set; }
    }

    private class CloudflareDnsRecord
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public int Ttl { get; set; }
        public bool Proxied { get; set; }
    }
}
