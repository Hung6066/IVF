using IVF.Application.Common.Interfaces;
using IVF.Application.Features.CycleFees.Commands;
using MediatR;

namespace IVF.Application.Features.CycleFees.Queries;

public record GetFeesByCycleQuery(Guid CycleId) : IRequest<IReadOnlyList<CycleFeeDto>>;

public class GetFeesByCycleHandler : IRequestHandler<GetFeesByCycleQuery, IReadOnlyList<CycleFeeDto>>
{
    private readonly ICycleFeeRepository _repo;
    public GetFeesByCycleHandler(ICycleFeeRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<CycleFeeDto>> Handle(GetFeesByCycleQuery r, CancellationToken ct)
    {
        var items = await _repo.GetByCycleIdAsync(r.CycleId, ct);
        return items.Select(CycleFeeDto.FromEntity).ToList();
    }
}

public record CheckCycleFeeExistsQuery(Guid CycleId, string FeeType) : IRequest<bool>;

public class CheckCycleFeeExistsHandler : IRequestHandler<CheckCycleFeeExistsQuery, bool>
{
    private readonly ICycleFeeRepository _repo;
    public CheckCycleFeeExistsHandler(ICycleFeeRepository repo) => _repo = repo;

    public async Task<bool> Handle(CheckCycleFeeExistsQuery r, CancellationToken ct)
        => await _repo.HasFeeForTypeAsync(r.CycleId, r.FeeType, ct);
}
