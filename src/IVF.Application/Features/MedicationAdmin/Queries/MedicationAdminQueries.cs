using IVF.Application.Common.Interfaces;
using IVF.Application.Features.MedicationAdmin.Commands;
using MediatR;

namespace IVF.Application.Features.MedicationAdmin.Queries;

// ==================== GET BY ID ====================
public record GetMedicationAdminByIdQuery(Guid Id) : IRequest<MedicationAdminDto?>;

public class GetMedicationAdminByIdHandler : IRequestHandler<GetMedicationAdminByIdQuery, MedicationAdminDto?>
{
    private readonly IMedicationAdministrationRepository _repo;
    public GetMedicationAdminByIdHandler(IMedicationAdministrationRepository repo) => _repo = repo;

    public async Task<MedicationAdminDto?> Handle(GetMedicationAdminByIdQuery r, CancellationToken ct)
    {
        var med = await _repo.GetByIdAsync(r.Id, ct);
        return med == null ? null : MedicationAdminDto.FromEntity(med);
    }
}

// ==================== GET BY CYCLE ====================
public record GetMedicationsByCycleQuery(Guid CycleId) : IRequest<IReadOnlyList<MedicationAdminDto>>;

public class GetMedicationsByCycleHandler : IRequestHandler<GetMedicationsByCycleQuery, IReadOnlyList<MedicationAdminDto>>
{
    private readonly IMedicationAdministrationRepository _repo;
    public GetMedicationsByCycleHandler(IMedicationAdministrationRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<MedicationAdminDto>> Handle(GetMedicationsByCycleQuery r, CancellationToken ct)
    {
        var items = await _repo.GetByCycleIdAsync(r.CycleId, ct);
        return items.Select(MedicationAdminDto.FromEntity).ToList();
    }
}

// ==================== GET TRIGGER SHOTS ====================
public record GetTriggerShotsByCycleQuery(Guid CycleId) : IRequest<IReadOnlyList<MedicationAdminDto>>;

public class GetTriggerShotsByCycleHandler : IRequestHandler<GetTriggerShotsByCycleQuery, IReadOnlyList<MedicationAdminDto>>
{
    private readonly IMedicationAdministrationRepository _repo;
    public GetTriggerShotsByCycleHandler(IMedicationAdministrationRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<MedicationAdminDto>> Handle(GetTriggerShotsByCycleQuery r, CancellationToken ct)
    {
        var items = await _repo.GetTriggerShotsByCycleAsync(r.CycleId, ct);
        return items.Select(MedicationAdminDto.FromEntity).ToList();
    }
}

// ==================== SEARCH ====================
public record SearchMedicationAdminsQuery(string? Query, Guid? CycleId, DateTime? FromDate, DateTime? ToDate, int Page = 1, int PageSize = 20)
    : IRequest<(IReadOnlyList<MedicationAdminDto> Items, int Total)>;

public class SearchMedicationAdminsHandler : IRequestHandler<SearchMedicationAdminsQuery, (IReadOnlyList<MedicationAdminDto> Items, int Total)>
{
    private readonly IMedicationAdministrationRepository _repo;
    public SearchMedicationAdminsHandler(IMedicationAdministrationRepository repo) => _repo = repo;

    public async Task<(IReadOnlyList<MedicationAdminDto> Items, int Total)> Handle(SearchMedicationAdminsQuery r, CancellationToken ct)
    {
        var (items, total) = await _repo.SearchAsync(r.Query, r.CycleId, r.FromDate, r.ToDate, r.Page, r.PageSize, ct);
        return (items.Select(MedicationAdminDto.FromEntity).ToList(), total);
    }
}
