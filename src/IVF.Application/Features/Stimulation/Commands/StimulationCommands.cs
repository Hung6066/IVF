using FluentValidation;
using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using MediatR;

// Alias for brevity
using ICycleRepository = IVF.Application.Common.Interfaces.ITreatmentCycleRepository;

namespace IVF.Application.Features.Stimulation.Commands;

// === DTOs ===

public record FollicleScanDto(
    Guid Id,
    Guid CycleId,
    DateTime ScanDate,
    int CycleDay,
    int? Size12Follicle,
    int? Size14Follicle,
    int? TotalFollicles,
    decimal? EndometriumThickness,
    string? EndometriumPattern,
    decimal? E2,
    decimal? Lh,
    decimal? P4,
    string? Notes,
    string ScanType,
    DateTime CreatedAt);

public record StimulationTrackerDto(
    Guid CycleId,
    DateTime? LastMenstruation,
    DateTime? StartDate,
    int? StartDay,
    List<FollicleScanDto> FollicleScans,
    decimal? CurrentEndometriumThickness,
    int? CurrentFolliclesReady,
    string? TriggerDrug,
    DateTime? TriggerDate,
    TimeSpan? TriggerTime,
    bool TriggerGiven,
    decimal? LhLab,
    decimal? E2Lab,
    decimal? P4Lab,
    string? ProcedureType,
    DateTime? AspirationDate)
{
    /// <summary>
    /// Planned OPU time = trigger date+time + 36 hours (standard clinical protocol)
    /// </summary>
    public DateTime? PlannedOpuDate => TriggerDate.HasValue && TriggerTime.HasValue
        ? TriggerDate.Value.Date.Add(TriggerTime.Value).AddHours(36)
        : null;
}

// === Record Follicle Scan ===

public record RecordFollicleScanCommand(
    Guid CycleId,
    DateTime ScanDate,
    int CycleDay,
    int? Size12Follicle,
    int? Size14Follicle,
    int? TotalFollicles,
    decimal? EndometriumThickness,
    string? EndometriumPattern,
    decimal? E2,
    decimal? Lh,
    decimal? P4,
    string? Notes,
    string ScanType = "Stimulation") : IRequest<Result<StimulationTrackerDto>>;

public class RecordFollicleScanValidator : AbstractValidator<RecordFollicleScanCommand>
{
    public RecordFollicleScanValidator()
    {
        RuleFor(x => x.CycleId).NotEmpty().WithMessage("Vui lòng chọn chu kỳ");
        RuleFor(x => x.ScanDate).NotEmpty().WithMessage("Vui lòng nhập ngày SA");
        RuleFor(x => x.CycleDay).GreaterThan(0).WithMessage("Ngày chu kỳ phải lớn hơn 0");
    }
}

public class RecordFollicleScanHandler(
    ICyclePhaseDataRepository phaseRepo,
    ICycleRepository cycleRepo,
    IUnitOfWork uow)
    : IRequestHandler<RecordFollicleScanCommand, Result<StimulationTrackerDto>>
{
    public async Task<Result<StimulationTrackerDto>> Handle(RecordFollicleScanCommand r, CancellationToken ct)
    {
        var cycle = await cycleRepo.GetByIdAsync(r.CycleId, ct);
        if (cycle is null) return Result<StimulationTrackerDto>.Failure("Không tìm thấy chu kỳ");

        var stim = await phaseRepo.GetStimulationByCycleIdAsync(r.CycleId, ct);
        if (stim is null)
        {
            stim = StimulationData.Create(r.CycleId);
            await phaseRepo.AddStimulationAsync(stim, ct);
        }

        // Update current follicle/endometrium values from latest scan
        stim.Update(
            stim.LastMenstruation,
            stim.StartDate,
            stim.StartDay,
            r.Size12Follicle,
            r.Size14Follicle,
            r.EndometriumThickness,
            stim.TriggerDrug,
            stim.TriggerDrug2,
            stim.HcgDate,
            stim.HcgDate2,
            stim.HcgTime,
            stim.HcgTime2,
            r.Lh,
            r.E2,
            r.P4,
            stim.ProcedureType,
            stim.AspirationDate,
            stim.ProcedureDate,
            stim.AspirationNo,
            stim.TechniqueWife,
            stim.TechniqueHusband);

        await uow.SaveChangesAsync(ct);

        return Result<StimulationTrackerDto>.Success(MapToTracker(stim, r));
    }

    private static StimulationTrackerDto MapToTracker(StimulationData stim, RecordFollicleScanCommand r)
    {
        var scan = new FollicleScanDto(
            Guid.NewGuid(),
            stim.CycleId,
            r.ScanDate,
            r.CycleDay,
            r.Size12Follicle,
            r.Size14Follicle,
            r.TotalFollicles,
            r.EndometriumThickness,
            r.EndometriumPattern,
            r.E2,
            r.Lh,
            r.P4,
            r.Notes,
            r.ScanType,
            DateTime.UtcNow);

        return new StimulationTrackerDto(
            stim.CycleId,
            stim.LastMenstruation,
            stim.StartDate,
            stim.StartDay,
            new List<FollicleScanDto> { scan },
            stim.EndometriumThickness,
            stim.Size14Follicle,
            stim.TriggerDrug,
            stim.HcgDate,
            stim.HcgTime,
            stim.TriggerDrug is not null,
            stim.LhLab,
            stim.E2Lab,
            stim.P4Lab,
            stim.ProcedureType,
            stim.AspirationDate);
    }
}

