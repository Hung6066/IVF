using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly IvfDbContext _context;
    
    public NotificationRepository(IvfDbContext context) => _context = context;

    public async Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id, ct);

    public async Task<IReadOnlyList<Notification>> GetByUserAsync(Guid userId, bool unreadOnly = false, CancellationToken ct = default)
    {
        var query = _context.Notifications.Where(n => n.UserId == userId);
        if (unreadOnly)
            query = query.Where(n => !n.IsRead);
        return await query.OrderByDescending(n => n.CreatedAt).ToListAsync(ct);
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
        => await _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);

    public async Task<Notification> AddAsync(Notification notification, CancellationToken ct = default)
    {
        await _context.Notifications.AddAsync(notification, ct);
        return notification;
    }

    public async Task AddManyAsync(IEnumerable<Notification> notifications, CancellationToken ct = default)
        => await _context.Notifications.AddRangeAsync(notifications, ct);

    public Task UpdateAsync(Notification notification, CancellationToken ct = default)
    {
        _context.Notifications.Update(notification);
        return Task.CompletedTask;
    }

    public async Task MarkAsReadAsync(Guid userId, Guid? notificationId = null, CancellationToken ct = default)
    {
        var query = _context.Notifications.Where(n => n.UserId == userId && !n.IsRead);
        if (notificationId.HasValue)
            query = query.Where(n => n.Id == notificationId);
        
        await query.ExecuteUpdateAsync(s => s
            .SetProperty(n => n.IsRead, true)
            .SetProperty(n => n.ReadAt, DateTime.UtcNow), ct);
    }
}
