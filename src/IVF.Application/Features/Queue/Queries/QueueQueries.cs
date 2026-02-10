using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Queue.Commands;
using IVF.Domain.Enums;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.Queue.Queries;

// ==================== GET QUEUE BY DEPARTMENT ====================
public record GetQueueByDepartmentQuery(string DepartmentCode) : IRequest<IReadOnlyList<QueueTicketDto>>;

public class GetQueueByDepartmentHandler : IRequestHandler<GetQueueByDepartmentQuery, IReadOnlyList<QueueTicketDto>>
{
    private readonly IQueueTicketRepository _queueRepo;

    public GetQueueByDepartmentHandler(IQueueTicketRepository queueRepo)
    {
        _queueRepo = queueRepo;
    }

    public async Task<IReadOnlyList<QueueTicketDto>> Handle(GetQueueByDepartmentQuery request, CancellationToken ct)
    {
        IReadOnlyList<QueueTicket> tickets;
        if (request.DepartmentCode.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            tickets = await _queueRepo.GetAllTodayAsync(ct);
        else
            tickets = await _queueRepo.GetByDepartmentTodayAsync(request.DepartmentCode, ct);
            
        return tickets.Select(t => QueueTicketDto.FromEntity(t, t.Patient?.FullName ?? "", t.Patient?.PatientCode)).ToList();
    }
}

// ==================== GET DEPARTMENT HISTORY ====================
public record GetDepartmentHistoryQuery(string DepartmentCode) : IRequest<IReadOnlyList<QueueTicketDto>>;

public class GetDepartmentHistoryHandler : IRequestHandler<GetDepartmentHistoryQuery, IReadOnlyList<QueueTicketDto>>
{
    private readonly IQueueTicketRepository _queueRepo;

    public GetDepartmentHistoryHandler(IQueueTicketRepository queueRepo)
    {
        _queueRepo = queueRepo;
    }

    public async Task<IReadOnlyList<QueueTicketDto>> Handle(GetDepartmentHistoryQuery request, CancellationToken ct)
    {
        var tickets = await _queueRepo.GetDepartmentHistoryTodayAsync(request.DepartmentCode, ct);
        return tickets.Select(t => QueueTicketDto.FromEntity(t, t.Patient?.FullName ?? "", t.Patient?.PatientCode)).ToList();
    }
}

// ==================== GET PATIENT PENDING TICKET ====================
public record PendingServicesDto(List<Guid> ServiceIds, List<string> TicketNumbers);

public record GetPatientPendingTicketQuery(Guid PatientId) : IRequest<Result<PendingServicesDto>>;

public class GetPatientPendingTicketHandler : IRequestHandler<GetPatientPendingTicketQuery, Result<PendingServicesDto>>
{
    private readonly IQueueTicketRepository _queueRepo;

    public GetPatientPendingTicketHandler(IQueueTicketRepository queueRepo)
    {
        _queueRepo = queueRepo;
    }

    public async Task<Result<PendingServicesDto>> Handle(GetPatientPendingTicketQuery request, CancellationToken ct)
    {
        var tickets = await _queueRepo.GetByPatientTodayAsync(request.PatientId, ct);
        var relevantTickets = tickets.Where(t => 
            t.Status == TicketStatus.Waiting || 
            t.Status == TicketStatus.Called || 
            t.Status == TicketStatus.InService ||
            t.Status == TicketStatus.Completed).ToList();
        
        var allServiceIds = new List<Guid>();
        var ticketNumbers = new List<string>();

        foreach (var t in relevantTickets)
        {
            if (t.Services != null)
            {
                allServiceIds.AddRange(t.Services.Select(s => s.ServiceCatalogId));
            }
            ticketNumbers.Add(t.TicketNumber);
        }
        
        return Result<PendingServicesDto>.Success(new PendingServicesDto(allServiceIds, ticketNumbers));
    }
}
