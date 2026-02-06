using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
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

    public GetLabStatsHandler(ITreatmentCycleRepository cycleRepo, IEmbryoRepository embryoRepo)
    {
        _cycleRepo = cycleRepo;
        _embryoRepo = embryoRepo;
    }

    public async Task<Result<LabStatsDto>> Handle(GetLabStatsQuery request, CancellationToken ct)
    {
        var phaseCounts = await _cycleRepo.GetPhaseCountsAsync(ct);
        var activeEmbryos = await _embryoRepo.GetActiveAsync(ct);
        var frozenEmbryos = activeEmbryos.Count(e => e.Status == EmbryoStatus.Frozen);

        return Result<LabStatsDto>.Success(new LabStatsDto
        {
            EggRetrievalCount = phaseCounts.GetValueOrDefault(CyclePhase.EggRetrieval),
            CultureCount = phaseCounts.GetValueOrDefault(CyclePhase.EmbryoCulture),
            TransferCount = phaseCounts.GetValueOrDefault(CyclePhase.EmbryoTransfer),
            FreezeCount = 0, // CyclePhase.EmbryoFreezing not defined in domain
            TotalFrozenEmbryos = frozenEmbryos,
            TotalFrozenEggs = 0,
            TotalFrozenSperm = 0
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

    public GetLabScheduleHandler(ITreatmentCycleRepository cycleRepo)
    {
        _cycleRepo = cycleRepo;
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

        return Result<List<LabScheduleItemDto>>.Success(list);
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
            Used = s.Used
        }).ToList());
    }
}
