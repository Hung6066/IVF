using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class PatientRepository : IPatientRepository
{
    private readonly IvfDbContext _context;

    public PatientRepository(IvfDbContext context) => _context = context;

    public async Task<Patient?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Patients.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<Patient?> GetByCodeAsync(string code, CancellationToken ct = default)
        => await _context.Patients.FirstOrDefaultAsync(p => p.PatientCode == code, ct);

    public async Task<(IReadOnlyList<Patient> Items, int Total)> SearchAsync(
        string? query, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _context.Patients.AsQueryable();
        
        if (!string.IsNullOrEmpty(query))
            q = q.Where(p => p.FullName.Contains(query) || p.PatientCode.Contains(query) || 
                (p.Phone != null && p.Phone.Contains(query)));

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<Patient> AddAsync(Patient patient, CancellationToken ct = default)
    {
        await _context.Patients.AddAsync(patient, ct);
        return patient;
    }

    public Task UpdateAsync(Patient patient, CancellationToken ct = default)
    {
        _context.Patients.Update(patient);
        return Task.CompletedTask;
    }

    public async Task<string> GenerateCodeAsync(CancellationToken ct = default)
    {
        var count = await _context.Patients.CountAsync(ct);
        return $"BN-{DateTime.Now:yyyy}-{count + 1:D6}";
    }

    public async Task<int> GetTotalCountAsync(CancellationToken ct = default)
        => await _context.Patients.CountAsync(ct);
}

public class UserRepository : IUserRepository
{
    private readonly IvfDbContext _context;

    public UserRepository(IvfDbContext context) => _context = context;

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
        => await _context.Users.FirstOrDefaultAsync(u => u.Username == username && u.IsActive, ct);

    public async Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
        => await _context.Users.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken && u.IsActive, ct);

    public Task UpdateAsync(User user, CancellationToken ct = default)
    {
        _context.Users.Update(user);
        return Task.CompletedTask;
    }
}

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
            .Where(q => q.DepartmentCode == departmentCode && q.IssuedAt >= today)
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

public class TreatmentCycleRepository : ITreatmentCycleRepository
{
    private readonly IvfDbContext _context;

    public TreatmentCycleRepository(IvfDbContext context) => _context = context;

    public async Task<TreatmentCycle?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.TreatmentCycles.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<TreatmentCycle?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
        => await _context.TreatmentCycles
            .Include(c => c.Couple).ThenInclude(c => c.Wife)
            .Include(c => c.Couple).ThenInclude(c => c.Husband)
            .Include(c => c.Ultrasounds)
            .Include(c => c.Embryos)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<TreatmentCycle>> GetByCoupleIdAsync(Guid coupleId, CancellationToken ct = default)
        => await _context.TreatmentCycles
            .Where(c => c.CoupleId == coupleId)
            .OrderByDescending(c => c.StartDate)
            .ToListAsync(ct);

    public async Task<TreatmentCycle> AddAsync(TreatmentCycle cycle, CancellationToken ct = default)
    {
        await _context.TreatmentCycles.AddAsync(cycle, ct);
        return cycle;
    }

    public Task UpdateAsync(TreatmentCycle cycle, CancellationToken ct = default)
    {
        _context.TreatmentCycles.Update(cycle);
        return Task.CompletedTask;
    }

    public async Task<string> GenerateCodeAsync(CancellationToken ct = default)
    {
        var count = await _context.TreatmentCycles.CountAsync(ct);
        return $"CK-{DateTime.Now:yyyy}-{count + 1:D4}";
    }

    public async Task<int> GetActiveCountAsync(CancellationToken ct = default)
        => await _context.TreatmentCycles.CountAsync(c => c.Outcome == Domain.Enums.CycleOutcome.Ongoing, ct);

    public async Task<Dictionary<string, int>> GetOutcomeStatsAsync(int year, CancellationToken ct = default)
    {
        var cycles = await _context.TreatmentCycles
            .Where(c => c.StartDate.Year == year && c.Outcome != Domain.Enums.CycleOutcome.Ongoing)
            .GroupBy(c => c.Outcome)
            .Select(g => new { Outcome = g.Key.ToString(), Count = g.Count() })
            .ToListAsync(ct);
        return cycles.ToDictionary(x => x.Outcome, x => x.Count);
    }

    public async Task<Dictionary<string, int>> GetMethodDistributionAsync(int year, CancellationToken ct = default)
    {
        var cycles = await _context.TreatmentCycles
            .Where(c => c.StartDate.Year == year)
            .GroupBy(c => c.Method)
            .Select(g => new { Method = g.Key.ToString(), Count = g.Count() })
            .ToListAsync(ct);
        return cycles.ToDictionary(x => x.Method, x => x.Count);
    }
}

public class CoupleRepository : ICoupleRepository
{
    private readonly IvfDbContext _context;

    public CoupleRepository(IvfDbContext context) => _context = context;

    public async Task<Couple?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Couples
            .Include(c => c.Wife)
            .Include(c => c.Husband)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<Couple?> GetByWifeAndHusbandAsync(Guid wifeId, Guid husbandId, CancellationToken ct = default)
        => await _context.Couples
            .FirstOrDefaultAsync(c => c.WifeId == wifeId && c.HusbandId == husbandId, ct);

    public async Task<IReadOnlyList<Couple>> GetAllAsync(CancellationToken ct = default)
        => await _context.Couples.ToListAsync(ct);

    public async Task<Couple> AddAsync(Couple couple, CancellationToken ct = default)
    {
        await _context.Couples.AddAsync(couple, ct);
        return couple;
    }

    public Task UpdateAsync(Couple couple, CancellationToken ct = default)
    {
        _context.Couples.Update(couple);
        return Task.CompletedTask;
    }
}

// Unit of Work
public class UnitOfWork : IUnitOfWork
{
    private readonly IvfDbContext _context;

    public UnitOfWork(IvfDbContext context) => _context = context;

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
