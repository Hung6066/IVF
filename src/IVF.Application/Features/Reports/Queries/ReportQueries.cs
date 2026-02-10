using IVF.Application.Common.Interfaces;
using IVF.Domain.Enums;
using MediatR;

namespace IVF.Application.Features.Reports.Queries;

// ==================== DASHBOARD STATS ====================
public record GetDashboardStatsQuery() : IRequest<DashboardStatsDto>;

public class GetDashboardStatsHandler : IRequestHandler<GetDashboardStatsQuery, DashboardStatsDto>
{
    private readonly IPatientRepository _patientRepo;
    private readonly ITreatmentCycleRepository _cycleRepo;
    private readonly IInvoiceRepository _invoiceRepo;
    private readonly IQueueTicketRepository _queueRepo;

    public GetDashboardStatsHandler(
        IPatientRepository patientRepo,
        ITreatmentCycleRepository cycleRepo,
        IInvoiceRepository invoiceRepo,
        IQueueTicketRepository queueRepo)
    {
        _patientRepo = patientRepo;
        _cycleRepo = cycleRepo;
        _invoiceRepo = invoiceRepo;
        _queueRepo = queueRepo;
    }

    public async Task<DashboardStatsDto> Handle(GetDashboardStatsQuery request, CancellationToken ct)
    {
        var totalPatients = await _patientRepo.GetTotalCountAsync(ct);
        var activeCycles = await _cycleRepo.GetActiveCountAsync(ct);
        var todayQueue = await _queueRepo.GetTodayCountAsync(ct);
        var monthlyRevenue = await _invoiceRepo.GetMonthlyRevenueAsync(DateTime.UtcNow.Month, DateTime.UtcNow.Year, ct);

        return new DashboardStatsDto(totalPatients, activeCycles, todayQueue, monthlyRevenue);
    }
}

// ==================== CYCLE SUCCESS RATES ====================
public record GetCycleSuccessRatesQuery(int? Year) : IRequest<CycleSuccessRatesDto>;

public class GetCycleSuccessRatesHandler : IRequestHandler<GetCycleSuccessRatesQuery, CycleSuccessRatesDto>
{
    private readonly ITreatmentCycleRepository _cycleRepo;

    public GetCycleSuccessRatesHandler(ITreatmentCycleRepository cycleRepo) => _cycleRepo = cycleRepo;

    public async Task<CycleSuccessRatesDto> Handle(GetCycleSuccessRatesQuery request, CancellationToken ct)
    {
        var year = request.Year ?? DateTime.UtcNow.Year;
        var stats = await _cycleRepo.GetOutcomeStatsAsync(year, ct);

        var total = stats.Values.Sum();
        var pregnancies = stats.GetValueOrDefault(CycleOutcome.Pregnant.ToString(), 0);
        var successRate = total > 0 ? (decimal)pregnancies / total * 100 : 0;

        return new CycleSuccessRatesDto(
            year,
            total,
            pregnancies,
            stats.GetValueOrDefault(CycleOutcome.NotPregnant.ToString(), 0),
            stats.GetValueOrDefault(CycleOutcome.Cancelled.ToString(), 0),
            stats.GetValueOrDefault(CycleOutcome.FrozenAll.ToString(), 0),
            Math.Round(successRate, 2)
        );
    }
}

// ==================== MONTHLY REVENUE ====================
public record GetMonthlyRevenueQuery(int Year) : IRequest<IReadOnlyList<MonthlyRevenueDto>>;

public class GetMonthlyRevenueHandler : IRequestHandler<GetMonthlyRevenueQuery, IReadOnlyList<MonthlyRevenueDto>>
{
    private readonly IInvoiceRepository _invoiceRepo;

    public GetMonthlyRevenueHandler(IInvoiceRepository invoiceRepo) => _invoiceRepo = invoiceRepo;

    public async Task<IReadOnlyList<MonthlyRevenueDto>> Handle(GetMonthlyRevenueQuery request, CancellationToken ct)
    {
        // Single query for all 12 months — eliminates 12 separate DB roundtrips
        var yearlyRevenue = await _invoiceRepo.GetYearlyRevenueByMonthAsync(request.Year, ct);
        var revenues = new List<MonthlyRevenueDto>();
        for (int month = 1; month <= 12; month++)
        {
            revenues.Add(new MonthlyRevenueDto(month, yearlyRevenue.GetValueOrDefault(month, 0)));
        }
        return revenues;
    }
}

// ==================== TREATMENT METHOD DISTRIBUTION ====================
public record GetTreatmentMethodDistributionQuery(int? Year) : IRequest<IReadOnlyList<TreatmentMethodDistributionDto>>;

public class GetTreatmentMethodDistributionHandler : IRequestHandler<GetTreatmentMethodDistributionQuery, IReadOnlyList<TreatmentMethodDistributionDto>>
{
    private readonly ITreatmentCycleRepository _cycleRepo;

    public GetTreatmentMethodDistributionHandler(ITreatmentCycleRepository cycleRepo) => _cycleRepo = cycleRepo;

    public async Task<IReadOnlyList<TreatmentMethodDistributionDto>> Handle(GetTreatmentMethodDistributionQuery request, CancellationToken ct)
    {
        var year = request.Year ?? DateTime.UtcNow.Year;
        var stats = await _cycleRepo.GetMethodDistributionAsync(year, ct);
        return stats.Select(kv => new TreatmentMethodDistributionDto(kv.Key, kv.Value)).ToList();
    }
}

// ==================== QUEUE STATISTICS ====================
public record GetQueueStatisticsQuery(DateTime Date) : IRequest<QueueStatisticsDto>;

public class GetQueueStatisticsHandler : IRequestHandler<GetQueueStatisticsQuery, QueueStatisticsDto>
{
    private readonly IQueueTicketRepository _queueRepo;

    public GetQueueStatisticsHandler(IQueueTicketRepository queueRepo) => _queueRepo = queueRepo;

    public async Task<QueueStatisticsDto> Handle(GetQueueStatisticsQuery request, CancellationToken ct)
    {
        var stats = await _queueRepo.GetDailyStatsAsync(request.Date, ct);
        return new QueueStatisticsDto(
            request.Date,
            stats.GetValueOrDefault("Total", 0),
            stats.GetValueOrDefault("Completed", 0),
            stats.GetValueOrDefault("Waiting", 0),
            stats.GetValueOrDefault("AverageWaitMinutes", 0)
        );
    }
}

// ==================== DTOs ====================
public record DashboardStatsDto(int TotalPatients, int ActiveCycles, int TodayQueueCount, decimal MonthlyRevenue);

public record CycleSuccessRatesDto(
    int Year,
    int TotalCycles,
    int Pregnancies,
    int NotPregnant,
    int Cancelled,
    int FrozenAll,
    decimal SuccessRate
);

public record MonthlyRevenueDto(int Month, decimal Revenue);

public record TreatmentMethodDistributionDto(string Method, int Count);

public record QueueStatisticsDto(
    DateTime Date,
    int TotalTickets,
    int CompletedTickets,
    int WaitingTickets,
    int AverageWaitMinutes
);