// === Record Trigger Shot ===

public record RecordTriggerShotCommand(
    Guid CycleId,
    string TriggerDrug,
    DateTime TriggerDate,
    TimeSpan TriggerTime,
    string? TriggerDrug2 = null,
    DateTime? TriggerDate2 = null,
    TimeSpan? TriggerTime2 = null,
    decimal? LhLab = null,
    decimal? E2Lab = null,
    decimal? P4Lab = null) : IRequest<Result<StimulationTrackerDto>>;

public class RecordTriggerShotValidator : AbstractValidator<RecordTriggerShotCommand>
{
    public RecordTriggerShotValidator()
    {
        RuleFor(x => x.CycleId).NotEmpty().WithMessage("Vui lòng chọn chu kỳ");
        RuleFor(x => x.TriggerDrug).NotEmpty().WithMessage("Vui lòng chọn thuốc trigger");
        RuleFor(x => x.TriggerDate).NotEmpty().WithMessage("Vui lòng nhập ngày tiêm rụng trứng");
    }
}

public class RecordTriggerShotHandler(
    ICyclePhaseDataRepository phaseRepo,
    ICycleRepository cycleRepo,
    IUnitOfWork uow)
    : IRequestHandler<RecordTriggerShotCommand, Result<StimulationTrackerDto>>
{
    public async Task<Result<StimulationTrackerDto>> Handle(RecordTriggerShotCommand r, CancellationToken ct)
    {
        var cycle = await cycleRepo.GetByIdAsync(r.CycleId, ct);
        if (cycle is null) return Result<StimulationTrackerDto>.Failure("Không tìm thấy chu kỳ");

        var stim = await phaseRepo.GetStimulationByCycleIdAsync(r.CycleId, ct);
        if (stim is null)
        {
            stim = StimulationData.Create(r.CycleId);
            await phaseRepo.AddStimulationAsync(stim, ct);
        }

        stim.Update(
            stim.LastMenstruation,
            stim.StartDate,
            stim.StartDay,
            stim.Size12Follicle,
            stim.Size14Follicle,
            stim.EndometriumThickness,
            r.TriggerDrug,
            r.TriggerDrug2,
            r.TriggerDate,
            r.TriggerDate2,
            r.TriggerTime,
            r.TriggerTime2,
            r.LhLab,
            r.E2Lab,
            r.P4Lab,
            stim.ProcedureType,
            stim.AspirationDate,
            stim.ProcedureDate,
            stim.AspirationNo,
            stim.TechniqueWife,
            stim.TechniqueHusband);

        await uow.SaveChangesAsync(ct);

        return Result<StimulationTrackerDto>.Success(new StimulationTrackerDto(
            stim.CycleId,
            stim.LastMenstruation,
            stim.StartDate,
            stim.StartDay,
            new List<FollicleScanDto>(),
            stim.EndometriumThickness,
            stim.Size14Follicle,
            stim.TriggerDrug,
            stim.HcgDate,
            stim.HcgTime,
            stim.TriggerDrug is not null,
            stim.LhLab,
            stim.E2Lab,
            stim.P4Lab,
            stim.ProcedureType,
            stim.AspirationDate));
    }
}

// === Evaluate Follicle Readiness ===

public record EvaluateFollicleReadinessCommand(
    Guid CycleId,
    string Decision,   // "Proceed" | "Cancel" | "IVM" | "CoastDay"
    string? Reason = null) : IRequest<Result<string>>;

public class EvaluateFollicleReadinessValidator : AbstractValidator<EvaluateFollicleReadinessCommand>
{
    private static readonly string[] ValidDecisions = ["Proceed", "Cancel", "IVM", "CoastDay"];

    public EvaluateFollicleReadinessValidator()
    {
        RuleFor(x => x.CycleId).NotEmpty();
        RuleFor(x => x.Decision).Must(d => ValidDecisions.Contains(d))
            .WithMessage("Quyết định không hợp lệ. Chọn: Proceed, Cancel, IVM, CoastDay");
    }
}

public class EvaluateFollicleReadinessHandler(ICycleRepository cycleRepo)
    : IRequestHandler<EvaluateFollicleReadinessCommand, Result<string>>
{
    public async Task<Result<string>> Handle(EvaluateFollicleReadinessCommand r, CancellationToken ct)
    {
        var cycle = await cycleRepo.GetByIdAsync(r.CycleId, ct);
        if (cycle is null) return Result<string>.Failure("Không tìm thấy chu kỳ");
        return Result<string>.Success(r.Decision);
    }
}
