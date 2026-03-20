using FluentValidation;
using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.Procedures.Commands;

// === DTO ===

public record ProcedureDto(
    Guid Id,
    Guid PatientId,
    string PatientName,
    string PatientCode,
    Guid? CycleId,
    Guid PerformedByDoctorId,
    string PerformedByDoctorName,
    Guid? AssistantDoctorId,
    string? AssistantDoctorName,
    string ProcedureType,
    string? ProcedureCode,
    string ProcedureName,
    DateTime ScheduledAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    int? DurationMinutes,
    string? AnesthesiaType,
    string? AnesthesiaNotes,
    string? RoomNumber,
    string? PreOpNotes,
    string? IntraOpFindings,
    string? PostOpNotes,
    string? Complications,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

// === Create ===

public record CreateProcedureCommand(
    Guid PatientId,
    Guid PerformedByDoctorId,
    string ProcedureType,
    string ProcedureName,
    DateTime ScheduledAt,
    Guid? CycleId = null,
    Guid? AssistantDoctorId = null,
    string? ProcedureCode = null,
    string? AnesthesiaType = null,
    string? RoomNumber = null,
    string? PreOpNotes = null) : IRequest<Result<ProcedureDto>>;

public class CreateProcedureValidator : AbstractValidator<CreateProcedureCommand>
{
    public CreateProcedureValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty().WithMessage("Vui lòng chọn bệnh nhân");
        RuleFor(x => x.PerformedByDoctorId).NotEmpty().WithMessage("Vui lòng chọn bác sĩ thực hiện");
        RuleFor(x => x.ProcedureType).NotEmpty().WithMessage("Vui lòng chọn loại thủ thuật");
        RuleFor(x => x.ProcedureName).NotEmpty().WithMessage("Vui lòng nhập tên thủ thuật");
    }
}

public class CreateProcedureHandler(IProcedureRepository repo, IUnitOfWork uow)
    : IRequestHandler<CreateProcedureCommand, Result<ProcedureDto>>
{
    public async Task<Result<ProcedureDto>> Handle(CreateProcedureCommand request, CancellationToken ct)
    {
        var procedure = Procedure.Create(
            request.PatientId, request.PerformedByDoctorId,
            request.ProcedureType, request.ProcedureName, request.ScheduledAt,
            request.CycleId, request.AssistantDoctorId,
            request.ProcedureCode, request.AnesthesiaType, request.RoomNumber, request.PreOpNotes);

        await repo.AddAsync(procedure, ct);
        await uow.SaveChangesAsync(ct);

        var created = await repo.GetByIdAsync(procedure.Id, ct);
        return Result<ProcedureDto>.Success(ProcedureMapper.MapToDto(created!));
    }
}

// === Start ===

public record StartProcedureCommand(Guid ProcedureId) : IRequest<Result<ProcedureDto>>;

public class StartProcedureHandler(IProcedureRepository repo, IUnitOfWork uow)
    : IRequestHandler<StartProcedureCommand, Result<ProcedureDto>>
{
    public async Task<Result<ProcedureDto>> Handle(StartProcedureCommand request, CancellationToken ct)
    {
        var proc = await repo.GetByIdAsync(request.ProcedureId, ct);
        if (proc is null) return Result<ProcedureDto>.Failure("Không tìm thấy thủ thuật");
        proc.Start();
        await uow.SaveChangesAsync(ct);
        return Result<ProcedureDto>.Success(ProcedureMapper.MapToDto(proc));
    }
}

// === Complete ===

public record CompleteProcedureCommand(
    Guid ProcedureId,
    string? IntraOpFindings,
    string? PostOpNotes,
    string? Complications,
    int? DurationMinutes) : IRequest<Result<ProcedureDto>>;

public class CompleteProcedureHandler(IProcedureRepository repo, IUnitOfWork uow)
    : IRequestHandler<CompleteProcedureCommand, Result<ProcedureDto>>
{
    public async Task<Result<ProcedureDto>> Handle(CompleteProcedureCommand request, CancellationToken ct)
    {
        var proc = await repo.GetByIdAsync(request.ProcedureId, ct);
        if (proc is null) return Result<ProcedureDto>.Failure("Không tìm thấy thủ thuật");
        proc.Complete(request.IntraOpFindings, request.PostOpNotes, request.Complications, request.DurationMinutes);
        await uow.SaveChangesAsync(ct);
        return Result<ProcedureDto>.Success(ProcedureMapper.MapToDto(proc));
    }
}

// === Cancel ===

public record CancelProcedureCommand(Guid ProcedureId, string? Reason = null) : IRequest<Result<ProcedureDto>>;

public class CancelProcedureHandler(IProcedureRepository repo, IUnitOfWork uow)
    : IRequestHandler<CancelProcedureCommand, Result<ProcedureDto>>
{
    public async Task<Result<ProcedureDto>> Handle(CancelProcedureCommand request, CancellationToken ct)
    {
        var proc = await repo.GetByIdAsync(request.ProcedureId, ct);
        if (proc is null) return Result<ProcedureDto>.Failure("Không tìm thấy thủ thuật");
        proc.Cancel(request.Reason);
        await uow.SaveChangesAsync(ct);
        return Result<ProcedureDto>.Success(ProcedureMapper.MapToDto(proc));
    }
}

// === Postpone ===

public record PostponeProcedureCommand(Guid ProcedureId, DateTime NewScheduledAt, string? Reason = null) : IRequest<Result<ProcedureDto>>;

public class PostponeProcedureHandler(IProcedureRepository repo, IUnitOfWork uow)
    : IRequestHandler<PostponeProcedureCommand, Result<ProcedureDto>>
{
    public async Task<Result<ProcedureDto>> Handle(PostponeProcedureCommand request, CancellationToken ct)
    {
        var proc = await repo.GetByIdAsync(request.ProcedureId, ct);
        if (proc is null) return Result<ProcedureDto>.Failure("Không tìm thấy thủ thuật");
        proc.Postpone(request.NewScheduledAt, request.Reason);
        await uow.SaveChangesAsync(ct);
        return Result<ProcedureDto>.Success(ProcedureMapper.MapToDto(proc));
    }
}

// === Mapping ===

internal static class ProcedureMapper
{
    public static ProcedureDto MapToDto(Procedure p) => new(
        p.Id, p.PatientId,
        p.Patient?.FullName ?? "",
        p.Patient?.PatientCode ?? "",
        p.CycleId, p.PerformedByDoctorId,
        p.PerformedByDoctor?.User?.FullName ?? "",
        p.AssistantDoctorId,
        p.AssistantDoctor?.User?.FullName,
        p.ProcedureType, p.ProcedureCode, p.ProcedureName,
        p.ScheduledAt, p.StartedAt, p.CompletedAt, p.DurationMinutes,
        p.AnesthesiaType, p.AnesthesiaNotes, p.RoomNumber,
        p.PreOpNotes, p.IntraOpFindings, p.PostOpNotes, p.Complications,
        p.Status, p.CreatedAt, p.UpdatedAt);
}
