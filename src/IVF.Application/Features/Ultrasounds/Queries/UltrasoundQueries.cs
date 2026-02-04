using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Ultrasounds.Commands;
using MediatR;

namespace IVF.Application.Features.Ultrasounds.Queries;

// ==================== GET ULTRASOUNDS BY CYCLE ====================
public record GetUltrasoundsByCycleQuery(Guid CycleId) : IRequest<IReadOnlyList<UltrasoundDto>>;

public class GetUltrasoundsByCycleHandler : IRequestHandler<GetUltrasoundsByCycleQuery, IReadOnlyList<UltrasoundDto>>
{
    private readonly IUltrasoundRepository _usRepo;

    public GetUltrasoundsByCycleHandler(IUltrasoundRepository usRepo)
    {
        _usRepo = usRepo;
    }

    public async Task<IReadOnlyList<UltrasoundDto>> Handle(GetUltrasoundsByCycleQuery request, CancellationToken ct)
    {
        var ultrasounds = await _usRepo.GetByCycleIdAsync(request.CycleId, ct);
        return ultrasounds.Select(UltrasoundDto.FromEntity).ToList();
    }
}
