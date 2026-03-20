using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Consent.Commands;
using MediatR;

namespace IVF.Application.Features.Consent.Queries;

// ==================== GET BY ID ====================
public record GetConsentByIdQuery(Guid Id) : IRequest<ConsentFormDto?>;

public class GetConsentByIdHandler : IRequestHandler<GetConsentByIdQuery, ConsentFormDto?>
{
    private readonly IConsentFormRepository _repo;
    public GetConsentByIdHandler(IConsentFormRepository repo) => _repo = repo;

    public async Task<ConsentFormDto?> Handle(GetConsentByIdQuery r, CancellationToken ct)
    {
        var consent = await _repo.GetByIdAsync(r.Id, ct);
        return consent == null ? null : ConsentFormDto.FromEntity(consent);
    }
}

// ==================== GET BY PATIENT ====================
public record GetConsentsByPatientQuery(Guid PatientId) : IRequest<IReadOnlyList<ConsentFormDto>>;

public class GetConsentsByPatientHandler : IRequestHandler<GetConsentsByPatientQuery, IReadOnlyList<ConsentFormDto>>
{
    private readonly IConsentFormRepository _repo;
    public GetConsentsByPatientHandler(IConsentFormRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<ConsentFormDto>> Handle(GetConsentsByPatientQuery r, CancellationToken ct)
    {
        var items = await _repo.GetByPatientIdAsync(r.PatientId, ct);
        return items.Select(ConsentFormDto.FromEntity).ToList();
    }
}

// ==================== GET BY CYCLE ====================
public record GetConsentsByCycleQuery(Guid CycleId) : IRequest<IReadOnlyList<ConsentFormDto>>;

public class GetConsentsByCycleHandler : IRequestHandler<GetConsentsByCycleQuery, IReadOnlyList<ConsentFormDto>>
{
    private readonly IConsentFormRepository _repo;
    public GetConsentsByCycleHandler(IConsentFormRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<ConsentFormDto>> Handle(GetConsentsByCycleQuery r, CancellationToken ct)
    {
        var items = await _repo.GetByCycleIdAsync(r.CycleId, ct);
        return items.Select(ConsentFormDto.FromEntity).ToList();
    }
}

// ==================== GET PENDING ====================
public record GetPendingConsentsQuery(Guid PatientId) : IRequest<IReadOnlyList<ConsentFormDto>>;

public class GetPendingConsentsHandler : IRequestHandler<GetPendingConsentsQuery, IReadOnlyList<ConsentFormDto>>
{
    private readonly IConsentFormRepository _repo;
    public GetPendingConsentsHandler(IConsentFormRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<ConsentFormDto>> Handle(GetPendingConsentsQuery r, CancellationToken ct)
    {
        var items = await _repo.GetPendingByPatientAsync(r.PatientId, ct);
        return items.Select(ConsentFormDto.FromEntity).ToList();
    }
}

// ==================== SEARCH ====================
public record SearchConsentsQuery(string? Query, string? Status, string? ConsentType, int Page = 1, int PageSize = 20)
    : IRequest<(IReadOnlyList<ConsentFormDto> Items, int Total)>;

public class SearchConsentsHandler : IRequestHandler<SearchConsentsQuery, (IReadOnlyList<ConsentFormDto> Items, int Total)>
{
    private readonly IConsentFormRepository _repo;
    public SearchConsentsHandler(IConsentFormRepository repo) => _repo = repo;

    public async Task<(IReadOnlyList<ConsentFormDto> Items, int Total)> Handle(SearchConsentsQuery r, CancellationToken ct)
    {
        var (items, total) = await _repo.SearchAsync(r.Query, r.Status, r.ConsentType, r.Page, r.PageSize, ct);
        return (items.Select(ConsentFormDto.FromEntity).ToList(), total);
    }
}
