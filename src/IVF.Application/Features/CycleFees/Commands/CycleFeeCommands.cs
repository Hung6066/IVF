using FluentValidation;
using IVF.Application.Common;
using IVF.Application.Common.Attributes;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.CycleFees.Commands;

// ==================== DTOs ====================
public record CycleFeeDto(
    Guid Id, Guid CycleId, Guid PatientId, string FeeType, string Description,
    decimal Amount, decimal PaidAmount, decimal BalanceDue, string Status,
    Guid? InvoiceId, bool IsOneTimePerCycle, DateTime? WaivedAt, string? WaivedReason,
    string? Notes, DateTime CreatedAt)
{
    public static CycleFeeDto FromEntity(CycleFee f) => new(
        f.Id, f.CycleId, f.PatientId, f.FeeType, f.Description,
        f.Amount, f.PaidAmount, f.BalanceDue, f.Status,
        f.InvoiceId, f.IsOneTimePerCycle, f.WaivedAt, f.WaivedReason,
        f.Notes, f.CreatedAt);
}

// ==================== CREATE CYCLE FEE ====================
[RequiresFeature(FeatureCodes.Billing)]
public record CreateCycleFeeCommand(
    Guid CycleId, Guid PatientId, string FeeType, string Description,
    decimal Amount, bool IsOneTimePerCycle, string? Notes
) : IRequest<Result<CycleFeeDto>>;

public class CreateCycleFeeValidator : AbstractValidator<CreateCycleFeeCommand>
{
    public CreateCycleFeeValidator()
    {
        RuleFor(x => x.CycleId).NotEmpty();
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.FeeType).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public class CreateCycleFeeHandler : IRequestHandler<CreateCycleFeeCommand, Result<CycleFeeDto>>
{
    private readonly ICycleFeeRepository _repo;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCycleFeeHandler(ICycleFeeRepository repo, IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CycleFeeDto>> Handle(CreateCycleFeeCommand r, CancellationToken ct)
    {
        if (r.IsOneTimePerCycle)
        {
            var exists = await _repo.HasFeeForTypeAsync(r.CycleId, r.FeeType, ct);
            if (exists) return Result<CycleFeeDto>.Failure($"Fee type '{r.FeeType}' already exists for this cycle (one-time fee)");
        }

        var fee = CycleFee.Create(r.CycleId, r.PatientId, r.FeeType, r.Description, r.Amount, r.IsOneTimePerCycle, r.Notes);
        await _repo.AddAsync(fee, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<CycleFeeDto>.Success(CycleFeeDto.FromEntity(fee));
    }
}

// ==================== WAIVE FEE ====================
[RequiresFeature(FeatureCodes.Billing)]
public record WaiveCycleFeeCommand(Guid FeeId, Guid WaivedByUserId, string Reason) : IRequest<Result<CycleFeeDto>>;

public class WaiveCycleFeeHandler : IRequestHandler<WaiveCycleFeeCommand, Result<CycleFeeDto>>
{
    private readonly ICycleFeeRepository _repo;
    private readonly IUnitOfWork _unitOfWork;

    public WaiveCycleFeeHandler(ICycleFeeRepository repo, IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CycleFeeDto>> Handle(WaiveCycleFeeCommand r, CancellationToken ct)
    {
        var fee = await _repo.GetByIdAsync(r.FeeId, ct);
        if (fee == null) return Result<CycleFeeDto>.Failure("Cycle fee not found");

        fee.Waive(r.WaivedByUserId, r.Reason);
        await _repo.UpdateAsync(fee, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<CycleFeeDto>.Success(CycleFeeDto.FromEntity(fee));
    }
}

// ==================== REFUND FEE ====================
[RequiresFeature(FeatureCodes.Billing)]
public record RefundCycleFeeCommand(Guid FeeId) : IRequest<Result<CycleFeeDto>>;

public class RefundCycleFeeHandler : IRequestHandler<RefundCycleFeeCommand, Result<CycleFeeDto>>
{
    private readonly ICycleFeeRepository _repo;
    private readonly IUnitOfWork _unitOfWork;

    public RefundCycleFeeHandler(ICycleFeeRepository repo, IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CycleFeeDto>> Handle(RefundCycleFeeCommand r, CancellationToken ct)
    {
        var fee = await _repo.GetByIdAsync(r.FeeId, ct);
        if (fee == null) return Result<CycleFeeDto>.Failure("Cycle fee not found");
        if (fee.Status != "Paid") return Result<CycleFeeDto>.Failure("Only paid fees can be refunded");

        fee.Refund();
        await _repo.UpdateAsync(fee, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<CycleFeeDto>.Success(CycleFeeDto.FromEntity(fee));
    }
}
