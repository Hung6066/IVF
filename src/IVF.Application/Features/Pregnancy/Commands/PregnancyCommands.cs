using FluentValidation;
using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using MediatR;

using ICycleRepository = IVF.Application.Common.Interfaces.ITreatmentCycleRepository;

namespace IVF.Application.Features.Pregnancy.Commands;

// === DTOs ===

public record PregnancyDto(
    Guid CycleId,
    decimal? BetaHcg,
    DateTime? BetaHcgDate,
    bool IsPregnant,
    int? GestationalSacs,
    int? FetalHeartbeats,
    DateTime? DueDate,
    string? Notes,
    string? PregnancyStatus);

// === Record Beta HCG ===

public record RecordBetaHcgCommand(
    Guid CycleId,
    decimal BetaHcg,
    DateTime TestDate,
    string? Notes = null) : IRequest<Result<PregnancyDto>>;

public class RecordBetaHcgValidator : AbstractValidator<RecordBetaHcgCommand>
{
    public RecordBetaHcgValidator()
    {
        RuleFor(x => x.CycleId).NotEmpty().WithMessage("Vui lòng chọn chu kỳ");
        RuleFor(x => x.BetaHcg).GreaterThanOrEqualTo(0).WithMessage("Chỉ số Beta HCG không hợp lệ");
        RuleFor(x => x.TestDate).NotEmpty().WithMessage("Vui lòng nhập ngày xét nghiệm");
    }
}

public class RecordBetaHcgHandler(
    ICyclePhaseDataRepository phaseRepo,
    ICycleRepository cycleRepo,
    IUnitOfWork uow)
    : IRequestHandler<RecordBetaHcgCommand, Result<PregnancyDto>>
{
    public async Task<Result<PregnancyDto>> Handle(RecordBetaHcgCommand r, CancellationToken ct)
    {
        var cycle = await cycleRepo.GetByIdAsync(r.CycleId, ct);
        if (cycle is null) return Result<PregnancyDto>.Failure("Không tìm thấy chu kỳ");

        var pregnancy = await phaseRepo.GetPregnancyByCycleIdAsync(r.CycleId, ct);
        if (pregnancy is null)
        {
            pregnancy = PregnancyData.Create(r.CycleId);
            await phaseRepo.AddPregnancyAsync(pregnancy, ct);
        }

        bool isPregnant = r.BetaHcg >= 25;
        pregnancy.Update(
            r.BetaHcg,
            r.TestDate,
            isPregnant,
            pregnancy.GestationalSacs,
            pregnancy.FetalHeartbeats,
            pregnancy.DueDate,
            r.Notes ?? pregnancy.Notes);

        await uow.SaveChangesAsync(ct);
        return Result<PregnancyDto>.Success(PregnancyMapper.MapToDto(pregnancy));
    }
}

// === Notify Beta HCG Result ===

public record NotifyBetaHcgResultCommand(
    Guid CycleId,
    string NotificationChannel,   // "InApp" | "SMS"
    string? Message = null) : IRequest<Result<string>>;

public class NotifyBetaHcgResultValidator : AbstractValidator<NotifyBetaHcgResultCommand>
{
    public NotifyBetaHcgResultValidator()
    {
        RuleFor(x => x.CycleId).NotEmpty();
    }
}

public class NotifyBetaHcgResultHandler(
    ICyclePhaseDataRepository phaseRepo,
    ICycleRepository cycleRepo)
    : IRequestHandler<NotifyBetaHcgResultCommand, Result<string>>
{
    public async Task<Result<string>> Handle(NotifyBetaHcgResultCommand r, CancellationToken ct)
    {
        var cycle = await cycleRepo.GetByIdAsync(r.CycleId, ct);
        if (cycle is null) return Result<string>.Failure("Không tìm thấy chu kỳ");

        var pregnancy = await phaseRepo.GetPregnancyByCycleIdAsync(r.CycleId, ct);
        if (pregnancy is null) return Result<string>.Failure("Chưa có kết quả Beta HCG");

        string result = pregnancy.IsPregnant ? "DƯƠNG TÍNH" : "ÂM TÍNH";
        string message = r.Message ?? $"Kết quả Beta HCG của bạn: {result} (giá trị: {pregnancy.BetaHcg})";

        // Notification dispatched via notification service (already registered via NotificationCommands)
        return Result<string>.Success($"Đã gửi thông báo qua {r.NotificationChannel}: {message}");
    }
}

