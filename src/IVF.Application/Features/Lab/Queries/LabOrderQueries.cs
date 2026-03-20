using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Lab.Commands;
using MediatR;

namespace IVF.Application.Features.Lab.Queries;

// ==================== GET ORDER BY ID ====================
public record GetLabOrderByIdQuery(Guid Id) : IRequest<LabOrderDto?>;

public class GetLabOrderByIdHandler : IRequestHandler<GetLabOrderByIdQuery, LabOrderDto?>
{
    private readonly ILabOrderRepository _labOrderRepo;
    public GetLabOrderByIdHandler(ILabOrderRepository labOrderRepo) => _labOrderRepo = labOrderRepo;

    public async Task<LabOrderDto?> Handle(GetLabOrderByIdQuery request, CancellationToken ct)
    {
        var order = await _labOrderRepo.GetByIdWithTestsAsync(request.Id, ct);
        return order == null ? null : LabOrderDto.FromEntity(order);
    }
}

// ==================== GET ORDERS BY PATIENT ====================
public record GetLabOrdersByPatientQuery(Guid PatientId) : IRequest<IReadOnlyList<LabOrderDto>>;

public class GetLabOrdersByPatientHandler : IRequestHandler<GetLabOrdersByPatientQuery, IReadOnlyList<LabOrderDto>>
{
    private readonly ILabOrderRepository _labOrderRepo;
    public GetLabOrdersByPatientHandler(ILabOrderRepository labOrderRepo) => _labOrderRepo = labOrderRepo;

    public async Task<IReadOnlyList<LabOrderDto>> Handle(GetLabOrdersByPatientQuery request, CancellationToken ct)
    {
        var orders = await _labOrderRepo.GetByPatientIdAsync(request.PatientId, ct);
        return orders.Select(LabOrderDto.FromEntity).ToList();
    }
}

// ==================== GET ORDERS BY CYCLE ====================
public record GetLabOrdersByCycleQuery(Guid CycleId) : IRequest<IReadOnlyList<LabOrderDto>>;

public class GetLabOrdersByCycleHandler : IRequestHandler<GetLabOrdersByCycleQuery, IReadOnlyList<LabOrderDto>>
{
    private readonly ILabOrderRepository _labOrderRepo;
    public GetLabOrdersByCycleHandler(ILabOrderRepository labOrderRepo) => _labOrderRepo = labOrderRepo;

    public async Task<IReadOnlyList<LabOrderDto>> Handle(GetLabOrdersByCycleQuery request, CancellationToken ct)
    {
        var orders = await _labOrderRepo.GetByCycleIdAsync(request.CycleId, ct);
        return orders.Select(LabOrderDto.FromEntity).ToList();
    }
}

// ==================== SEARCH LAB ORDERS ====================
public record SearchLabOrdersQuery(string? Query, string? Status, string? OrderType, DateTime? FromDate, DateTime? ToDate, int Page = 1, int PageSize = 20)
    : IRequest<(IReadOnlyList<LabOrderDto> Items, int Total)>;

public class SearchLabOrdersHandler : IRequestHandler<SearchLabOrdersQuery, (IReadOnlyList<LabOrderDto> Items, int Total)>
{
    private readonly ILabOrderRepository _labOrderRepo;
    public SearchLabOrdersHandler(ILabOrderRepository labOrderRepo) => _labOrderRepo = labOrderRepo;

    public async Task<(IReadOnlyList<LabOrderDto> Items, int Total)> Handle(SearchLabOrdersQuery request, CancellationToken ct)
    {
        var (items, total) = await _labOrderRepo.SearchAsync(request.Query, request.Status, request.OrderType, request.FromDate, request.ToDate, request.Page, request.PageSize, ct);
        var dtos = items.Select(LabOrderDto.FromEntity).ToList();
        return (dtos, total);
    }
}

// ==================== LAB ORDER STATISTICS ====================
public record LabOrderStatisticsDto(int OrderedCount, int InProgressCount, int CompletedCount, int DeliveredCount);

public record GetLabOrderStatisticsQuery : IRequest<LabOrderStatisticsDto>;

public class GetLabOrderStatisticsHandler : IRequestHandler<GetLabOrderStatisticsQuery, LabOrderStatisticsDto>
{
    private readonly ILabOrderRepository _labOrderRepo;
    public GetLabOrderStatisticsHandler(ILabOrderRepository labOrderRepo) => _labOrderRepo = labOrderRepo;

    public async Task<LabOrderStatisticsDto> Handle(GetLabOrderStatisticsQuery request, CancellationToken ct)
    {
        var ordered = await _labOrderRepo.GetCountByStatusAsync("Ordered", ct);
        var inProgress = await _labOrderRepo.GetCountByStatusAsync("InProgress", ct);
        var completed = await _labOrderRepo.GetCountByStatusAsync("Completed", ct);
        var delivered = await _labOrderRepo.GetCountByStatusAsync("Delivered", ct);
        return new LabOrderStatisticsDto(ordered, inProgress, completed, delivered);
    }
}
