using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class QueueTicketRepository : IQueueTicketRepository
{
    private readonly IvfDbContext _context;

    public QueueTicketRepository(IvfDbContext context) => _context = context;

    public async Task<QueueTicket?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.QueueTickets.FirstOrDefaultAsync(q => q.Id == id, ct);

    public async Task<IReadOnlyList<QueueTicket>> GetByDepartmentTodayAsync(string departmentCode, CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        return await _context.QueueTickets
            .Where(q => q.DepartmentCode == departmentCode && q.IssuedAt >= today
                && q.Status != Domain.Enums.TicketStatus.Completed
                && q.Status != Domain.Enums.TicketStatus.Skipped
                && q.Status != Domain.Enums.TicketStatus.Cancelled)
            .Include(q => q.Patient)
            .OrderBy(q => q.IssuedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<QueueTicket>> GetDepartmentHistoryTodayAsync(string departmentCode, CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        return await _context.QueueTickets
            .Where(q => q.DepartmentCode == departmentCode && q.IssuedAt >= today
                && (q.Status == Domain.Enums.TicketStatus.Completed
                 || q.Status == Domain.Enums.TicketStatus.Skipped
                 || q.Status == Domain.Enums.TicketStatus.Cancelled))
            .Include(q => q.Patient)
            .OrderByDescending(q => q.CompletedAt ?? q.IssuedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<QueueTicket>> GetByPatientTodayAsync(Guid patientId, CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        return await _context.QueueTickets
            .Where(q => q.PatientId == patientId && q.IssuedAt >= today)
            .Include(q => q.Patient)
            .OrderByDescending(q => q.IssuedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<QueueTicket>> GetAllTodayAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        return await _context.QueueTickets
            .Where(q => q.IssuedAt >= today
                && q.Status != Domain.Enums.TicketStatus.Completed
                && q.Status != Domain.Enums.TicketStatus.Skipped
                && q.Status != Domain.Enums.TicketStatus.Cancelled)
            .Include(q => q.Patient)
            .OrderBy(q => q.IssuedAt)
            .ToListAsync(ct);
    }

    public async Task<QueueTicket> AddAsync(QueueTicket ticket, CancellationToken ct = default)
    {
        await _context.QueueTickets.AddAsync(ticket, ct);
        return ticket;
    }

    public Task UpdateAsync(QueueTicket ticket, CancellationToken ct = default)
    {
        _context.QueueTickets.Update(ticket);
        return Task.CompletedTask;
    }

    public async Task<string> GenerateTicketNumberAsync(string departmentCode, CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var count = await _context.QueueTickets
            .CountAsync(q => q.DepartmentCode == departmentCode && q.IssuedAt >= today, ct);
        var prefix = departmentCode.Split('-')[0];
        return $"{prefix}-{count + 1:D3}";
    }

    public async Task<int> GetTodayCountAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        return await _context.QueueTickets.CountAsync(q => q.IssuedAt >= today, ct);
    }

    public async Task<Dictionary<string, int>> GetDailyStatsAsync(DateTime date, CancellationToken ct = default)
    {
        var dateStart = date.Date;
        var dateEnd = dateStart.AddDays(1);
        var tickets = await _context.QueueTickets
            .Where(q => q.IssuedAt >= dateStart && q.IssuedAt < dateEnd)
            .ToListAsync(ct);

        var completed = tickets.Count(t => t.Status == Domain.Enums.TicketStatus.Completed);
        var waiting = tickets.Count(t => t.Status == Domain.Enums.TicketStatus.Waiting);
        var avgWait = tickets.Where(t => t.CalledAt.HasValue)
            .Select(t => (t.CalledAt!.Value - t.IssuedAt).TotalMinutes)
            .DefaultIfEmpty(0).Average();

        return new Dictionary<string, int>
        {
            ["Total"] = tickets.Count,
            ["Completed"] = completed,
            ["Waiting"] = waiting,
            ["AverageWaitMinutes"] = (int)avgWait
        };
    }
}
