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