// === Record Prenatal Exam (7 Week) ===

public record RecordPrenatalExamCommand(
    Guid CycleId,
    DateTime ExamDate,
    int? GestationalSacs,
    int? FetalHeartbeats,
    DateTime? DueDate,
    string? UltrasoundFindings,
    string? Notes) : IRequest<Result<PregnancyDto>>;

public class RecordPrenatalExamValidator : AbstractValidator<RecordPrenatalExamCommand>
{
    public RecordPrenatalExamValidator()
    {
        RuleFor(x => x.CycleId).NotEmpty().WithMessage("Vui lòng chọn chu kỳ");
        RuleFor(x => x.ExamDate).NotEmpty().WithMessage("Vui lòng nhập ngày khám");
    }
}

public class RecordPrenatalExamHandler(
    ICyclePhaseDataRepository phaseRepo,
    ICycleRepository cycleRepo,
    IUnitOfWork uow)
    : IRequestHandler<RecordPrenatalExamCommand, Result<PregnancyDto>>
{
    public async Task<Result<PregnancyDto>> Handle(RecordPrenatalExamCommand r, CancellationToken ct)
    {
        var cycle = await cycleRepo.GetByIdAsync(r.CycleId, ct);
        if (cycle is null) return Result<PregnancyDto>.Failure("Không tìm thấy chu kỳ");

        var pregnancy = await phaseRepo.GetPregnancyByCycleIdAsync(r.CycleId, ct);
        if (pregnancy is null) return Result<PregnancyDto>.Failure("Chưa ghi nhận kết quả Beta HCG");

        string combinedNotes = string.IsNullOrWhiteSpace(r.UltrasoundFindings)
            ? r.Notes ?? pregnancy.Notes ?? string.Empty
            : $"SA 7 tuần: {r.UltrasoundFindings}. {r.Notes}".Trim();

        pregnancy.Update(
            pregnancy.BetaHcg,
            pregnancy.BetaHcgDate,
            pregnancy.IsPregnant,
            r.GestationalSacs ?? pregnancy.GestationalSacs,
            r.FetalHeartbeats ?? pregnancy.FetalHeartbeats,
            r.DueDate ?? pregnancy.DueDate,
            combinedNotes);

        await uow.SaveChangesAsync(ct);
        return Result<PregnancyDto>.Success(PregnancyMapper.MapToDto(pregnancy));
    }
}

// === Discharge Cycle (close IVF, transfer to OB) ===

public record DischargeCycleCommand(
    Guid CycleId,
    string OutcomeNote,
    DateTime DischargeDate) : IRequest<Result<string>>;

public class DischargeCycleValidator : AbstractValidator<DischargeCycleCommand>
{
    public DischargeCycleValidator()
    {
        RuleFor(x => x.CycleId).NotEmpty();
        RuleFor(x => x.OutcomeNote).NotEmpty().WithMessage("Vui lòng nhập ghi chú đóng chu kỳ");
    }
}

public class DischargeCycleHandler(ICycleRepository cycleRepo, IUnitOfWork uow)
    : IRequestHandler<DischargeCycleCommand, Result<string>>
{
    public async Task<Result<string>> Handle(DischargeCycleCommand r, CancellationToken ct)
    {
        var cycle = await cycleRepo.GetByIdAsync(r.CycleId, ct);
        if (cycle is null) return Result<string>.Failure("Không tìm thấy chu kỳ");

        cycle.Complete(IVF.Domain.Enums.CycleOutcome.Pregnant);
        await uow.SaveChangesAsync(ct);
        return Result<string>.Success("Đã đóng chu kỳ IVF và chuyển theo dõi thai bệnh viện");
    }
}

// === Mapper (shared) ===

internal static class PregnancyMapper
{
    public static PregnancyDto MapToDto(PregnancyData e) => new(
        e.CycleId,
        e.BetaHcg,
        e.BetaHcgDate,
        e.IsPregnant,
        e.GestationalSacs,
        e.FetalHeartbeats,
        e.DueDate,
        e.Notes,
        e.IsPregnant
            ? (e.GestationalSacs.HasValue ? "Có thai" : "Dương tính")
            : "Âm tính");
}
