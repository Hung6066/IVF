using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Application.Features.InventoryRequests.Commands;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.InventoryRequests.Queries;

public record GetInventoryRequestByIdQuery(Guid Id) : IRequest<InventoryRequestDto?>;

public class GetInventoryRequestByIdHandler : IRequestHandler<GetInventoryRequestByIdQuery, InventoryRequestDto?>
{
    private readonly IInventoryRequestRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public GetInventoryRequestByIdHandler(IInventoryRequestRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<InventoryRequestDto?> Handle(GetInventoryRequestByIdQuery req, CancellationToken ct)
    {
        var request = await _repo.GetByIdAsync(req.Id, _currentUser.TenantId ?? Guid.Empty, ct);
        return request is null ? null : InventoryRequestDto.FromEntity(request);
    }
}

public record GetInventoryRequestsByStatusQuery(InventoryRequestStatus Status) : IRequest<IReadOnlyList<InventoryRequestDto>>;

public class GetInventoryRequestsByStatusHandler : IRequestHandler<GetInventoryRequestsByStatusQuery, IReadOnlyList<InventoryRequestDto>>
{
    private readonly IInventoryRequestRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public GetInventoryRequestsByStatusHandler(IInventoryRequestRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<InventoryRequestDto>> Handle(GetInventoryRequestsByStatusQuery req, CancellationToken ct)
    {
        var items = await _repo.GetByStatusAsync(req.Status, _currentUser.TenantId ?? Guid.Empty, ct);
        return items.Select(InventoryRequestDto.FromEntity).ToList().AsReadOnly();
    }
}

public record SearchInventoryRequestsQuery(string? Query, InventoryRequestStatus? Status, InventoryRequestType? Type, int Page = 1, int PageSize = 20) : IRequest<PagedResult<InventoryRequestDto>>;

public class SearchInventoryRequestsHandler : IRequestHandler<SearchInventoryRequestsQuery, PagedResult<InventoryRequestDto>>
{
    private readonly IInventoryRequestRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public SearchInventoryRequestsHandler(IInventoryRequestRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<InventoryRequestDto>> Handle(SearchInventoryRequestsQuery req, CancellationToken ct)
    {
        var (items, total) = await _repo.SearchAsync(req.Query, req.Status, req.Type, req.Page, req.PageSize, _currentUser.TenantId ?? Guid.Empty, ct);
        return new PagedResult<InventoryRequestDto>(items.Select(InventoryRequestDto.FromEntity).ToList(), total, req.Page, req.PageSize);
    }
}
