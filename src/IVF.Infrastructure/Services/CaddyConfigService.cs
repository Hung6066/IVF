using System.Text;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

public class CaddyConfigService : ICaddyConfigService
{
    private readonly ITenantRepository _tenantRepository;
    private readonly HttpClient _httpClient;
    private readonly ILogger<CaddyConfigService> _logger;
    private readonly string _adminUrl;
    private readonly string _baseDomain;
    private readonly string _subdomainSuffix;
    private readonly string _monitoringPasswordHash;
    private readonly string _prometheusBasicAuth;

    public CaddyConfigService(
        ITenantRepository tenantRepository,
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<CaddyConfigService> logger)
    {
        _tenantRepository = tenantRepository;
        _httpClient = httpClient;
        _logger = logger;

        var section = configuration.GetSection("CaddyAdmin");
        _adminUrl = section["AdminUrl"] ?? "http://caddy:2019";
        _baseDomain = section["BaseDomain"] ?? "natra.site";
        _subdomainSuffix = section["SubdomainSuffix"] ?? ".natra.site";
        _monitoringPasswordHash = section["MonitoringPasswordHash"]
            ?? "$2b$12$i27omGvrtGdYIh93svxdZO6.BJRMU6a1Y//ZjK7qMqBSV2E/9srIC";
        _prometheusBasicAuth = section["PrometheusBasicAuth"]
            ?? "Basic bW9uaXRvcjp3RERhSTh6elNUQlB5emZHcDN3UmM2SmtER2dJdjZaRg==";
    }

    public async Task<string> GenerateCaddyfileAsync(CancellationToken ct = default)
    {
        var tenants = await _tenantRepository.GetAllTenantsRawAsync(ct);
        var activeTenants = tenants
            .Where(t => t.Status == TenantStatus.Active && !t.IsDeleted && !t.IsRootTenant)
            .ToList();

        var sb = new StringBuilder();

        // Global options
        sb.AppendLine("{");
        sb.AppendLine("\tadmin 0.0.0.0:2019 {");
        sb.AppendLine("\t\torigins 10.0.0.0/8 172.16.0.0/12 192.168.0.0/16 127.0.0.1");
        sb.AppendLine("\t}");
        sb.AppendLine("\tservers {");
        sb.AppendLine("\t\tmetrics");
        sb.AppendLine("\t}");
        sb.AppendLine("}");
        sb.AppendLine();

        // Main site block
        sb.AppendLine(GenerateMainSiteBlock());
        sb.AppendLine();

        // Tenant subdomain blocks
        foreach (var tenant in activeTenants)
        {
            var domains = new List<string>();

            if (!string.IsNullOrWhiteSpace(tenant.Slug))
                domains.Add($"{tenant.Slug}{_subdomainSuffix}");

            if (!string.IsNullOrWhiteSpace(tenant.CustomDomain)
                && tenant.CustomDomainStatus == CustomDomainStatus.Verified)
                domains.Add(tenant.CustomDomain);

            if (domains.Count > 0)
            {
                sb.AppendLine(GenerateTenantBlock(string.Join(", ", domains), tenant.Name));
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    public async Task<CaddySyncResult> SyncConfigAsync(CancellationToken ct = default)
    {
        try
        {
            var caddyfile = await GenerateCaddyfileAsync(ct);

            var content = new StringContent(caddyfile, Encoding.UTF8, "text/caddyfile");
            var response = await _httpClient.PostAsync($"{_adminUrl}/load", content, ct);

            if (response.IsSuccessStatusCode)
            {
                var tenants = await _tenantRepository.GetAllTenantsRawAsync(ct);
                var domainCount = tenants.Count(t =>
                    t.Status == TenantStatus.Active && !t.IsDeleted && !t.IsRootTenant
                    && !string.IsNullOrWhiteSpace(t.Slug));

                _logger.LogInformation("Caddy config synced successfully with {DomainCount} tenant domains", domainCount);
                return new CaddySyncResult(true, $"Đồng bộ thành công {domainCount} domain", domainCount);
            }

            var error = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Caddy config sync failed: {StatusCode} {Error}", response.StatusCode, error);
            return new CaddySyncResult(false, $"Lỗi đồng bộ: {response.StatusCode} - {error}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Cannot connect to Caddy admin API at {AdminUrl}", _adminUrl);
            return new CaddySyncResult(false, $"Không thể kết nối Caddy Admin API: {ex.Message}");
        }
    }

    public async Task<string?> GetCurrentConfigAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_adminUrl}/config/", ct);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadAsStringAsync(ct);

            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Cannot get current Caddy config from {AdminUrl}", _adminUrl);
            return null;
        }
    }

