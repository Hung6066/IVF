using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Constants;
using IVF.Domain.Enums;
using MediatR;

namespace IVF.Application.Features.Lab.Queries;

// ==================== STATS ====================
public record GetLabStatsQuery : IRequest<Result<LabStatsDto>>;

public class LabStatsDto
{
    public int EggRetrievalCount { get; set; }
    public int CultureCount { get; set; }
    public int TransferCount { get; set; }
    public int FreezeCount { get; set; }
    public int TotalFrozenEmbryos { get; set; }
    public int TotalFrozenEggs { get; set; }
    public int TotalFrozenSperm { get; set; }
}

public class GetLabStatsHandler : IRequestHandler<GetLabStatsQuery, Result<LabStatsDto>>
{
    private readonly ITreatmentCycleRepository _cycleRepo;
    private readonly IEmbryoRepository _embryoRepo;
    private readonly ICryoLocationRepository _cryoRepo;

    public GetLabStatsHandler(ITreatmentCycleRepository cycleRepo, IEmbryoRepository embryoRepo, ICryoLocationRepository cryoRepo)
    {
        _cycleRepo = cycleRepo;
        _embryoRepo = embryoRepo;
        _cryoRepo = cryoRepo;
    }

    public async Task<Result<LabStatsDto>> Handle(GetLabStatsQuery request, CancellationToken ct)
    {
        var phaseCounts = await _cycleRepo.GetPhaseCountsAsync(ct);
        var activeEmbryos = await _embryoRepo.GetActiveAsync(ct);
        // We can use either EmbryoRepository or CryoLocationRepository for frozen embryos.
        // CryoLocationRepository is more generic for all types.
        var specimenCounts = await _cryoRepo.GetSpecimenCountsAsync(ct);

        // Get today's schedule to count egg retrievals
        var today = DateTime.UtcNow.Date;
        var todaySchedule = await _cycleRepo.GetLabScheduleAsync(today, ct);
        var eggRetrievalCount = todaySchedule.Count(s => s.Type == ScheduleTypes.Retrieval);

        // Count developing embryos (embryos being cultured)
        var cultureCount = activeEmbryos.Count(e => e.Status == Domain.Enums.EmbryoStatus.Developing);

        return Result<LabStatsDto>.Success(new LabStatsDto
        {
            EggRetrievalCount = eggRetrievalCount,
            CultureCount = cultureCount,
            TransferCount = phaseCounts.GetValueOrDefault(CyclePhase.EmbryoTransfer),
            FreezeCount = specimenCounts.GetValueOrDefault(SpecimenType.Embryo), // Fixed: was 0
            TotalFrozenEmbryos = specimenCounts.GetValueOrDefault(SpecimenType.Embryo),
            TotalFrozenEggs = specimenCounts.GetValueOrDefault(SpecimenType.Oocyte),
            TotalFrozenSperm = specimenCounts.GetValueOrDefault(SpecimenType.Sperm)
        });
    }
}

// ==================== SCHEDULE ====================
public record GetLabScheduleQuery(DateTime Date) : IRequest<Result<List<LabScheduleItemDto>>>;

public class LabScheduleItemDto
{
    public string Id { get; set; } = null!;
    public string Time { get; set; } = null!;
    public string PatientName { get; set; } = null!;
    public string CycleCode { get; set; } = null!;
    public string Procedure { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Status { get; set; } = null!;
}

public class GetLabScheduleHandler : IRequestHandler<GetLabScheduleQuery, Result<List<LabScheduleItemDto>>>
{
    private readonly ITreatmentCycleRepository _cycleRepo;
    private readonly IAppointmentRepository _appointmentRepo;

    public GetLabScheduleHandler(ITreatmentCycleRepository cycleRepo, IAppointmentRepository appointmentRepo)
    {
        _cycleRepo = cycleRepo;
        _appointmentRepo = appointmentRepo;
    }

    public async Task<Result<List<LabScheduleItemDto>>> Handle(GetLabScheduleQuery request, CancellationToken ct)
    {
        var schedule = await _cycleRepo.GetLabScheduleAsync(request.Date, ct);
        
        // Map Repository DTO to Public API DTO
        var list = schedule.Select(s => new LabScheduleItemDto
        {
            Id = s.Id.ToString(),
            Time = s.Time.ToString("HH:mm"),
            PatientName = s.PatientName,
            CycleCode = s.CycleCode,
            Procedure = s.Procedure,
            Type = s.Type,
            Status = s.Status
        }).ToList();

        // Fetch Appointments (Reports)
        // Get appointments for the date
        var date = DateTime.SpecifyKind(request.Date.Date, DateTimeKind.Utc);
        var appointments = await _appointmentRepo.GetByDateRangeAsync(date, date.AddDays(1), ct);

        // Filter for "Báo phôi" reports
        var reports = appointments
            .Where(a => a.Type == AppointmentType.Other && (a.Notes?.Contains(AppointmentNotes.EmbryoReport) == true))
            .Select(a => new LabScheduleItemDto
            {
                Id = a.Id.ToString(),
                Time = a.ScheduledAt.ToString("HH:mm"),
                PatientName = a.Patient?.FullName ?? "Unknown",
                CycleCode = a.Cycle?.CycleCode ?? "",
                Procedure = AppointmentNotes.EmbryoReport,
                Type = ScheduleTypes.Report,
                Status = a.Status == AppointmentStatus.Completed ? ScheduleStatuses.Done : ScheduleStatuses.Pending
            });

        list.AddRange(reports);

        return Result<List<LabScheduleItemDto>>.Success(list.OrderBy(x => x.Time).ToList());
    }
}

// ==================== CRYO ====================
public record GetCryoLocationsQuery : IRequest<Result<List<CryoLocationDto>>>;

public class CryoLocationDto
{
    public string Tank { get; set; } = null!;
    public int Canister { get; set; }
    public int Cane { get; set; }
    public int Goblet { get; set; }
    public int Available { get; set; }
    public int Used { get; set; }
    public int SpecimenType { get; set; }
}

public class GetCryoLocationsHandler : IRequestHandler<GetCryoLocationsQuery, Result<List<CryoLocationDto>>>
{
    private readonly ICryoLocationRepository _cryoRepo;

