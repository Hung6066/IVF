using IVF.Application.Common.Interfaces;
using IVF.Domain.Constants;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class TreatmentCycleRepository : ITreatmentCycleRepository
{
    private readonly IvfDbContext _context;

    public TreatmentCycleRepository(IvfDbContext context) => _context = context;

    public async Task<TreatmentCycle?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.TreatmentCycles.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<TreatmentCycle?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
        => await _context.TreatmentCycles
            .AsNoTracking()
            .Include(c => c.Couple).ThenInclude(c => c.Wife)
            .Include(c => c.Couple).ThenInclude(c => c.Husband)
            .Include(c => c.Ultrasounds)
            .Include(c => c.Embryos)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<TreatmentCycle>> GetByCoupleIdAsync(Guid coupleId, CancellationToken ct = default)
        => await _context.TreatmentCycles
            .AsNoTracking()
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

    public async Task<Dictionary<IVF.Domain.Enums.CyclePhase, int>> GetPhaseCountsAsync(CancellationToken ct = default)
    {
        return await _context.TreatmentCycles
            .GroupBy(c => c.CurrentPhase)
            .Select(g => new { Phase = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Phase, x => x.Count, ct);
    }

    public async Task<List<LabScheduleDto>> GetLabScheduleAsync(DateTime date, CancellationToken ct = default)
    {
        var queryDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        var list = new List<LabScheduleDto>();

        // Retrievals
        var retrievals = await _context.StimulationData
            .Include(s => s.Cycle).ThenInclude(c => c.Couple).ThenInclude(cp => cp.Wife)
            .Where(s => s.AspirationDate.HasValue && s.AspirationDate.Value.Date == queryDate)
            .ToListAsync(ct);

        list.AddRange(retrievals.Select(r => new LabScheduleDto
        {
            Id = r.Id,
            Time = r.AspirationDate!.Value, // Used to be ToLocalTime() but simplified to keeping it as stored for now
            PatientName = r.Cycle.Couple?.Wife?.FullName ?? "Unknown",
            CycleCode = r.Cycle.CycleCode,
            Procedure = "Chọc hút",
            Type = ScheduleTypes.Retrieval,
            Status = r.Cycle.CurrentPhase > IVF.Domain.Enums.CyclePhase.EggRetrieval ? ScheduleStatuses.Done : ScheduleStatuses.Pending
        }));

        // Transfers
        var transfers = await _context.TransferData
            .Include(t => t.Cycle).ThenInclude(c => c.Couple).ThenInclude(cp => cp.Wife)
            .Where(t => t.TransferDate.HasValue && t.TransferDate.Value.Date == queryDate)
            .ToListAsync(ct);

        list.AddRange(transfers.Select(t => new LabScheduleDto
        {
            Id = t.Id,
            Time = t.TransferDate!.Value,
            PatientName = t.Cycle.Couple?.Wife?.FullName ?? "Unknown",
            CycleCode = t.Cycle.CycleCode,
            Procedure = "Chuyển phôi",
            Type = ScheduleTypes.Transfer,
            Status = t.Cycle.CurrentPhase > IVF.Domain.Enums.CyclePhase.EmbryoTransfer ? ScheduleStatuses.Done : ScheduleStatuses.Pending
        }));

        return list.OrderBy(x => x.Time).ToList();
    }

    public async Task<IReadOnlyList<TreatmentCycle>> GetActiveCyclesAsync(CancellationToken ct = default)
    {
        return await _context.TreatmentCycles
            .AsNoTracking()
            .Include(c => c.Couple).ThenInclude(cp => cp.Wife)
            .Include(c => c.Couple).ThenInclude(cp => cp.Husband)
            .Where(c => c.Outcome == Domain.Enums.CycleOutcome.Ongoing)
            .OrderByDescending(c => c.StartDate)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TreatmentCycle>> GetAllWithDetailsAsync(CancellationToken ct = default)
    {
        return await _context.TreatmentCycles
            .AsNoTracking()
            .Include(c => c.Couple).ThenInclude(cp => cp.Wife)
            .Include(c => c.Couple).ThenInclude(cp => cp.Husband)
            .Include(c => c.Stimulation)
            .Include(c => c.Embryos)
            .OrderByDescending(c => c.StartDate)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TreatmentCycle>> SearchAsync(string query, Guid? patientId = null, CancellationToken ct = default)
    {
        var baseQuery = _context.TreatmentCycles
            .AsNoTracking()
            .Include(c => c.Couple).ThenInclude(cp => cp.Wife)
            .Include(c => c.Couple).ThenInclude(cp => cp.Husband)
            .AsQueryable();

        if (patientId.HasValue)
        {
            baseQuery = baseQuery.Where(c => c.Couple.WifeId == patientId || c.Couple.HusbandId == patientId);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            baseQuery = baseQuery.Where(c => EF.Functions.ILike(c.CycleCode, $"%{query}%") ||
                        (c.Couple != null && (EF.Functions.ILike(c.Couple.Wife.FullName, $"%{query}%") || EF.Functions.ILike(c.Couple.Husband.FullName, $"%{query}%"))));
        }

        return await baseQuery
            .OrderByDescending(c => c.StartDate)
            .Take(20) // Limit results
            .ToListAsync(ct);
    }
}

