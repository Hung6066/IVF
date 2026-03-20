using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Application.Features.PrescriptionTemplates.Commands;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.PrescriptionTemplates.Queries;

public record GetPrescriptionTemplateByIdQuery(Guid Id) : IRequest<PrescriptionTemplateDto?>;

public class GetPrescriptionTemplateByIdHandler : IRequestHandler<GetPrescriptionTemplateByIdQuery, PrescriptionTemplateDto?>
{
    private readonly IPrescriptionTemplateRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public GetPrescriptionTemplateByIdHandler(IPrescriptionTemplateRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<PrescriptionTemplateDto?> Handle(GetPrescriptionTemplateByIdQuery req, CancellationToken ct)
    {
        var t = await _repo.GetByIdAsync(req.Id, _currentUser.TenantId ?? Guid.Empty, ct);
        return t is null ? null : PrescriptionTemplateDto.FromEntity(t);
    }
}

public record GetTemplatesByDoctorQuery(Guid DoctorId) : IRequest<IReadOnlyList<PrescriptionTemplateDto>>;

public class GetTemplatesByDoctorHandler : IRequestHandler<GetTemplatesByDoctorQuery, IReadOnlyList<PrescriptionTemplateDto>>
{
    private readonly IPrescriptionTemplateRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public GetTemplatesByDoctorHandler(IPrescriptionTemplateRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<PrescriptionTemplateDto>> Handle(GetTemplatesByDoctorQuery req, CancellationToken ct)
    {
        var items = await _repo.GetByDoctorAsync(req.DoctorId, _currentUser.TenantId ?? Guid.Empty, ct);
        return items.Select(PrescriptionTemplateDto.FromEntity).ToList().AsReadOnly();
    }
}

public record GetTemplatesByCycleTypeQuery(PrescriptionCycleType CycleType) : IRequest<IReadOnlyList<PrescriptionTemplateDto>>;

public class GetTemplatesByCycleTypeHandler : IRequestHandler<GetTemplatesByCycleTypeQuery, IReadOnlyList<PrescriptionTemplateDto>>
{
    private readonly IPrescriptionTemplateRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public GetTemplatesByCycleTypeHandler(IPrescriptionTemplateRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<PrescriptionTemplateDto>> Handle(GetTemplatesByCycleTypeQuery req, CancellationToken ct)
    {
        var items = await _repo.GetByCycleTypeAsync(req.CycleType, _currentUser.TenantId ?? Guid.Empty, ct);
        return items.Select(PrescriptionTemplateDto.FromEntity).ToList().AsReadOnly();
    }
}

public record SearchPrescriptionTemplatesQuery(string? Query, PrescriptionCycleType? CycleType, bool? IsActive, int Page = 1, int PageSize = 20) : IRequest<PagedResult<PrescriptionTemplateDto>>;

public class SearchPrescriptionTemplatesHandler : IRequestHandler<SearchPrescriptionTemplatesQuery, PagedResult<PrescriptionTemplateDto>>
{
    private readonly IPrescriptionTemplateRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public SearchPrescriptionTemplatesHandler(IPrescriptionTemplateRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<PrescriptionTemplateDto>> Handle(SearchPrescriptionTemplatesQuery req, CancellationToken ct)
    {
        var (items, total) = await _repo.SearchAsync(req.Query, req.CycleType, req.IsActive, req.Page, req.PageSize, _currentUser.TenantId ?? Guid.Empty, ct);
        return new PagedResult<PrescriptionTemplateDto>(items.Select(PrescriptionTemplateDto.FromEntity).ToList(), total, req.Page, req.PageSize);
    }
}
