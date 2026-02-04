using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Embryos.Commands;
using MediatR;

namespace IVF.Application.Features.Embryos.Queries;

// ==================== GET EMBRYOS BY CYCLE ====================
public record GetEmbryosByCycleQuery(Guid CycleId) : IRequest<IReadOnlyList<EmbryoDto>>;

public class GetEmbryosByCycleHandler : IRequestHandler<GetEmbryosByCycleQuery, IReadOnlyList<EmbryoDto>>
{
    private readonly IEmbryoRepository _embryoRepo;

    public GetEmbryosByCycleHandler(IEmbryoRepository embryoRepo)
    {
        _embryoRepo = embryoRepo;
    }

    public async Task<IReadOnlyList<EmbryoDto>> Handle(GetEmbryosByCycleQuery request, CancellationToken ct)
    {
        var embryos = await _embryoRepo.GetByCycleIdAsync(request.CycleId, ct);
        return embryos.Select(EmbryoDto.FromEntity).ToList();
    }
}

// ==================== GET ACTIVE EMBRYOS ====================
public record GetActiveEmbryosQuery() : IRequest<IReadOnlyList<EmbryoActiveDto>>;

public class GetActiveEmbryosHandler : IRequestHandler<GetActiveEmbryosQuery, IReadOnlyList<EmbryoActiveDto>>
{
    private readonly IEmbryoRepository _repo;

    public GetActiveEmbryosHandler(IEmbryoRepository repo)
    {
        _repo = repo;
    }

    public async Task<IReadOnlyList<EmbryoActiveDto>> Handle(GetActiveEmbryosQuery request, CancellationToken ct)
    {
        var active = await _repo.GetActiveAsync(ct);
        return active.Select(e => new EmbryoActiveDto(
            e.Id,
            e.Cycle?.CycleCode ?? "",
            e.Cycle?.Couple?.Wife?.FullName ?? "",
            e.EmbryoNumber,
            e.Grade?.ToString() ?? "",
            e.Day.ToString(),
            e.Status.ToString()
        )).ToList();
    }
}

public record EmbryoActiveDto(Guid Id, string CycleCode, string PatientName, int Number, string Grade, string Day, string Status);

// ==================== GET CRYO STATS ====================
public record GetCryoStorageStatsQuery() : IRequest<IReadOnlyList<CryoStatsDto>>;

public class GetCryoStorageStatsHandler : IRequestHandler<GetCryoStorageStatsQuery, IReadOnlyList<CryoStatsDto>>
{
    private readonly ICryoLocationRepository _repo;

    public GetCryoStorageStatsHandler(ICryoLocationRepository repo)
    {
        _repo = repo;
    }

    public async Task<IReadOnlyList<CryoStatsDto>> Handle(GetCryoStorageStatsQuery request, CancellationToken ct)
    {
        return await _repo.GetStorageStatsAsync(ct);
    }
}
