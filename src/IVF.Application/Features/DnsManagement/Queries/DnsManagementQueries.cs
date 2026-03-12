using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IVF.Application.Features.DnsManagement.Queries;

// ═══════════════════════════════════════════════════════════════════════════════════════
// GET TENANT DNS RECORDS QUERY
// ═══════════════════════════════════════════════════════════════════════════════════════

public record GetTenantDnsRecordsQuery : IRequest<List<DnsRecordListDto>>;

public class GetTenantDnsRecordsHandler : IRequestHandler<GetTenantDnsRecordsQuery, List<DnsRecordListDto>>
{
    private readonly IDnsRecordRepository _dnsRepo;
    private readonly IDnsProvider _dnsProvider;
    private readonly ILogger<GetTenantDnsRecordsHandler> _logger;

    public GetTenantDnsRecordsHandler(
        IDnsRecordRepository dnsRepo,
        IDnsProvider dnsProvider,
        ILogger<GetTenantDnsRecordsHandler> logger)
    {
        _dnsRepo = dnsRepo;
        _dnsProvider = dnsProvider;
        _logger = logger;
    }

    public async Task<List<DnsRecordListDto>> Handle(GetTenantDnsRecordsQuery request, CancellationToken ct)
    {
        try
        {
            // Fetch actual records from Cloudflare API
            var cloudflareRecords = await _dnsProvider.ListRecordsAsync(ct);

            _logger.LogInformation("Cloudflare returned {Count} DNS records", cloudflareRecords.Count);

            // Convert to DTO and return Cloudflare's actual records
            return cloudflareRecords
                .OrderByDescending(r => r.Type)
                .Select(r => new DnsRecordListDto(
                    Guid.NewGuid(), // Client-side ID for management (Cloudflare ID stored separately)
                    r.Type,
                    r.Name,
                    r.Content,
                    r.Ttl > 0 ? r.Ttl : 3600, // Use Cloudflare TTL or default to 1 hour
                    true, // Cloudflare records are always "active"
                    DateTime.UtcNow)) // Use current time since Cloudflare doesn't provide creation date easily
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch DNS records from Cloudflare, falling back to local DB");
            // Fallback to local database records if Cloudflare API fails
            var localRecords = await _dnsRepo.GetAllAsync(ct);
            return localRecords
                .Where(r => !r.IsDeleted)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new DnsRecordListDto(
                    r.Id,
                    r.RecordType.ToString(),
                    r.Name,
                    r.Content,
                    r.TtlSeconds,
                    r.IsActive,
                    r.CreatedAt))
                .ToList();
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════════════
// DTO
// ═══════════════════════════════════════════════════════════════════════════════════════

public record DnsRecordListDto(
    Guid Id,
    string RecordType,
    string Name,
    string Content,
    int TtlSeconds,
    bool IsActive,
    DateTime CreatedAt);
