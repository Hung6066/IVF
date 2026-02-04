using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface IQueueTicketRepository
{
    Task<QueueTicket?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<QueueTicket>> GetByDepartmentTodayAsync(string departmentCode, CancellationToken ct = default);
    Task<QueueTicket> AddAsync(QueueTicket ticket, CancellationToken ct = default);
    Task UpdateAsync(QueueTicket ticket, CancellationToken ct = default);
    Task<string> GenerateTicketNumberAsync(string departmentCode, CancellationToken ct = default);
    // Reporting
    Task<int> GetTodayCountAsync(CancellationToken ct = default);
    Task<Dictionary<string, int>> GetDailyStatsAsync(DateTime date, CancellationToken ct = default);
}
