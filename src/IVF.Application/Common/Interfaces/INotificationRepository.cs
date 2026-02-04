using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> GetByUserAsync(Guid userId, bool unreadOnly = false, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);
    Task<Notification> AddAsync(Notification notification, CancellationToken ct = default);
    Task AddManyAsync(IEnumerable<Notification> notifications, CancellationToken ct = default);
    Task UpdateAsync(Notification notification, CancellationToken ct = default);
    Task MarkAsReadAsync(Guid userId, Guid? notificationId = null, CancellationToken ct = default);
}
