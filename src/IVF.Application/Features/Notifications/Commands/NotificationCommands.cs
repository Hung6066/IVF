using IVF.Application.Common;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.Notifications.Commands;

// ==================== DTO ====================
public class NotificationDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public DateTime CreatedAt { get; set; }

    public static NotificationDto FromEntity(Notification n) => new()
    {
        Id = n.Id,
        UserId = n.UserId,
        Title = n.Title,
        Message = n.Message,
        Type = n.Type.ToString(),
        IsRead = n.IsRead,
        ReadAt = n.ReadAt,
        EntityType = n.EntityType,
        EntityId = n.EntityId,
        CreatedAt = n.CreatedAt
    };
}

// ==================== SEND NOTIFICATION ====================
public record SendNotificationCommand(
    Guid UserId,
    string Title,
    string Message,
    NotificationType Type,
    string? EntityType = null,
    Guid? EntityId = null) : IRequest<Result<NotificationDto>>;

public class SendNotificationHandler : IRequestHandler<SendNotificationCommand, Result<NotificationDto>>
{
    private readonly INotificationRepository _repo;
    private readonly IUnitOfWork _unitOfWork;

    public SendNotificationHandler(INotificationRepository repo, IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<NotificationDto>> Handle(SendNotificationCommand req, CancellationToken ct)
    {
        var notification = Notification.Create(req.UserId, req.Title, req.Message, req.Type, req.EntityType, req.EntityId);
        await _repo.AddAsync(notification);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<NotificationDto>.Success(NotificationDto.FromEntity(notification));
    }
}

// ==================== BROADCAST ====================
public record BroadcastNotificationCommand(
    List<Guid> UserIds,
    string Title,
    string Message,
    NotificationType Type) : IRequest<Result<int>>;

public class BroadcastNotificationHandler : IRequestHandler<BroadcastNotificationCommand, Result<int>>
{
    private readonly INotificationRepository _repo;
    private readonly IUnitOfWork _unitOfWork;

    public BroadcastNotificationHandler(INotificationRepository repo, IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<int>> Handle(BroadcastNotificationCommand req, CancellationToken ct)
    {
        var notifications = req.UserIds.Select(uid => Notification.Create(uid, req.Title, req.Message, req.Type));
        await _repo.AddManyAsync(notifications);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<int>.Success(req.UserIds.Count);
    }
}

// ==================== MARK READ ====================
public record MarkNotificationReadCommand(Guid NotificationId, Guid RequestingUserId) : IRequest<Result<NotificationDto>>;

public class MarkNotificationReadHandler : IRequestHandler<MarkNotificationReadCommand, Result<NotificationDto>>
{
    private readonly INotificationRepository _repo;
    private readonly IUnitOfWork _unitOfWork;

    public MarkNotificationReadHandler(INotificationRepository repo, IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<NotificationDto>> Handle(MarkNotificationReadCommand req, CancellationToken ct)
    {
        var notification = await _repo.GetByIdAsync(req.NotificationId);
        if (notification is null) return Result<NotificationDto>.Failure("Không tìm thấy thông báo.");
        if (notification.UserId != req.RequestingUserId) return Result<NotificationDto>.Failure("Không có quyền truy cập.");

        notification.MarkAsRead();
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<NotificationDto>.Success(NotificationDto.FromEntity(notification));
    }
}

// ==================== MARK ALL READ ====================
public record MarkAllNotificationsReadCommand(Guid UserId) : IRequest<Result<bool>>;

public class MarkAllNotificationsReadHandler : IRequestHandler<MarkAllNotificationsReadCommand, Result<bool>>
{
    private readonly INotificationRepository _repo;
    private readonly IUnitOfWork _unitOfWork;

    public MarkAllNotificationsReadHandler(INotificationRepository repo, IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<bool>> Handle(MarkAllNotificationsReadCommand req, CancellationToken ct)
    {
        await _repo.MarkAsReadAsync(req.UserId);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<bool>.Success(true);
    }
}
