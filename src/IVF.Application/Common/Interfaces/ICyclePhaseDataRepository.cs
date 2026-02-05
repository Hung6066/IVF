using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

/// <summary>
/// Repository for managing cycle phase-specific data entities
/// </summary>
public interface ICyclePhaseDataRepository
{
    // Treatment Indication
    Task<TreatmentIndication?> GetIndicationByCycleIdAsync(Guid cycleId, CancellationToken ct = default);
    Task AddIndicationAsync(TreatmentIndication indication, CancellationToken ct = default);

    // Stimulation Data
    Task<StimulationData?> GetStimulationByCycleIdAsync(Guid cycleId, CancellationToken ct = default);
    Task AddStimulationAsync(StimulationData data, CancellationToken ct = default);

    // Culture Data
    Task<CultureData?> GetCultureByCycleIdAsync(Guid cycleId, CancellationToken ct = default);
    Task AddCultureAsync(CultureData data, CancellationToken ct = default);

    // Transfer Data
    Task<TransferData?> GetTransferByCycleIdAsync(Guid cycleId, CancellationToken ct = default);
    Task AddTransferAsync(TransferData data, CancellationToken ct = default);

    // Luteal Phase Data
    Task<LutealPhaseData?> GetLutealPhaseByCycleIdAsync(Guid cycleId, CancellationToken ct = default);
    Task AddLutealPhaseAsync(LutealPhaseData data, CancellationToken ct = default);

    // Pregnancy Data
    Task<PregnancyData?> GetPregnancyByCycleIdAsync(Guid cycleId, CancellationToken ct = default);
    Task AddPregnancyAsync(PregnancyData data, CancellationToken ct = default);

    // Birth Data
    Task<BirthData?> GetBirthByCycleIdAsync(Guid cycleId, CancellationToken ct = default);
    Task AddBirthAsync(BirthData data, CancellationToken ct = default);

    // Adverse Events
    Task<IReadOnlyList<AdverseEventData>> GetAdverseEventsByCycleIdAsync(Guid cycleId, CancellationToken ct = default);
    Task AddAdverseEventAsync(AdverseEventData data, CancellationToken ct = default);
    Task DeleteAdverseEventAsync(Guid id, CancellationToken ct = default);
}
