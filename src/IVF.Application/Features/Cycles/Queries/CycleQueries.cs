using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Cycles.Commands;
using MediatR;

namespace IVF.Application.Features.Cycles.Queries;

// ==================== GET CYCLE BY ID ====================
public record GetCycleByIdQuery(Guid Id) : IRequest<Result<CycleDetailDto>>;

public class GetCycleByIdHandler : IRequestHandler<GetCycleByIdQuery, Result<CycleDetailDto>>
{
    private readonly ITreatmentCycleRepository _cycleRepo;

    public GetCycleByIdHandler(ITreatmentCycleRepository cycleRepo)
    {
        _cycleRepo = cycleRepo;
    }

    public async Task<Result<CycleDetailDto>> Handle(GetCycleByIdQuery request, CancellationToken ct)
    {
        var cycle = await _cycleRepo.GetByIdWithDetailsAsync(request.Id, ct);
        if (cycle == null)
            return Result<CycleDetailDto>.Failure("Cycle not found");

        return Result<CycleDetailDto>.Success(CycleDetailDto.FromEntity(cycle));
    }
}

// ==================== GET CYCLES BY COUPLE ====================
public record GetCyclesByCoupleQuery(Guid CoupleId) : IRequest<IReadOnlyList<CycleDto>>;

public class GetCyclesByCoupleHandler : IRequestHandler<GetCyclesByCoupleQuery, IReadOnlyList<CycleDto>>
{
    private readonly ITreatmentCycleRepository _cycleRepo;

    public GetCyclesByCoupleHandler(ITreatmentCycleRepository cycleRepo)
    {
        _cycleRepo = cycleRepo;
    }

    public async Task<IReadOnlyList<CycleDto>> Handle(GetCyclesByCoupleQuery request, CancellationToken ct)
    {
        var cycles = await _cycleRepo.GetByCoupleIdAsync(request.CoupleId, ct);
        return cycles.Select(CycleDto.FromEntity).ToList();
    }
}

// ==================== GET ACTIVE CYCLES ====================
public record GetActiveCyclesQuery() : IRequest<IReadOnlyList<CycleDto>>;

public class GetActiveCyclesHandler : IRequestHandler<GetActiveCyclesQuery, IReadOnlyList<CycleDto>>
{
    private readonly ITreatmentCycleRepository _cycleRepo;

    public GetActiveCyclesHandler(ITreatmentCycleRepository cycleRepo)
    {
        _cycleRepo = cycleRepo;
    }

    public async Task<IReadOnlyList<CycleDto>> Handle(GetActiveCyclesQuery request, CancellationToken ct)
    {
        var cycles = await _cycleRepo.GetActiveCyclesAsync(ct);
        return cycles.Select(CycleDto.FromEntity).ToList();
    }
}

// ==================== SEARCH CYCLES ====================
public record SearchCyclesQuery(string Query, Guid? PatientId = null) : IRequest<IReadOnlyList<CycleDto>>;

public class SearchCyclesHandler : IRequestHandler<SearchCyclesQuery, IReadOnlyList<CycleDto>>
{
    private readonly ITreatmentCycleRepository _cycleRepo;


    public SearchCyclesHandler(ITreatmentCycleRepository cycleRepo)
    {
        _cycleRepo = cycleRepo;
    }

    public async Task<IReadOnlyList<CycleDto>> Handle(SearchCyclesQuery request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query) && request.PatientId == null)
        {
             // If no query and no patient filter, return empty
             return new List<CycleDto>();
        }

        var cycles = await _cycleRepo.SearchAsync(request.Query, request.PatientId, ct);
        return cycles.Select(CycleDto.FromEntity).ToList();
    }
}

// ==================== DETAIL DTO ====================
public record CycleDetailDto(
    Guid Id,
    string CycleCode,
    Guid CoupleId,
    string WifeName,
    string HusbandName,
    string Method,
    string CurrentPhase,
    DateTime StartDate,
    DateTime? EndDate,
    string Outcome,
    int UltrasoundCount,
    int EmbryoCount,
    DateTime CreatedAt
)
{
    public static CycleDetailDto FromEntity(Domain.Entities.TreatmentCycle c) => new(
        c.Id, c.CycleCode, c.CoupleId,
        c.Couple?.Wife?.FullName ?? "",
        c.Couple?.Husband?.FullName ?? "",
        c.Method.ToString(), c.CurrentPhase.ToString(),
        c.StartDate, c.EndDate, c.Outcome.ToString(),
        c.Ultrasounds?.Count ?? 0,
        c.Embryos?.Count ?? 0,
        c.CreatedAt
    );
}
