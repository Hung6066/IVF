using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Application.Features.DrugCatalog.Commands;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.DrugCatalog.Queries;

public record GetDrugByIdQuery(Guid Id) : IRequest<DrugCatalogDto?>;

public class GetDrugByIdHandler : IRequestHandler<GetDrugByIdQuery, DrugCatalogDto?>
{
    private readonly IDrugCatalogRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public GetDrugByIdHandler(IDrugCatalogRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<DrugCatalogDto?> Handle(GetDrugByIdQuery req, CancellationToken ct)
    {
        var drug = await _repo.GetByIdAsync(req.Id, _currentUser.TenantId ?? Guid.Empty, ct);
        return drug is null ? null : DrugCatalogDto.FromEntity(drug);
    }
}

public record GetDrugsByCategoryQuery(DrugCategory Category) : IRequest<IReadOnlyList<DrugCatalogDto>>;

public class GetDrugsByCategoryHandler : IRequestHandler<GetDrugsByCategoryQuery, IReadOnlyList<DrugCatalogDto>>
{
    private readonly IDrugCatalogRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public GetDrugsByCategoryHandler(IDrugCatalogRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<DrugCatalogDto>> Handle(GetDrugsByCategoryQuery req, CancellationToken ct)
    {
        var drugs = await _repo.GetByCategoryAsync(req.Category, _currentUser.TenantId ?? Guid.Empty, ct);
        return drugs.Select(DrugCatalogDto.FromEntity).ToList().AsReadOnly();
    }
}

public record GetActiveDrugsQuery : IRequest<IReadOnlyList<DrugCatalogDto>>;

public class GetActiveDrugsHandler : IRequestHandler<GetActiveDrugsQuery, IReadOnlyList<DrugCatalogDto>>
{
    private readonly IDrugCatalogRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public GetActiveDrugsHandler(IDrugCatalogRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<DrugCatalogDto>> Handle(GetActiveDrugsQuery req, CancellationToken ct)
    {
        var drugs = await _repo.GetActiveAsync(_currentUser.TenantId ?? Guid.Empty, ct);
        return drugs.Select(DrugCatalogDto.FromEntity).ToList().AsReadOnly();
    }
}

public record SearchDrugsQuery(string? Query, DrugCategory? Category, bool? IsActive, int Page = 1, int PageSize = 20) : IRequest<PagedResult<DrugCatalogDto>>;

public class SearchDrugsHandler : IRequestHandler<SearchDrugsQuery, PagedResult<DrugCatalogDto>>
{
    private readonly IDrugCatalogRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public SearchDrugsHandler(IDrugCatalogRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<DrugCatalogDto>> Handle(SearchDrugsQuery req, CancellationToken ct)
    {
        var (items, total) = await _repo.SearchAsync(req.Query, req.Category, req.IsActive, req.Page, req.PageSize, _currentUser.TenantId ?? Guid.Empty, ct);
        return new PagedResult<DrugCatalogDto>(items.Select(DrugCatalogDto.FromEntity).ToList(), total, req.Page, req.PageSize);
    }
}
