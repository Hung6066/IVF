using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using MediatR;

namespace IVF.Application.Features.Cycles.Commands;

// ==================== UPDATE TREATMENT INDICATION ====================
public record UpdateTreatmentIndicationCommand(
    Guid CycleId,
    DateTime? LastMenstruation,
    string? TreatmentType,
    string? Regimen,
    bool FreezeAll,
    bool Sis,
    string? WifeDiagnosis,
    string? WifeDiagnosis2,
    string? HusbandDiagnosis,
    string? HusbandDiagnosis2,
    Guid? UltrasoundDoctorId,
    Guid? IndicationDoctorId,
    Guid? FshDoctorId,
    Guid? MidwifeId,
    bool Timelapse,
    bool PgtA,
    bool PgtSr,
    bool PgtM,
    string? SubType,
    string? ScientificResearch,
    string? Source,
    string? ProcedurePlace,
    string? StopReason,
    DateTime? TreatmentMonth,
    int PreviousTreatmentsAtSite,
    int PreviousTreatmentsOther
) : IRequest<Result<TreatmentIndicationDto>>;

public class UpdateTreatmentIndicationHandler : IRequestHandler<UpdateTreatmentIndicationCommand, Result<TreatmentIndicationDto>>
{
    private readonly ICyclePhaseDataRepository _phaseRepo;
    private readonly ITreatmentCycleRepository _cycleRepo;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateTreatmentIndicationHandler(
        ICyclePhaseDataRepository phaseRepo,
        ITreatmentCycleRepository cycleRepo,
        IUnitOfWork unitOfWork)
    {
        _phaseRepo = phaseRepo;
        _cycleRepo = cycleRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<TreatmentIndicationDto>> Handle(UpdateTreatmentIndicationCommand r, CancellationToken ct)
    {
        var cycle = await _cycleRepo.GetByIdAsync(r.CycleId, ct);
        if (cycle == null)
            return Result<TreatmentIndicationDto>.Failure("Cycle not found");

        var indication = await _phaseRepo.GetIndicationByCycleIdAsync(r.CycleId, ct);
        if (indication == null)
        {
            indication = TreatmentIndication.Create(r.CycleId);
            await _phaseRepo.AddIndicationAsync(indication, ct);
        }

        indication.Update(
            r.LastMenstruation, r.TreatmentType, r.Regimen, r.FreezeAll, r.Sis,
            r.WifeDiagnosis, r.WifeDiagnosis2, r.HusbandDiagnosis, r.HusbandDiagnosis2,
            r.UltrasoundDoctorId, r.IndicationDoctorId, r.FshDoctorId, r.MidwifeId,
            r.Timelapse, r.PgtA, r.PgtSr, r.PgtM,
            r.SubType, r.ScientificResearch, r.Source, r.ProcedurePlace, r.StopReason,
            r.TreatmentMonth, r.PreviousTreatmentsAtSite, r.PreviousTreatmentsOther);

        // Auto-advance if in Consultation
        if (cycle.CurrentPhase == CyclePhase.Consultation)
        {
            cycle.AdvancePhase(CyclePhase.OvarianStimulation);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result<TreatmentIndicationDto>.Success(TreatmentIndicationDto.FromEntity(indication));
    }
}

// ==================== UPDATE STIMULATION DATA ====================
public record StimulationDrugInput(string DrugName, int Duration, string? Posology);

public record UpdateStimulationDataCommand(
    Guid CycleId,
    DateTime? LastMenstruation,
    DateTime? StartDate,
    int? StartDay,
    List<StimulationDrugInput>? Drugs,
    int? Size12Follicle,
    int? Size14Follicle,
    decimal? EndometriumThickness,
    string? TriggerDrug,
    string? TriggerDrug2,
    DateTime? HcgDate,
    DateTime? HcgDate2,
    TimeSpan? HcgTime,
    TimeSpan? HcgTime2,
    decimal? LhLab,
    decimal? E2Lab,
    decimal? P4Lab,
    string? ProcedureType,
    DateTime? AspirationDate,
    DateTime? ProcedureDate,
    int? AspirationNo,
    string? TechniqueWife,
    string? TechniqueHusband
) : IRequest<Result<StimulationDataDto>>;

public class UpdateStimulationDataHandler : IRequestHandler<UpdateStimulationDataCommand, Result<StimulationDataDto>>
{
    private readonly ICyclePhaseDataRepository _phaseRepo;
    private readonly ITreatmentCycleRepository _cycleRepo;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateStimulationDataHandler(
        ICyclePhaseDataRepository phaseRepo,
        ITreatmentCycleRepository cycleRepo,
        IUnitOfWork unitOfWork)
    {
        _phaseRepo = phaseRepo;
        _cycleRepo = cycleRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<StimulationDataDto>> Handle(UpdateStimulationDataCommand r, CancellationToken ct)
    {
        var cycle = await _cycleRepo.GetByIdAsync(r.CycleId, ct);
        if (cycle == null)
            return Result<StimulationDataDto>.Failure("Cycle not found");

        var data = await _phaseRepo.GetStimulationByCycleIdAsync(r.CycleId, ct);
        if (data == null)
        {
            data = StimulationData.Create(r.CycleId);
            await _phaseRepo.AddStimulationAsync(data, ct);
        }

        data.Update(
            r.LastMenstruation, r.StartDate, r.StartDay,
            r.Size12Follicle, r.Size14Follicle, r.EndometriumThickness,
            r.TriggerDrug, r.TriggerDrug2,
            r.HcgDate, r.HcgDate2, r.HcgTime, r.HcgTime2,
            r.LhLab, r.E2Lab, r.P4Lab,
            r.ProcedureType, r.AspirationDate, r.ProcedureDate, r.AspirationNo,
            r.TechniqueWife, r.TechniqueHusband);

        // Update normalized drug collection
        if (r.Drugs != null)
        {
            var drugEntities = r.Drugs.Select((d, i) => StimulationDrug.Create(data.Id, i + 1, d.DrugName, d.Duration, d.Posology));
            data.SetDrugs(drugEntities);
        }

        // Auto-advance logic
        if (r.AspirationDate.HasValue && cycle.CurrentPhase < CyclePhase.EggRetrieval)
        {
            cycle.AdvancePhase(CyclePhase.EggRetrieval);
        }
        else if (r.HcgDate.HasValue && cycle.CurrentPhase < CyclePhase.TriggerShot)
        {
            cycle.AdvancePhase(CyclePhase.TriggerShot);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result<StimulationDataDto>.Success(StimulationDataDto.FromEntity(data));
    }
}

// ==================== UPDATE CULTURE DATA ====================
public record UpdateCultureDataCommand(
    Guid CycleId,
    int TotalFreezedEmbryo,
    int TotalThawedEmbryo,
    int TotalTransferedEmbryo,
    int RemainFreezedEmbryo
) : IRequest<Result<CultureDataDto>>;

public class UpdateCultureDataHandler : IRequestHandler<UpdateCultureDataCommand, Result<CultureDataDto>>
{
    private readonly ICyclePhaseDataRepository _phaseRepo;
    private readonly ITreatmentCycleRepository _cycleRepo;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCultureDataHandler(
        ICyclePhaseDataRepository phaseRepo,
        ITreatmentCycleRepository cycleRepo,
        IUnitOfWork unitOfWork)
    {
        _phaseRepo = phaseRepo;
        _cycleRepo = cycleRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CultureDataDto>> Handle(UpdateCultureDataCommand r, CancellationToken ct)
    {
        var cycle = await _cycleRepo.GetByIdAsync(r.CycleId, ct);
        if (cycle == null)
            return Result<CultureDataDto>.Failure("Cycle not found");

        var data = await _phaseRepo.GetCultureByCycleIdAsync(r.CycleId, ct);
        if (data == null)
        {
            data = CultureData.Create(r.CycleId);
            await _phaseRepo.AddCultureAsync(data, ct);
        }

        data.Update(r.TotalFreezedEmbryo, r.TotalThawedEmbryo, r.TotalTransferedEmbryo, r.RemainFreezedEmbryo);

        // Auto-advance
        if (cycle.CurrentPhase < CyclePhase.EmbryoCulture)
        {
            cycle.AdvancePhase(CyclePhase.EmbryoCulture);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result<CultureDataDto>.Success(CultureDataDto.FromEntity(data));
    }
}

// ==================== UPDATE TRANSFER DATA ====================
public record UpdateTransferDataCommand(
    Guid CycleId,
    DateTime? TransferDate,
    DateTime? ThawingDate,
    int DayOfTransfered,
    string? LabNote
) : IRequest<Result<TransferDataDto>>;

public class UpdateTransferDataHandler : IRequestHandler<UpdateTransferDataCommand, Result<TransferDataDto>>
{
    private readonly ICyclePhaseDataRepository _phaseRepo;
    private readonly ITreatmentCycleRepository _cycleRepo;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateTransferDataHandler(
        ICyclePhaseDataRepository phaseRepo,
        ITreatmentCycleRepository cycleRepo,
        IUnitOfWork unitOfWork)
    {
        _phaseRepo = phaseRepo;
        _cycleRepo = cycleRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<TransferDataDto>> Handle(UpdateTransferDataCommand r, CancellationToken ct)
    {
        var cycle = await _cycleRepo.GetByIdAsync(r.CycleId, ct);
        if (cycle == null)
            return Result<TransferDataDto>.Failure("Cycle not found");

        var data = await _phaseRepo.GetTransferByCycleIdAsync(r.CycleId, ct);
        if (data == null)
        {
            data = TransferData.Create(r.CycleId);
            await _phaseRepo.AddTransferAsync(data, ct);
        }

        data.Update(r.TransferDate, r.ThawingDate, r.DayOfTransfered, r.LabNote);

        // Auto-advance
        if (cycle.CurrentPhase < CyclePhase.EmbryoTransfer)
        {
            cycle.AdvancePhase(CyclePhase.EmbryoTransfer);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result<TransferDataDto>.Success(TransferDataDto.FromEntity(data));
    }
}

// ==================== UPDATE LUTEAL PHASE DATA ====================
public record LutealPhaseDrugInput(string DrugName, string Category);

public record UpdateLutealPhaseDataCommand(
    Guid CycleId,
    List<LutealPhaseDrugInput>? Drugs
) : IRequest<Result<LutealPhaseDataDto>>;

public class UpdateLutealPhaseDataHandler : IRequestHandler<UpdateLutealPhaseDataCommand, Result<LutealPhaseDataDto>>
{
    private readonly ICyclePhaseDataRepository _phaseRepo;
    private readonly ITreatmentCycleRepository _cycleRepo;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateLutealPhaseDataHandler(
        ICyclePhaseDataRepository phaseRepo,
        ITreatmentCycleRepository cycleRepo,
        IUnitOfWork unitOfWork)
    {
        _phaseRepo = phaseRepo;
        _cycleRepo = cycleRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<LutealPhaseDataDto>> Handle(UpdateLutealPhaseDataCommand r, CancellationToken ct)
    {
        var cycle = await _cycleRepo.GetByIdAsync(r.CycleId, ct);
        if (cycle == null)
            return Result<LutealPhaseDataDto>.Failure("Cycle not found");

        var data = await _phaseRepo.GetLutealPhaseByCycleIdAsync(r.CycleId, ct);
        if (data == null)
        {
            data = LutealPhaseData.Create(r.CycleId);
            await _phaseRepo.AddLutealPhaseAsync(data, ct);
        }

        data.Update();

        // Update normalized drug collection
        if (r.Drugs != null)
        {
            var drugEntities = r.Drugs.Select((d, i) => LutealPhaseDrug.Create(data.Id, i + 1, d.DrugName, d.Category));
            data.SetDrugs(drugEntities);
        }

        // Auto-advance
        if (cycle.CurrentPhase < CyclePhase.LutealSupport)
        {
            cycle.AdvancePhase(CyclePhase.LutealSupport);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result<LutealPhaseDataDto>.Success(LutealPhaseDataDto.FromEntity(data));
    }
}

// ==================== UPDATE PREGNANCY DATA ====================
public record UpdatePregnancyDataCommand(
    Guid CycleId,
    decimal? BetaHcg,
    DateTime? BetaHcgDate,
    bool IsPregnant,
    int? GestationalSacs,
    int? FetalHeartbeats,
    DateTime? DueDate,
    string? Notes
) : IRequest<Result<PregnancyDataDto>>;

public class UpdatePregnancyDataHandler : IRequestHandler<UpdatePregnancyDataCommand, Result<PregnancyDataDto>>
{
    private readonly ICyclePhaseDataRepository _phaseRepo;
    private readonly ITreatmentCycleRepository _cycleRepo;
    private readonly IUnitOfWork _unitOfWork;

    public UpdatePregnancyDataHandler(
        ICyclePhaseDataRepository phaseRepo,
        ITreatmentCycleRepository cycleRepo,
        IUnitOfWork unitOfWork)
    {
        _phaseRepo = phaseRepo;
        _cycleRepo = cycleRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PregnancyDataDto>> Handle(UpdatePregnancyDataCommand r, CancellationToken ct)
    {
        var cycle = await _cycleRepo.GetByIdAsync(r.CycleId, ct);
        if (cycle == null)
            return Result<PregnancyDataDto>.Failure("Cycle not found");

        var data = await _phaseRepo.GetPregnancyByCycleIdAsync(r.CycleId, ct);
        if (data == null)
        {
            data = PregnancyData.Create(r.CycleId);
            await _phaseRepo.AddPregnancyAsync(data, ct);
        }

        data.Update(r.BetaHcg, r.BetaHcgDate, r.IsPregnant, r.GestationalSacs, r.FetalHeartbeats, r.DueDate, r.Notes);

        // Auto-advance
        if (cycle.CurrentPhase < CyclePhase.PregnancyTest)
        {
            cycle.AdvancePhase(CyclePhase.PregnancyTest);
        }

        return Result<PregnancyDataDto>.Success(PregnancyDataDto.FromEntity(data));
    }
}

// ==================== UPDATE BIRTH DATA ====================
public record BirthOutcomeInput(string Gender, decimal? Weight, bool IsLiveBirth = true);

public record UpdateBirthDataCommand(
    Guid CycleId,
    DateTime? DeliveryDate,
    int GestationalWeeks,
    string? DeliveryMethod,
    int LiveBirths,
    int Stillbirths,
    List<BirthOutcomeInput>? Outcomes,
    string? Complications
) : IRequest<Result<BirthDataDto>>;

public class UpdateBirthDataHandler : IRequestHandler<UpdateBirthDataCommand, Result<BirthDataDto>>
{
    private readonly ICyclePhaseDataRepository _phaseRepo;
    private readonly ITreatmentCycleRepository _cycleRepo;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateBirthDataHandler(
        ICyclePhaseDataRepository phaseRepo,
        ITreatmentCycleRepository cycleRepo,
        IUnitOfWork unitOfWork)
    {
        _phaseRepo = phaseRepo;
        _cycleRepo = cycleRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<BirthDataDto>> Handle(UpdateBirthDataCommand r, CancellationToken ct)
    {
        var cycle = await _cycleRepo.GetByIdAsync(r.CycleId, ct);
        if (cycle == null)
            return Result<BirthDataDto>.Failure("Cycle not found");

        var data = await _phaseRepo.GetBirthByCycleIdAsync(r.CycleId, ct);
        if (data == null)
        {
            data = BirthData.Create(r.CycleId);
            await _phaseRepo.AddBirthAsync(data, ct);
        }

        data.Update(r.DeliveryDate, r.GestationalWeeks, r.DeliveryMethod, r.LiveBirths, r.Stillbirths, r.Complications);

        // Update normalized outcomes collection
        if (r.Outcomes != null)
        {
            var outcomeEntities = r.Outcomes.Select((o, i) => BirthOutcome.Create(data.Id, i + 1, o.Gender, o.Weight, o.IsLiveBirth));
            data.SetOutcomes(outcomeEntities);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result<BirthDataDto>.Success(BirthDataDto.FromEntity(data));
    }
}

// ==================== CREATE ADVERSE EVENT ====================
public record CreateAdverseEventCommand(
    Guid CycleId,
    DateTime? EventDate,
    string? EventType,
    string? Severity,
    string? Description,
    string? Treatment,
    string? Outcome
) : IRequest<Result<AdverseEventDataDto>>;

public class CreateAdverseEventHandler : IRequestHandler<CreateAdverseEventCommand, Result<AdverseEventDataDto>>
{
    private readonly ICyclePhaseDataRepository _phaseRepo;
    private readonly ITreatmentCycleRepository _cycleRepo;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAdverseEventHandler(
        ICyclePhaseDataRepository phaseRepo,
        ITreatmentCycleRepository cycleRepo,
        IUnitOfWork unitOfWork)
    {
        _phaseRepo = phaseRepo;
        _cycleRepo = cycleRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AdverseEventDataDto>> Handle(CreateAdverseEventCommand r, CancellationToken ct)
    {
        var cycle = await _cycleRepo.GetByIdAsync(r.CycleId, ct);
        if (cycle == null)
            return Result<AdverseEventDataDto>.Failure("Cycle not found");

        var data = AdverseEventData.Create(r.CycleId, r.EventDate, r.EventType, r.Severity, r.Description, r.Treatment, r.Outcome);
        await _phaseRepo.AddAdverseEventAsync(data, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<AdverseEventDataDto>.Success(AdverseEventDataDto.FromEntity(data));
    }
}

// ==================== DTOs ====================
public record TreatmentIndicationDto(
    Guid Id, Guid CycleId, DateTime? LastMenstruation, string? TreatmentType, string? Regimen,
    bool FreezeAll, bool Sis, string? WifeDiagnosis, string? WifeDiagnosis2,
    string? HusbandDiagnosis, string? HusbandDiagnosis2,
    bool Timelapse, bool PgtA, bool PgtSr, bool PgtM,
    string? SubType, string? Source, string? ProcedurePlace
)
{
    public static TreatmentIndicationDto FromEntity(TreatmentIndication e) => new(
        e.Id, e.CycleId, e.LastMenstruation, e.TreatmentType, e.Regimen,
        e.FreezeAll, e.Sis, e.WifeDiagnosis, e.WifeDiagnosis2,
        e.HusbandDiagnosis, e.HusbandDiagnosis2,
        e.Timelapse, e.PgtA, e.PgtSr, e.PgtM,
        e.SubType, e.Source, e.ProcedurePlace);
}

public record StimulationDrugDto(string DrugName, int Duration, string? Posology, int SortOrder);

public record StimulationDataDto(
    Guid Id, Guid CycleId, DateTime? LastMenstruation, DateTime? StartDate, int? StartDay,
    List<StimulationDrugDto> Drugs,
    int? Size12Follicle, int? Size14Follicle, decimal? EndometriumThickness,
    string? TriggerDrug, DateTime? HcgDate, TimeSpan? HcgTime,
    DateTime? AspirationDate, int? AspirationNo
)
{
    public static StimulationDataDto FromEntity(StimulationData e) => new(
        e.Id, e.CycleId, e.LastMenstruation, e.StartDate, e.StartDay,
        e.Drugs.OrderBy(d => d.SortOrder).Select(d => new StimulationDrugDto(d.DrugName, d.Duration, d.Posology, d.SortOrder)).ToList(),
        e.Size12Follicle, e.Size14Follicle, e.EndometriumThickness,
        e.TriggerDrug, e.HcgDate, e.HcgTime,
        e.AspirationDate, e.AspirationNo);
}

public record CultureDataDto(
    Guid Id, Guid CycleId, int TotalFreezedEmbryo, int TotalThawedEmbryo,
    int TotalTransferedEmbryo, int RemainFreezedEmbryo
)
{
    public static CultureDataDto FromEntity(CultureData e) => new(
        e.Id, e.CycleId, e.TotalFreezedEmbryo, e.TotalThawedEmbryo,
        e.TotalTransferedEmbryo, e.RemainFreezedEmbryo);
}

public record TransferDataDto(
    Guid Id, Guid CycleId, DateTime? TransferDate, DateTime? ThawingDate,
    int DayOfTransfered, string? LabNote
)
{
    public static TransferDataDto FromEntity(TransferData e) => new(
        e.Id, e.CycleId, e.TransferDate, e.ThawingDate, e.DayOfTransfered, e.LabNote);
}

public record LutealPhaseDrugDto(string DrugName, string Category, int SortOrder);

public record LutealPhaseDataDto(
    Guid Id, Guid CycleId, List<LutealPhaseDrugDto> Drugs
)
{
    public static LutealPhaseDataDto FromEntity(LutealPhaseData e) => new(
        e.Id, e.CycleId,
        e.Drugs.OrderBy(d => d.SortOrder).Select(d => new LutealPhaseDrugDto(d.DrugName, d.Category, d.SortOrder)).ToList());
}

public record PregnancyDataDto(
    Guid Id, Guid CycleId, decimal? BetaHcg, DateTime? BetaHcgDate, bool IsPregnant,
    int? GestationalSacs, int? FetalHeartbeats, DateTime? DueDate, string? Notes
)
{
    public static PregnancyDataDto FromEntity(PregnancyData e) => new(
        e.Id, e.CycleId, e.BetaHcg, e.BetaHcgDate, e.IsPregnant,
        e.GestationalSacs, e.FetalHeartbeats, e.DueDate, e.Notes);
}

public record BirthOutcomeDto(string Gender, decimal? Weight, bool IsLiveBirth, int SortOrder);

public record BirthDataDto(
    Guid Id, Guid CycleId, DateTime? DeliveryDate, int GestationalWeeks, string? DeliveryMethod,
    int LiveBirths, int Stillbirths, List<BirthOutcomeDto> Outcomes, string? Complications
)
{
    public static BirthDataDto FromEntity(BirthData e) => new(
        e.Id, e.CycleId, e.DeliveryDate, e.GestationalWeeks, e.DeliveryMethod,
        e.LiveBirths, e.Stillbirths,
        e.Outcomes.OrderBy(o => o.SortOrder).Select(o => new BirthOutcomeDto(o.Gender, o.Weight, o.IsLiveBirth, o.SortOrder)).ToList(),
        e.Complications);
}

public record AdverseEventDataDto(
    Guid Id, Guid CycleId, DateTime? EventDate, string? EventType, string? Severity,
    string? Description, string? Treatment, string? Outcome
)
{
    public static AdverseEventDataDto FromEntity(AdverseEventData e) => new(
        e.Id, e.CycleId, e.EventDate, e.EventType, e.Severity, e.Description, e.Treatment, e.Outcome);
}
