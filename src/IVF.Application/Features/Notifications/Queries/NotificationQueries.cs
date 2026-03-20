using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Notifications.Commands;
using MediatR;

namespace IVF.Application.Features.Notifications.Queries;

public record GetNotificationByIdQuery(Guid Id) : IRequest<NotificationDto?>;

public class GetNotificationByIdHandler : IRequestHandler<GetNotificationByIdQuery, NotificationDto?>
{
    private readonly INotificationRepository _repo;
    public GetNotificationByIdHandler(INotificationRepository repo) => _repo = repo;

    public async Task<NotificationDto?> Handle(GetNotificationByIdQuery req, CancellationToken ct)
    {
        var n = await _repo.GetByIdAsync(req.Id);
        return n is null ? null : NotificationDto.FromEntity(n);
    }
}

public record GetNotificationsByUserQuery(Guid UserId, bool UnreadOnly = false) : IRequest<IReadOnlyList<NotificationDto>>;

public class GetNotificationsByUserHandler : IRequestHandler<GetNotificationsByUserQuery, IReadOnlyList<NotificationDto>>
{
    private readonly INotificationRepository _repo;
    public GetNotificationsByUserHandler(INotificationRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<NotificationDto>> Handle(GetNotificationsByUserQuery req, CancellationToken ct)
    {
        var items = await _repo.GetByUserAsync(req.UserId, req.UnreadOnly);
        return items.Select(NotificationDto.FromEntity).ToList().AsReadOnly();
    }
}

public record GetUnreadCountQuery(Guid UserId) : IRequest<int>;

public class GetUnreadCountHandler : IRequestHandler<GetUnreadCountQuery, int>
{
    private readonly INotificationRepository _repo;
    public GetUnreadCountHandler(INotificationRepository repo) => _repo = repo;

    public async Task<int> Handle(GetUnreadCountQuery req, CancellationToken ct)
        => await _repo.GetUnreadCountAsync(req.UserId);
}
