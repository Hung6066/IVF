using IVF.Application.Common;
using IVF.Application.Common.Attributes;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Constants;
using IVF.Domain.Entities;
using FluentValidation;
using MediatR;
using Entities = IVF.Domain.Entities;

namespace IVF.Application.Features.FileTracking.Commands;

// ==================== DTOs ====================
public class FileTransferDto
{
    public Guid Id { get; set; }
    public string FromLocation { get; set; } = string.Empty;
    public string ToLocation { get; set; } = string.Empty;
    public Guid TransferredByUserId { get; set; }
    public string? Reason { get; set; }
    public DateTime TransferredAt { get; set; }

    public static FileTransferDto FromEntity(FileTransfer t) => new()
    {
        Id = t.Id,
        FromLocation = t.FromLocation,
        ToLocation = t.ToLocation,
        TransferredByUserId = t.TransferredByUserId,
        Reason = t.Reason,
        TransferredAt = t.TransferredAt
    };
}

public class FileTrackingDto
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string FileCode { get; set; } = string.Empty;
    public string CurrentLocation { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<FileTransferDto> Transfers { get; set; } = new();

    public static FileTrackingDto FromEntity(Entities.FileTracking f) => new()
    {
        Id = f.Id,
        PatientId = f.PatientId,
        PatientName = f.Patient?.FullName ?? string.Empty,
        FileCode = f.FileCode,
        CurrentLocation = f.CurrentLocation,
        Status = f.Status.ToString(),
        Notes = f.Notes,
        CreatedAt = f.CreatedAt,
        Transfers = f.Transfers.Select(FileTransferDto.FromEntity).ToList()
    };
}

// ==================== CREATE FILE ====================
[RequiresFeature(FeatureCodes.PatientManagement)]
public record CreateFileTrackingCommand(
    Guid PatientId,
    string FileCode,
    string CurrentLocation,
    string? Notes) : IRequest<Result<FileTrackingDto>>;

public class CreateFileTrackingValidator : AbstractValidator<CreateFileTrackingCommand>
{
    public CreateFileTrackingValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.FileCode).NotEmpty().MaximumLength(50);
        RuleFor(x => x.CurrentLocation).NotEmpty().MaximumLength(200);
    }
}

public class CreateFileTrackingHandler : IRequestHandler<CreateFileTrackingCommand, Result<FileTrackingDto>>
{
    private readonly IFileTrackingRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public CreateFileTrackingHandler(IFileTrackingRepository repo, ICurrentUserService currentUser, IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<FileTrackingDto>> Handle(CreateFileTrackingCommand req, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId ?? Guid.Empty;
        if (await _repo.FileCodeExistsAsync(req.FileCode, tenantId, ct))
            return Result<FileTrackingDto>.Failure($"Mã hồ sơ '{req.FileCode}' đã tồn tại.");

        var file = Entities.FileTracking.Create(tenantId, req.PatientId, req.FileCode, req.CurrentLocation, req.Notes);
        await _repo.AddAsync(file, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<FileTrackingDto>.Success(FileTrackingDto.FromEntity(file));
    }
}

// ==================== TRANSFER FILE ====================
[RequiresFeature(FeatureCodes.PatientManagement)]
public record TransferFileCommand(
    Guid Id,
    string ToLocation,
    Guid TransferredByUserId,
    string? Reason) : IRequest<Result<FileTrackingDto>>;

public class TransferFileValidator : AbstractValidator<TransferFileCommand>
{
    public TransferFileValidator()
    {
        RuleFor(x => x.ToLocation).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TransferredByUserId).NotEmpty();
    }
}

public class TransferFileHandler : IRequestHandler<TransferFileCommand, Result<FileTrackingDto>>
{
    private readonly IFileTrackingRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public TransferFileHandler(IFileTrackingRepository repo, ICurrentUserService currentUser, IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<FileTrackingDto>> Handle(TransferFileCommand req, CancellationToken ct)
    {
        var file = await _repo.GetByIdAsync(req.Id, _currentUser.TenantId ?? Guid.Empty, ct);
        if (file is null) return Result<FileTrackingDto>.Failure("Không tìm thấy hồ sơ.");

        file.Transfer(req.ToLocation, req.TransferredByUserId, req.Reason);
        await _repo.UpdateAsync(file, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<FileTrackingDto>.Success(FileTrackingDto.FromEntity(file));
    }
}

// ==================== MARK RECEIVED ====================
[RequiresFeature(FeatureCodes.PatientManagement)]
public record MarkFileReceivedCommand(Guid Id) : IRequest<Result<FileTrackingDto>>;

public class MarkFileReceivedHandler : IRequestHandler<MarkFileReceivedCommand, Result<FileTrackingDto>>
{
    private readonly IFileTrackingRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public MarkFileReceivedHandler(IFileTrackingRepository repo, ICurrentUserService currentUser, IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<FileTrackingDto>> Handle(MarkFileReceivedCommand req, CancellationToken ct)
    {
        var file = await _repo.GetByIdAsync(req.Id, _currentUser.TenantId ?? Guid.Empty, ct);
        if (file is null) return Result<FileTrackingDto>.Failure("Không tìm thấy hồ sơ.");

        file.MarkReceived();
        await _repo.UpdateAsync(file, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<FileTrackingDto>.Success(FileTrackingDto.FromEntity(file));
    }
}

// ==================== MARK LOST ====================
[RequiresFeature(FeatureCodes.PatientManagement)]
public record MarkFileLostCommand(Guid Id, string? Reason) : IRequest<Result<FileTrackingDto>>;

public class MarkFileLostHandler : IRequestHandler<MarkFileLostCommand, Result<FileTrackingDto>>
{
    private readonly IFileTrackingRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public MarkFileLostHandler(IFileTrackingRepository repo, ICurrentUserService currentUser, IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<FileTrackingDto>> Handle(MarkFileLostCommand req, CancellationToken ct)
    {
        var file = await _repo.GetByIdAsync(req.Id, _currentUser.TenantId ?? Guid.Empty, ct);
        if (file is null) return Result<FileTrackingDto>.Failure("Không tìm thấy hồ sơ.");

        file.MarkLost(req.Reason);
        await _repo.UpdateAsync(file, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<FileTrackingDto>.Success(FileTrackingDto.FromEntity(file));
    }
}
