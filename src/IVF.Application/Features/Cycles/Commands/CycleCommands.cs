using FluentValidation;
using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using MediatR;

namespace IVF.Application.Features.Cycles.Commands;

// ==================== CREATE CYCLE ====================
public record CreateCycleCommand(
    Guid CoupleId,
    TreatmentMethod Method,
    DateTime StartDate,
    string? Notes
) : IRequest<Result<CycleDto>>;

public class CreateCycleValidator : AbstractValidator<CreateCycleCommand>
{
    public CreateCycleValidator()
    {
        RuleFor(x => x.CoupleId).NotEmpty();
        RuleFor(x => x.StartDate).NotEmpty();
    }
}

public class CreateCycleHandler : IRequestHandler<CreateCycleCommand, Result<CycleDto>>
{
    private readonly ITreatmentCycleRepository _cycleRepo;
    private readonly ICoupleRepository _coupleRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCycleHandler(ITreatmentCycleRepository cycleRepo, ICoupleRepository coupleRepo, IUnitOfWork unitOfWork)
    {
        _cycleRepo = cycleRepo;
        _coupleRepo = coupleRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CycleDto>> Handle(CreateCycleCommand request, CancellationToken ct)
    {
        var couple = await _coupleRepo.GetByIdAsync(request.CoupleId, ct);
        if (couple == null)
            return Result<CycleDto>.Failure("Couple not found");

        var code = await _cycleRepo.GenerateCodeAsync(ct);
        var cycle = TreatmentCycle.Create(request.CoupleId, code, request.Method, request.StartDate, request.Notes);
        
        await _cycleRepo.AddAsync(cycle, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<CycleDto>.Success(CycleDto.FromEntity(cycle));
    }
}

// ==================== ADVANCE PHASE ====================
public record AdvancePhaseCommand(Guid CycleId, CyclePhase NewPhase) : IRequest<Result<CycleDto>>;

public class AdvancePhaseHandler : IRequestHandler<AdvancePhaseCommand, Result<CycleDto>>
{
    private readonly ITreatmentCycleRepository _cycleRepo;
    private readonly IUnitOfWork _unitOfWork;

    public AdvancePhaseHandler(ITreatmentCycleRepository cycleRepo, IUnitOfWork unitOfWork)
    {
        _cycleRepo = cycleRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CycleDto>> Handle(AdvancePhaseCommand request, CancellationToken ct)
    {
        var cycle = await _cycleRepo.GetByIdAsync(request.CycleId, ct);
        if (cycle == null)
            return Result<CycleDto>.Failure("Cycle not found");

        cycle.AdvancePhase(request.NewPhase);
        await _cycleRepo.UpdateAsync(cycle, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<CycleDto>.Success(CycleDto.FromEntity(cycle));
    }
}

// ==================== COMPLETE CYCLE ====================
public record CompleteCycleCommand(Guid CycleId, CycleOutcome Outcome) : IRequest<Result<CycleDto>>;

public class CompleteCycleHandler : IRequestHandler<CompleteCycleCommand, Result<CycleDto>>
{
    private readonly ITreatmentCycleRepository _cycleRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CompleteCycleHandler(ITreatmentCycleRepository cycleRepo, IUnitOfWork unitOfWork)
    {
        _cycleRepo = cycleRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CycleDto>> Handle(CompleteCycleCommand request, CancellationToken ct)
    {
        var cycle = await _cycleRepo.GetByIdAsync(request.CycleId, ct);
        if (cycle == null)
            return Result<CycleDto>.Failure("Cycle not found");

        cycle.Complete(request.Outcome);
        await _cycleRepo.UpdateAsync(cycle, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<CycleDto>.Success(CycleDto.FromEntity(cycle));
    }
}

// ==================== CANCEL CYCLE ====================
public record CancelCycleCommand(Guid CycleId) : IRequest<Result>;

public class CancelCycleHandler : IRequestHandler<CancelCycleCommand, Result>
{
    private readonly ITreatmentCycleRepository _cycleRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CancelCycleHandler(ITreatmentCycleRepository cycleRepo, IUnitOfWork unitOfWork)
    {
        _cycleRepo = cycleRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(CancelCycleCommand request, CancellationToken ct)
    {
        var cycle = await _cycleRepo.GetByIdAsync(request.CycleId, ct);
        if (cycle == null)
            return Result.Failure("Cycle not found");

        cycle.Cancel();
        await _cycleRepo.UpdateAsync(cycle, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }
}

// ==================== DTO ====================
public record CycleDto(
    Guid Id,
    string CycleCode,
    Guid CoupleId,
    string Method,
    string CurrentPhase,
    DateTime StartDate,
    DateTime? EndDate,
    string Outcome,
    string? Notes,
    DateTime CreatedAt
)
{
    public static CycleDto FromEntity(TreatmentCycle c) => new(
        c.Id, c.CycleCode, c.CoupleId, c.Method.ToString(), c.CurrentPhase.ToString(),
        c.StartDate, c.EndDate, c.Outcome.ToString(), c.Notes, c.CreatedAt
    );
}
