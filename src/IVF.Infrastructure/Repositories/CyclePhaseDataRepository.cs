using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class CyclePhaseDataRepository : ICyclePhaseDataRepository
{
    private readonly IvfDbContext _context;

    public CyclePhaseDataRepository(IvfDbContext context)
    {
        _context = context;
    }

    // Treatment Indication
    public async Task<TreatmentIndication?> GetIndicationByCycleIdAsync(Guid cycleId, CancellationToken ct = default)
        => await _context.TreatmentIndications.FirstOrDefaultAsync(x => x.CycleId == cycleId, ct);

    public async Task AddIndicationAsync(TreatmentIndication indication, CancellationToken ct = default)
        => await _context.TreatmentIndications.AddAsync(indication, ct);

    // Stimulation Data
    public async Task<StimulationData?> GetStimulationByCycleIdAsync(Guid cycleId, CancellationToken ct = default)
        => await _context.StimulationData.FirstOrDefaultAsync(x => x.CycleId == cycleId, ct);

    public async Task AddStimulationAsync(StimulationData data, CancellationToken ct = default)
        => await _context.StimulationData.AddAsync(data, ct);

    // Culture Data
    public async Task<CultureData?> GetCultureByCycleIdAsync(Guid cycleId, CancellationToken ct = default)
        => await _context.CultureData.FirstOrDefaultAsync(x => x.CycleId == cycleId, ct);

    public async Task AddCultureAsync(CultureData data, CancellationToken ct = default)
        => await _context.CultureData.AddAsync(data, ct);

    // Transfer Data
    public async Task<TransferData?> GetTransferByCycleIdAsync(Guid cycleId, CancellationToken ct = default)
        => await _context.TransferData.FirstOrDefaultAsync(x => x.CycleId == cycleId, ct);

    public async Task AddTransferAsync(TransferData data, CancellationToken ct = default)
        => await _context.TransferData.AddAsync(data, ct);

    // Luteal Phase Data
    public async Task<LutealPhaseData?> GetLutealPhaseByCycleIdAsync(Guid cycleId, CancellationToken ct = default)
        => await _context.LutealPhaseData.FirstOrDefaultAsync(x => x.CycleId == cycleId, ct);

    public async Task AddLutealPhaseAsync(LutealPhaseData data, CancellationToken ct = default)
        => await _context.LutealPhaseData.AddAsync(data, ct);

    // Pregnancy Data
    public async Task<PregnancyData?> GetPregnancyByCycleIdAsync(Guid cycleId, CancellationToken ct = default)
        => await _context.PregnancyData.FirstOrDefaultAsync(x => x.CycleId == cycleId, ct);

    public async Task AddPregnancyAsync(PregnancyData data, CancellationToken ct = default)
        => await _context.PregnancyData.AddAsync(data, ct);

    // Birth Data
    public async Task<BirthData?> GetBirthByCycleIdAsync(Guid cycleId, CancellationToken ct = default)
        => await _context.BirthData.FirstOrDefaultAsync(x => x.CycleId == cycleId, ct);

    public async Task AddBirthAsync(BirthData data, CancellationToken ct = default)
        => await _context.BirthData.AddAsync(data, ct);

    // Adverse Events
    public async Task<IReadOnlyList<AdverseEventData>> GetAdverseEventsByCycleIdAsync(Guid cycleId, CancellationToken ct = default)
        => await _context.AdverseEventData.Where(x => x.CycleId == cycleId).ToListAsync(ct);

    public async Task AddAdverseEventAsync(AdverseEventData data, CancellationToken ct = default)
        => await _context.AdverseEventData.AddAsync(data, ct);

    public async Task DeleteAdverseEventAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.AdverseEventData.FindAsync([id], cancellationToken: ct);
        if (entity != null)
        {
            entity.MarkAsDeleted();
        }
    }
}
