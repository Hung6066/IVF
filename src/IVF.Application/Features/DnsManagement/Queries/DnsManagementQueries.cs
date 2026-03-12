using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.DnsManagement.Queries;

// ═══════════════════════════════════════════════════════════════════════════════════════
// GET TENANT DNS RECORDS QUERY
// ═══════════════════════════════════════════════════════════════════════════════════════

public record GetTenantDnsRecordsQuery : IRequest<List<DnsRecordListDto>>;

public class GetTenantDnsRecordsHandler : IRequestHandler<GetTenantDnsRecordsQuery, List<DnsRecordListDto>>
{
    private readonly IDnsRecordRepository _dnsRepo;

    public GetTenantDnsRecordsHandler(IDnsRecordRepository dnsRepo)
    {
        _dnsRepo = dnsRepo;
    }

    public async Task<List<DnsRecordListDto>> Handle(GetTenantDnsRecordsQuery request, CancellationToken ct)
    {
        var records = await _dnsRepo.GetAllAsync(ct);

        return records
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
