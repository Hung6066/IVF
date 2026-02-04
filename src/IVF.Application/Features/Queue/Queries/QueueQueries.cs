using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Queue.Commands;
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
        var tickets = await _queueRepo.GetByDepartmentTodayAsync(request.DepartmentCode, ct);
        return tickets.Select(t => QueueTicketDto.FromEntity(t, t.Patient?.FullName ?? "")).ToList();
    }
}
