using IVF.Application.Common;
using IVF.Application.Common.Attributes;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Constants;
using IVF.Domain.Entities;
using FluentValidation;
using MediatR;

namespace IVF.Application.Features.InventoryRequests.Commands;

// ==================== DTO ====================
public class InventoryRequestDto
{
    public Guid Id { get; set; }
    public string RequestType { get; set; } = string.Empty;
    public Guid RequestedByUserId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? RejectionReason { get; set; }

    public static InventoryRequestDto FromEntity(InventoryRequest r) => new()
    {
        Id = r.Id,
        RequestType = r.RequestType.ToString(),
        RequestedByUserId = r.RequestedByUserId,
        ApprovedByUserId = r.ApprovedByUserId,
        ItemName = r.ItemName,
        Quantity = r.Quantity,
        Unit = r.Unit,
        Reason = r.Reason,
        Notes = r.Notes,
        Status = r.Status.ToString(),
        RequestedAt = r.RequestedAt,
        ProcessedAt = r.ProcessedAt,
        RejectionReason = r.RejectionReason
    };
}

// ==================== CREATE REQUEST ====================
[RequiresFeature(FeatureCodes.Pharmacy)]
public record CreateInventoryRequestCommand(
    InventoryRequestType RequestType,
    Guid RequestedByUserId,
    string ItemName,
    int Quantity,
    string Unit,
    string? Reason,
    string? Notes) : IRequest<Result<InventoryRequestDto>>;

public class CreateInventoryRequestValidator : AbstractValidator<CreateInventoryRequestCommand>
{
    public CreateInventoryRequestValidator()
    {
        RuleFor(x => x.RequestedByUserId).NotEmpty();
        RuleFor(x => x.ItemName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(20);
    }
}

public class CreateInventoryRequestHandler : IRequestHandler<CreateInventoryRequestCommand, Result<InventoryRequestDto>>
{
    private readonly IInventoryRequestRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public CreateInventoryRequestHandler(IInventoryRequestRepository repo, ICurrentUserService currentUser, IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<InventoryRequestDto>> Handle(CreateInventoryRequestCommand req, CancellationToken ct)
    {
        var request = InventoryRequest.Create(_currentUser.TenantId ?? Guid.Empty, req.RequestType, req.RequestedByUserId,
            req.ItemName, req.Quantity, req.Unit, req.Reason, req.Notes);

        await _repo.AddAsync(request, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<InventoryRequestDto>.Success(InventoryRequestDto.FromEntity(request));
    }
}

// ==================== APPROVE ====================
[RequiresFeature(FeatureCodes.Pharmacy)]
public record ApproveInventoryRequestCommand(Guid Id, Guid ApprovedByUserId) : IRequest<Result<InventoryRequestDto>>;

public class ApproveInventoryRequestHandler : IRequestHandler<ApproveInventoryRequestCommand, Result<InventoryRequestDto>>
{
    private readonly IInventoryRequestRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public ApproveInventoryRequestHandler(IInventoryRequestRepository repo, ICurrentUserService currentUser, IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<InventoryRequestDto>> Handle(ApproveInventoryRequestCommand req, CancellationToken ct)
    {
        var request = await _repo.GetByIdAsync(req.Id, _currentUser.TenantId ?? Guid.Empty, ct);
        if (request is null) return Result<InventoryRequestDto>.Failure("Không tìm thấy yêu cầu.");
        if (request.Status != InventoryRequestStatus.Pending) return Result<InventoryRequestDto>.Failure("Yêu cầu không ở trạng thái chờ duyệt.");

        request.Approve(req.ApprovedByUserId);
        await _repo.UpdateAsync(request, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<InventoryRequestDto>.Success(InventoryRequestDto.FromEntity(request));
    }
}

// ==================== REJECT ====================
[RequiresFeature(FeatureCodes.Pharmacy)]
public record RejectInventoryRequestCommand(Guid Id, Guid RejectedByUserId, string Reason) : IRequest<Result<InventoryRequestDto>>;

public class RejectInventoryRequestHandler : IRequestHandler<RejectInventoryRequestCommand, Result<InventoryRequestDto>>
{
    private readonly IInventoryRequestRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public RejectInventoryRequestHandler(IInventoryRequestRepository repo, ICurrentUserService currentUser, IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<InventoryRequestDto>> Handle(RejectInventoryRequestCommand req, CancellationToken ct)
    {
        var request = await _repo.GetByIdAsync(req.Id, _currentUser.TenantId ?? Guid.Empty, ct);
        if (request is null) return Result<InventoryRequestDto>.Failure("Không tìm thấy yêu cầu.");
        if (request.Status != InventoryRequestStatus.Pending) return Result<InventoryRequestDto>.Failure("Yêu cầu không ở trạng thái chờ duyệt.");

        request.Reject(req.RejectedByUserId, req.Reason);
        await _repo.UpdateAsync(request, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<InventoryRequestDto>.Success(InventoryRequestDto.FromEntity(request));
    }
}

// ==================== FULFILL ====================
[RequiresFeature(FeatureCodes.Pharmacy)]
public record FulfillInventoryRequestCommand(Guid Id) : IRequest<Result<InventoryRequestDto>>;

public class FulfillInventoryRequestHandler : IRequestHandler<FulfillInventoryRequestCommand, Result<InventoryRequestDto>>
{
    private readonly IInventoryRequestRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public FulfillInventoryRequestHandler(IInventoryRequestRepository repo, ICurrentUserService currentUser, IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<InventoryRequestDto>> Handle(FulfillInventoryRequestCommand req, CancellationToken ct)
    {
        var request = await _repo.GetByIdAsync(req.Id, _currentUser.TenantId ?? Guid.Empty, ct);
        if (request is null) return Result<InventoryRequestDto>.Failure("Không tìm thấy yêu cầu.");
        if (request.Status != InventoryRequestStatus.Approved) return Result<InventoryRequestDto>.Failure("Yêu cầu chưa được duyệt.");

        request.Fulfill();
        await _repo.UpdateAsync(request, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<InventoryRequestDto>.Success(InventoryRequestDto.FromEntity(request));
    }
}
