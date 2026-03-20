using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Application.Features.FileTracking.Commands;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.FileTracking.Queries;

public record GetFileTrackingByIdQuery(Guid Id) : IRequest<FileTrackingDto?>;

public class GetFileTrackingByIdHandler : IRequestHandler<GetFileTrackingByIdQuery, FileTrackingDto?>
{
    private readonly IFileTrackingRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public GetFileTrackingByIdHandler(IFileTrackingRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<FileTrackingDto?> Handle(GetFileTrackingByIdQuery req, CancellationToken ct)
    {
        var file = await _repo.GetByIdAsync(req.Id, _currentUser.TenantId ?? Guid.Empty, ct);
        return file is null ? null : FileTrackingDto.FromEntity(file);
    }
}

public record GetFilesByPatientQuery(Guid PatientId) : IRequest<IReadOnlyList<FileTrackingDto>>;

public class GetFilesByPatientHandler : IRequestHandler<GetFilesByPatientQuery, IReadOnlyList<FileTrackingDto>>
{
    private readonly IFileTrackingRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public GetFilesByPatientHandler(IFileTrackingRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<FileTrackingDto>> Handle(GetFilesByPatientQuery req, CancellationToken ct)
    {
        var files = await _repo.GetByPatientAsync(req.PatientId, _currentUser.TenantId ?? Guid.Empty, ct);
        return files.Select(FileTrackingDto.FromEntity).ToList().AsReadOnly();
    }
}

public record GetFilesByLocationQuery(string Location) : IRequest<IReadOnlyList<FileTrackingDto>>;

public class GetFilesByLocationHandler : IRequestHandler<GetFilesByLocationQuery, IReadOnlyList<FileTrackingDto>>
{
    private readonly IFileTrackingRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public GetFilesByLocationHandler(IFileTrackingRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<FileTrackingDto>> Handle(GetFilesByLocationQuery req, CancellationToken ct)
    {
        var files = await _repo.GetByLocationAsync(req.Location, _currentUser.TenantId ?? Guid.Empty, ct);
        return files.Select(FileTrackingDto.FromEntity).ToList().AsReadOnly();
    }
}

public record SearchFileTrackingQuery(string? Query, FileStatus? Status, string? Location, int Page = 1, int PageSize = 20) : IRequest<PagedResult<FileTrackingDto>>;

public class SearchFileTrackingHandler : IRequestHandler<SearchFileTrackingQuery, PagedResult<FileTrackingDto>>
{
    private readonly IFileTrackingRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public SearchFileTrackingHandler(IFileTrackingRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<FileTrackingDto>> Handle(SearchFileTrackingQuery req, CancellationToken ct)
    {
        var (items, total) = await _repo.SearchAsync(req.Query, req.Status, req.Location, req.Page, req.PageSize, _currentUser.TenantId ?? Guid.Empty, ct);
        return new PagedResult<FileTrackingDto>(items.Select(FileTrackingDto.FromEntity).ToList(), total, req.Page, req.PageSize);
    }
}
