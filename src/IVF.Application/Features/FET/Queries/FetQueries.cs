using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Application.Features.FET.Commands;
using MediatR;

namespace IVF.Application.Features.FET.Queries;

// === Get by ID ===

public record GetFetProtocolByIdQuery(Guid Id) : IRequest<Result<FetProtocolDto>>;

public class GetFetProtocolByIdHandler(IFetProtocolRepository repo)
    : IRequestHandler<GetFetProtocolByIdQuery, Result<FetProtocolDto>>
{
    public async Task<Result<FetProtocolDto>> Handle(GetFetProtocolByIdQuery request, CancellationToken ct)
    {
        var protocol = await repo.GetByIdAsync(request.Id, ct);
        if (protocol is null) return Result<FetProtocolDto>.Failure("Không tìm thấy FET protocol");
        return Result<FetProtocolDto>.Success(FetProtocolMapper.MapToDto(protocol));
    }
}

// === Get by Cycle ===

public record GetFetProtocolByCycleQuery(Guid CycleId) : IRequest<Result<FetProtocolDto>>;

public class GetFetProtocolByCycleHandler(IFetProtocolRepository repo)
    : IRequestHandler<GetFetProtocolByCycleQuery, Result<FetProtocolDto>>
{
    public async Task<Result<FetProtocolDto>> Handle(GetFetProtocolByCycleQuery request, CancellationToken ct)
    {
        var protocol = await repo.GetByCycleIdAsync(request.CycleId, ct);
        if (protocol is null) return Result<FetProtocolDto>.Failure("Chu kỳ chưa có FET protocol");
        return Result<FetProtocolDto>.Success(FetProtocolMapper.MapToDto(protocol));
    }
}

// === Search ===

public record SearchFetProtocolsQuery(
    string? Query = null,
    string? Status = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<FetProtocolDto>>>;

public class SearchFetProtocolsHandler(IFetProtocolRepository repo)
    : IRequestHandler<SearchFetProtocolsQuery, Result<PagedResult<FetProtocolDto>>>
{
    public async Task<Result<PagedResult<FetProtocolDto>>> Handle(SearchFetProtocolsQuery request, CancellationToken ct)
    {
        var items = await repo.SearchAsync(request.Query, request.Status, request.Page, request.PageSize, ct);
        var total = await repo.CountAsync(request.Query, request.Status, ct);
        var dtos = items.Select(FetProtocolMapper.MapToDto).ToList();
        return Result<PagedResult<FetProtocolDto>>.Success(new PagedResult<FetProtocolDto>(dtos, total, request.Page, request.PageSize));
    }
}