    public async Task<List<TenantDomainInfo>> GetTenantDomainsAsync(CancellationToken ct = default)
    {
        var tenants = await _tenantRepository.GetAllTenantsRawAsync(ct);

        return tenants
            .Where(t => !t.IsDeleted && !t.IsRootTenant)
            .Select(t => new TenantDomainInfo(
                t.Id,
                t.Name,
                t.Slug,
                string.IsNullOrWhiteSpace(t.Slug) ? null : $"{t.Slug}{_subdomainSuffix}",
                t.CustomDomain,
                t.CustomDomainStatus.ToString(),
                t.Status == TenantStatus.Active))
            .OrderBy(t => t.TenantName)
            .ToList();
    }

    private string GenerateMainSiteBlock()
    {
        return $@"{_baseDomain} {{
	# Health checks (no auth required)
	handle /health/* {{
		reverse_proxy api:8080
	}}

	# Grafana — monitoring dashboard (basic auth protected)
	handle /grafana* {{
		basic_auth {{
			monitor {_monitoringPasswordHash}
		}}
		reverse_proxy ivf-grafana:3000
	}}

	# Prometheus — metrics (basic auth protected)
	handle /prometheus* {{
		basic_auth {{
			monitor {_monitoringPasswordHash}
		}}
		reverse_proxy ivf-prometheus:9090 {{
			header_up Authorization ""{_prometheusBasicAuth}""
		}}
	}}

	# Loki — log aggregation (basic auth protected)
	handle_path /loki/* {{
		basic_auth {{
			monitor {_monitoringPasswordHash}
		}}
		reverse_proxy ivf-loki:3100
	}}
	handle /loki {{
		redir /loki/ permanent
	}}

	# API requests
	handle /api/* {{
		reverse_proxy api:8080 {{
			health_uri      /health/live
			health_interval 15s
			health_timeout  5s
			fail_duration   30s
			lb_policy       round_robin
			lb_retries      2
		}}
	}}

	# SignalR hubs
	handle /hubs/* {{
		reverse_proxy api:8080 {{
			health_uri      /health/live
			health_interval 15s
			health_timeout  5s
			fail_duration   30s
			lb_policy       ip_hash
			header_up Connection {{http.request.header.Connection}}
			header_up Upgrade    {{http.request.header.Upgrade}}
		}}
	}}

	# Swagger
	handle /swagger/* {{
		reverse_proxy api:8080 {{
			health_uri      /health/live
			health_interval 15s
			health_timeout  5s
			fail_duration   30s
		}}
	}}

	# Everything else -> Angular frontend
	handle {{
		reverse_proxy frontend:80 {{
			health_uri      /
			health_interval 30s
			health_timeout  5s
			fail_duration   30s
		}}
	}}

	# Security headers
	header {{
		X-Content-Type-Options nosniff
		X-Frame-Options DENY
		Referrer-Policy strict-origin-when-cross-origin
		Strict-Transport-Security ""max-age=31536000; includeSubDomains; preload""
		-Server
	}}

	encode gzip zstd
}}";
    }

    private static string GenerateTenantBlock(string siteAddress, string tenantName)
    {
        return $@"# Tenant: {tenantName}
{siteAddress} {{
	# Health checks
	handle /health/* {{
		reverse_proxy api:8080
	}}

	# API requests
	handle /api/* {{
		reverse_proxy api:8080 {{
			health_uri      /health/live
			health_interval 15s
			health_timeout  5s
			fail_duration   30s
			lb_policy       round_robin
			lb_retries      2
		}}
	}}

	# SignalR hubs
	handle /hubs/* {{
		reverse_proxy api:8080 {{
			health_uri      /health/live
			health_interval 15s
			health_timeout  5s
			fail_duration   30s
			lb_policy       ip_hash
			header_up Connection {{http.request.header.Connection}}
			header_up Upgrade    {{http.request.header.Upgrade}}
		}}
	}}

	# Everything else -> Angular frontend
	handle {{
		reverse_proxy frontend:80 {{
			health_uri      /
			health_interval 30s
			health_timeout  5s
			fail_duration   30s
		}}
	}}

	# Security headers
	header {{
		X-Content-Type-Options nosniff
		X-Frame-Options DENY
		Referrer-Policy strict-origin-when-cross-origin
		Strict-Transport-Security ""max-age=31536000; includeSubDomains; preload""
		-Server
	}}

	encode gzip zstd
}}";
    }
}
