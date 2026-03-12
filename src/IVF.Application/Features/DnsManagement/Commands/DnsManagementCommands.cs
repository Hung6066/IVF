using FluentValidation;
using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IVF.Application.Features.DnsManagement.Commands;

// ═══════════════════════════════════════════════════════════════════════════════════════
// CREATE DNS RECORD COMMAND
// ═══════════════════════════════════════════════════════════════════════════════════════

public record CreateDnsRecordCommand(
    DnsRecordType RecordType,
    string Name,
    string Content,
    int TtlSeconds
) : IRequest<Result<DnsRecordDto>>;

public class CreateDnsRecordValidator : AbstractValidator<CreateDnsRecordCommand>
{
    public CreateDnsRecordValidator()
    {
        RuleFor(x => x.RecordType).IsInEnum();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Content).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.TtlSeconds)
            .GreaterThanOrEqualTo(300).WithMessage("TTL phải tối thiểu 300 giây")
            .LessThanOrEqualTo(86400).WithMessage("TTL phải tối đa 86400 giây");
    }
}

public class CreateDnsRecordHandler : IRequestHandler<CreateDnsRecordCommand, Result<DnsRecordDto>>
{
    private readonly IDnsRecordRepository _dnsRepo;
    private readonly IDnsProvider _dnsProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateDnsRecordHandler> _logger;

    public CreateDnsRecordHandler(
        IDnsRecordRepository dnsRepo,
        IDnsProvider dnsProvider,
        IUnitOfWork unitOfWork,
        ILogger<CreateDnsRecordHandler> logger)
    {
        _dnsRepo = dnsRepo;
        _dnsProvider = dnsProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<DnsRecordDto>> Handle(CreateDnsRecordCommand request, CancellationToken ct)
    {
        try
        {
            // Create record with Cloudflare
            var providerRecord = await _dnsProvider.CreateRecordAsync(
                request.RecordType.ToString(),
                request.Name,
                request.Content,
                request.TtlSeconds,
                ct);

            // Create local entity with empty TenantId (DbContext will set it automatically during SaveChanges)
            var dnsRecord = DnsRecord.Create(
                Guid.Empty,  // Will be set by DbContext.SaveChangesAsync
                request.RecordType,
                request.Name,
                request.Content,
                request.TtlSeconds);

            dnsRecord.SetCloudflareId(providerRecord.Id);

            await _dnsRepo.AddAsync(dnsRecord, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            _logger.LogInformation("DNS record created: {Name} ({Type})", request.Name, request.RecordType);

            return Result<DnsRecordDto>.Success(MapToDto(dnsRecord));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create DNS record: {Name}", request.Name);
            return Result<DnsRecordDto>.Failure($"Không thể tạo DNS record: {ex.Message}");
        }
    }

    private DnsRecordDto MapToDto(DnsRecord record) => new(
        record.Id,
        record.RecordType.ToString(),
        record.Name,
        record.Content,
        record.TtlSeconds,
        record.IsActive,
        record.CreatedAt);
}

// ═══════════════════════════════════════════════════════════════════════════════════════
// DELETE DNS RECORD COMMAND
// ═══════════════════════════════════════════════════════════════════════════════════════

public record DeleteDnsRecordCommand(Guid RecordId) : IRequest<Result>;

public class DeleteDnsRecordValidator : AbstractValidator<DeleteDnsRecordCommand>
{
    public DeleteDnsRecordValidator()
    {
        RuleFor(x => x.RecordId).NotEmpty();
    }
}

public class DeleteDnsRecordHandler : IRequestHandler<DeleteDnsRecordCommand, Result>
{
    private readonly IDnsRecordRepository _dnsRepo;
    private readonly IDnsProvider _dnsProvider;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteDnsRecordHandler> _logger;

    public DeleteDnsRecordHandler(
        IDnsRecordRepository dnsRepo,
        IDnsProvider dnsProvider,
        IUnitOfWork unitOfWork,
        ILogger<DeleteDnsRecordHandler> logger)
    {
        _dnsRepo = dnsRepo;
        _dnsProvider = dnsProvider;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(DeleteDnsRecordCommand request, CancellationToken ct)
    {
        try
        {
            var record = await _dnsRepo.GetByIdAsync(request.RecordId, ct);
            if (record == null)
                return Result.Failure("DNS record không tìm thấy");

            // Delete from Cloudflare
            if (!string.IsNullOrEmpty(record.CloudflareId))
            {
                await _dnsProvider.DeleteRecordAsync(record.CloudflareId, ct);
            }

            // Soft delete locally
            record.MarkAsDeleted();
            await _unitOfWork.SaveChangesAsync(ct);

            _logger.LogInformation("DNS record deleted: {Name}", record.Name);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete DNS record: {RecordId}", request.RecordId);
            return Result.Failure($"Không thể xóa DNS record: {ex.Message}");
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════════════════
// DTO
// ═══════════════════════════════════════════════════════════════════════════════════════

public record DnsRecordDto(
    Guid Id,
    string RecordType,
    string Name,
    string Content,
    int TtlSeconds,
    bool IsActive,
    DateTime CreatedAt);
