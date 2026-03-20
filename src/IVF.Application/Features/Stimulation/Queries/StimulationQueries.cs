using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Stimulation.Commands;
using MediatR;

using ICycleRepository = IVF.Application.Common.Interfaces.ITreatmentCycleRepository;

namespace IVF.Application.Features.Stimulation.Queries;

// === Get Stimulation Tracker ===

public record GetStimulationTrackerQuery(Guid CycleId) : IRequest<Result<StimulationTrackerDto>>;

public class GetStimulationTrackerHandler(
    ICyclePhaseDataRepository phaseRepo,
    ICycleRepository cycleRepo)
    : IRequestHandler<GetStimulationTrackerQuery, Result<StimulationTrackerDto>>
{
    public async Task<Result<StimulationTrackerDto>> Handle(GetStimulationTrackerQuery request, CancellationToken ct)
    {
        var cycle = await cycleRepo.GetByIdAsync(request.CycleId, ct);
        if (cycle is null) return Result<StimulationTrackerDto>.Failure("Không tìm thấy chu kỳ");

        var stim = await phaseRepo.GetStimulationByCycleIdAsync(request.CycleId, ct);
        if (stim is null)
            return Result<StimulationTrackerDto>.Success(new StimulationTrackerDto(
                request.CycleId, null, null, null,
                new List<FollicleScanDto>(), null, null,
                null, null, null, false, null, null, null, null, null));

        return Result<StimulationTrackerDto>.Success(new StimulationTrackerDto(
            stim.CycleId,
            stim.LastMenstruation,
            stim.StartDate,
            stim.StartDay,
            new List<FollicleScanDto>(),   // Ultrasound scans queried separately
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

// === Get Follicle Chart Data ===

public record FollicleChartPointDto(DateTime Date, int CycleDay, int? Size12, int? Size14, int? Total, decimal? Endometrium, decimal? E2);

public record GetFollicleChartQuery(Guid CycleId) : IRequest<Result<List<FollicleChartPointDto>>>;

public class GetFollicleChartHandler(ICyclePhaseDataRepository phaseRepo, ICycleRepository cycleRepo)
    : IRequestHandler<GetFollicleChartQuery, Result<List<FollicleChartPointDto>>>
{
    public async Task<Result<List<FollicleChartPointDto>>> Handle(GetFollicleChartQuery request, CancellationToken ct)
    {
        var cycle = await cycleRepo.GetByIdAsync(request.CycleId, ct);
        if (cycle is null) return Result<List<FollicleChartPointDto>>.Failure("Không tìm thấy chu kỳ");

        var stim = await phaseRepo.GetStimulationByCycleIdAsync(request.CycleId, ct);
        if (stim is null)
            return Result<List<FollicleChartPointDto>>.Success(new List<FollicleChartPointDto>());

        // Return single current data point (chart data comes from Ultrasound records)
        var points = new List<FollicleChartPointDto>();
        if (stim.StartDate.HasValue)
        {
            points.Add(new FollicleChartPointDto(
                stim.StartDate.Value,
                stim.StartDay ?? 1,
                stim.Size12Follicle,
                stim.Size14Follicle,
                null,
                stim.EndometriumThickness,
                stim.E2Lab));
        }
        return Result<List<FollicleChartPointDto>>.Success(points);
    }
}

// === Get Medication Schedule ===

public record MedicationScheduleItemDto(
    string DrugName,
    string? Posology,
    int Duration,
    int SortOrder);

public record GetMedicationScheduleQuery(Guid CycleId) : IRequest<Result<List<MedicationScheduleItemDto>>>;

public class GetMedicationScheduleHandler(ICyclePhaseDataRepository phaseRepo)
    : IRequestHandler<GetMedicationScheduleQuery, Result<List<MedicationScheduleItemDto>>>
{
    public async Task<Result<List<MedicationScheduleItemDto>>> Handle(GetMedicationScheduleQuery request, CancellationToken ct)
    {
        var stim = await phaseRepo.GetStimulationByCycleIdAsync(request.CycleId, ct);
        if (stim is null)
            return Result<List<MedicationScheduleItemDto>>.Success(new List<MedicationScheduleItemDto>());

        var items = stim.Drugs
            .OrderBy(d => d.SortOrder)
            .Select(d => new MedicationScheduleItemDto(d.DrugName, d.Posology, d.Duration, d.SortOrder))
            .ToList();

        return Result<List<MedicationScheduleItemDto>>.Success(items);
    }
}
