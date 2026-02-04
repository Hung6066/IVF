using FluentValidation;
using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.Ultrasounds.Commands;

// ==================== CREATE ULTRASOUND ====================
public record CreateUltrasoundCommand(
    Guid CycleId,
    DateTime ExamDate,
    string UltrasoundType,
    Guid? DoctorId
) : IRequest<Result<UltrasoundDto>>;

public class CreateUltrasoundValidator : AbstractValidator<CreateUltrasoundCommand>
{
    public CreateUltrasoundValidator()
    {
        RuleFor(x => x.CycleId).NotEmpty();
        RuleFor(x => x.UltrasoundType).NotEmpty().MaximumLength(30);
    }
}

public class CreateUltrasoundHandler : IRequestHandler<CreateUltrasoundCommand, Result<UltrasoundDto>>
{
    private readonly IUltrasoundRepository _usRepo;
    private readonly ITreatmentCycleRepository _cycleRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CreateUltrasoundHandler(IUltrasoundRepository usRepo, ITreatmentCycleRepository cycleRepo, IUnitOfWork unitOfWork)
    {
        _usRepo = usRepo;
        _cycleRepo = cycleRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<UltrasoundDto>> Handle(CreateUltrasoundCommand request, CancellationToken ct)
    {
        var cycle = await _cycleRepo.GetByIdAsync(request.CycleId, ct);
        if (cycle == null)
            return Result<UltrasoundDto>.Failure("Cycle not found");

        var us = Ultrasound.Create(request.CycleId, request.ExamDate, request.UltrasoundType, request.DoctorId);
        await _usRepo.AddAsync(us, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<UltrasoundDto>.Success(UltrasoundDto.FromEntity(us));
    }
}

// ==================== RECORD FOLLICLES ====================
public record RecordFolliclesCommand(
    Guid UltrasoundId,
    int? LeftOvaryCount,
    int? RightOvaryCount,
    string? LeftFollicles,
    string? RightFollicles,
    decimal? EndometriumThickness,
    string? Findings
) : IRequest<Result<UltrasoundDto>>;

public class RecordFolliclesHandler : IRequestHandler<RecordFolliclesCommand, Result<UltrasoundDto>>
{
    private readonly IUltrasoundRepository _usRepo;
    private readonly IUnitOfWork _unitOfWork;

    public RecordFolliclesHandler(IUltrasoundRepository usRepo, IUnitOfWork unitOfWork)
    {
        _usRepo = usRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<UltrasoundDto>> Handle(RecordFolliclesCommand request, CancellationToken ct)
    {
        var us = await _usRepo.GetByIdAsync(request.UltrasoundId, ct);
        if (us == null)
            return Result<UltrasoundDto>.Failure("Ultrasound not found");

        us.RecordFollicles(
            request.LeftOvaryCount, request.RightOvaryCount,
            request.LeftFollicles, request.RightFollicles,
            request.EndometriumThickness, request.Findings
        );
        
        await _usRepo.UpdateAsync(us, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<UltrasoundDto>.Success(UltrasoundDto.FromEntity(us));
    }
}

// ==================== DTO ====================
public record UltrasoundDto(
    Guid Id,
    Guid CycleId,
    DateTime ExamDate,
    string UltrasoundType,
    int? LeftOvaryCount,
    int? RightOvaryCount,
    decimal? EndometriumThickness,
    string? LeftFollicles,
    string? RightFollicles,
    string? Findings,
    DateTime CreatedAt
)
{
    public static UltrasoundDto FromEntity(Ultrasound u) => new(
        u.Id, u.CycleId, u.ExamDate, u.UltrasoundType,
        u.LeftOvaryCount, u.RightOvaryCount, u.EndometriumThickness,
        u.LeftFollicles, u.RightFollicles, u.Findings, u.CreatedAt
    );
}
