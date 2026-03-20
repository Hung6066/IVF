using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Prescriptions.Commands;
using MediatR;

namespace IVF.Application.Features.Prescriptions.Queries;

// ==================== GET BY ID ====================
public record GetPrescriptionByIdQuery(Guid Id) : IRequest<PrescriptionDto?>;

public class GetPrescriptionByIdHandler : IRequestHandler<GetPrescriptionByIdQuery, PrescriptionDto?>
{
    private readonly IPrescriptionRepository _prescriptionRepo;
    public GetPrescriptionByIdHandler(IPrescriptionRepository prescriptionRepo) => _prescriptionRepo = prescriptionRepo;

    public async Task<PrescriptionDto?> Handle(GetPrescriptionByIdQuery request, CancellationToken ct)
    {
        var prescription = await _prescriptionRepo.GetByIdWithItemsAsync(request.Id, ct);
        return prescription == null ? null : PrescriptionDto.FromEntity(prescription);
    }
}

// ==================== GET BY PATIENT ====================
public record GetPrescriptionsByPatientQuery(Guid PatientId) : IRequest<IReadOnlyList<PrescriptionDto>>;

public class GetPrescriptionsByPatientHandler : IRequestHandler<GetPrescriptionsByPatientQuery, IReadOnlyList<PrescriptionDto>>
{
    private readonly IPrescriptionRepository _prescriptionRepo;
    public GetPrescriptionsByPatientHandler(IPrescriptionRepository prescriptionRepo) => _prescriptionRepo = prescriptionRepo;

    public async Task<IReadOnlyList<PrescriptionDto>> Handle(GetPrescriptionsByPatientQuery request, CancellationToken ct)
    {
        var prescriptions = await _prescriptionRepo.GetByPatientIdAsync(request.PatientId, ct);
        return prescriptions.Select(PrescriptionDto.FromEntity).ToList();
    }
}

// ==================== GET BY CYCLE ====================
public record GetPrescriptionsByCycleQuery(Guid CycleId) : IRequest<IReadOnlyList<PrescriptionDto>>;

public class GetPrescriptionsByCycleHandler : IRequestHandler<GetPrescriptionsByCycleQuery, IReadOnlyList<PrescriptionDto>>
{
    private readonly IPrescriptionRepository _prescriptionRepo;
    public GetPrescriptionsByCycleHandler(IPrescriptionRepository prescriptionRepo) => _prescriptionRepo = prescriptionRepo;

    public async Task<IReadOnlyList<PrescriptionDto>> Handle(GetPrescriptionsByCycleQuery request, CancellationToken ct)
    {
        var prescriptions = await _prescriptionRepo.GetByCycleIdAsync(request.CycleId, ct);
        return prescriptions.Select(PrescriptionDto.FromEntity).ToList();
    }
}

// ==================== SEARCH ====================
public record SearchPrescriptionsQuery(string? Query, DateTime? FromDate, DateTime? ToDate, string? Status, int Page = 1, int PageSize = 20)
    : IRequest<(IReadOnlyList<PrescriptionDto> Items, int Total)>;

public class SearchPrescriptionsHandler : IRequestHandler<SearchPrescriptionsQuery, (IReadOnlyList<PrescriptionDto> Items, int Total)>
{
    private readonly IPrescriptionRepository _prescriptionRepo;
    public SearchPrescriptionsHandler(IPrescriptionRepository prescriptionRepo) => _prescriptionRepo = prescriptionRepo;

    public async Task<(IReadOnlyList<PrescriptionDto> Items, int Total)> Handle(SearchPrescriptionsQuery request, CancellationToken ct)
    {
        var (items, total) = await _prescriptionRepo.SearchAsync(request.Query, request.FromDate, request.ToDate, request.Status, request.Page, request.PageSize, ct);
        var dtos = items.Select(PrescriptionDto.FromEntity).ToList();
        return (dtos, total);
    }
}

// ==================== STATISTICS ====================
public record PrescriptionStatisticsDto(int TodayCount, int PendingCount);

public record GetPrescriptionStatisticsQuery : IRequest<PrescriptionStatisticsDto>;

public class GetPrescriptionStatisticsHandler : IRequestHandler<GetPrescriptionStatisticsQuery, PrescriptionStatisticsDto>
{
    private readonly IPrescriptionRepository _prescriptionRepo;
    public GetPrescriptionStatisticsHandler(IPrescriptionRepository prescriptionRepo) => _prescriptionRepo = prescriptionRepo;

    public async Task<PrescriptionStatisticsDto> Handle(GetPrescriptionStatisticsQuery request, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var todayCount = await _prescriptionRepo.GetCountByDateAsync(today, ct);
        var pendingCount = await _prescriptionRepo.GetPendingCountAsync(ct);
        return new PrescriptionStatisticsDto(todayCount, pendingCount);
    }
}