    public GetCryoLocationsHandler(ICryoLocationRepository cryoRepo)
    {
        _cryoRepo = cryoRepo;
    }

    public async Task<Result<List<CryoLocationDto>>> Handle(GetCryoLocationsQuery request, CancellationToken ct)
    {
        var stats = await _cryoRepo.GetStorageStatsAsync(ct);
        
        return Result<List<CryoLocationDto>>.Success(stats.Select(s => new CryoLocationDto
        {
            Tank = s.Tank,
            Canister = s.CanisterCount,
            Cane = s.CaneCount,
            Goblet = s.GobletCount,
            Available = s.Available,
            Used = s.Used,
            SpecimenType = s.SpecimenType
        }).ToList());
    }
}

// ==================== EMBRYO REPORT ====================
public record GetEmbryoReportQuery(DateTime Date) : IRequest<Result<List<EmbryoReportDto>>>;

public class EmbryoReportDto
{
    public string CycleCode { get; set; } = null!;
    public string PatientName { get; set; } = null!;
    public string AspirationDate { get; set; } = null!;
    public int TotalEggs { get; set; }
    public int MII { get; set; }
    public int TwoPN { get; set; }
    public int D3 { get; set; }
    public string D5D6 { get; set; } = "—";
    public int Transferred { get; set; }
    public int Frozen { get; set; }
    public string Status { get; set; } = null!;
}

public class GetEmbryoReportHandler : IRequestHandler<GetEmbryoReportQuery, Result<List<EmbryoReportDto>>>
{
    private readonly ITreatmentCycleRepository _cycleRepo;
    private readonly IEmbryoRepository _embryoRepo;

    public GetEmbryoReportHandler(ITreatmentCycleRepository cycleRepo, IEmbryoRepository embryoRepo)
    {
        _cycleRepo = cycleRepo;
        _embryoRepo = embryoRepo;
    }

    public async Task<Result<List<EmbryoReportDto>>> Handle(GetEmbryoReportQuery request, CancellationToken ct)
    {
        // Get all cycles with embryos
        var cycles = await _cycleRepo.GetAllWithDetailsAsync(ct);
        var result = new List<EmbryoReportDto>();

        // Define date range for filtering (show cycles from the entire month or all if no specific filtering needed)
        // For now, we'll show all cycles that have embryos, but you can filter by request.Date if needed
        
        foreach (var cycle in cycles)
        {
            var embryos = await _embryoRepo.GetByCycleIdAsync(cycle.Id, ct);
            if (!embryos.Any()) continue;

            var stimData = cycle.Stimulation;
            var aspirationDate = stimData?.AspirationDate;

            // Optional: Filter by date if you want to show only cycles from a specific period
            // Uncomment the following lines to filter by month:
            // if (aspirationDate.HasValue && aspirationDate.Value.Month != request.Date.Month)
            //     continue;

            // Calculate counts
            var mii = embryos.Count; // Initially all eggs from retrieval become embryos if fertilized
            var twoPN = embryos.Count(e => e.Day >= Domain.Enums.EmbryoDay.D1);
            var d3 = embryos.Count(e => e.Day >= Domain.Enums.EmbryoDay.D3);
            var d5d6 = embryos.Where(e => e.Day >= Domain.Enums.EmbryoDay.D5).ToList();
            var transferred = embryos.Count(e => e.Status == Domain.Enums.EmbryoStatus.Transferred);
            var frozen = embryos.Count(e => e.Status == Domain.Enums.EmbryoStatus.Frozen);

            // Determine status based on embryo days
            var maxDay = embryos.Any() ? embryos.Max(e => e.Day) : Domain.Enums.EmbryoDay.D1;
            string status = maxDay switch
            {
                Domain.Enums.EmbryoDay.D1 => "D1",
                Domain.Enums.EmbryoDay.D2 => "D2",
                Domain.Enums.EmbryoDay.D3 => "D3",
                Domain.Enums.EmbryoDay.D4 => "D4",
                Domain.Enums.EmbryoDay.D5 => "D5",
                Domain.Enums.EmbryoDay.D6 => "D6",
                _ => "D1"
            };

            // If all embryos transferred/frozen, mark as completed
            if (embryos.All(e => e.Status == Domain.Enums.EmbryoStatus.Transferred || e.Status == Domain.Enums.EmbryoStatus.Frozen))
            {
                status = "Hoàn thành";
            }

            // D5/D6 embryos with grades
            var d5d6Text = d5d6.Any() 
                ? $"{d5d6.Count} ({string.Join(", ", d5d6.Take(3).Select(e => e.Grade))}...)"
                : "—";

            result.Add(new EmbryoReportDto
            {
                CycleCode = cycle.CycleCode,
                PatientName = cycle.Couple?.Wife?.FullName ?? "Unknown",
                AspirationDate = aspirationDate?.ToString("dd/MM") ?? "—",
                TotalEggs = stimData?.AspirationNo ?? embryos.Count,
                MII = mii,
                TwoPN = twoPN,
                D3 = d3,
                D5D6 = d5d6Text,
                Transferred = transferred,
                Frozen = frozen,
                Status = status
            });
        }

        return Result<List<EmbryoReportDto>>.Success(result);
    }
}
