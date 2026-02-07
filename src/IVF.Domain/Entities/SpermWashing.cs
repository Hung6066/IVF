using IVF.Domain.Common;
using IVF.Domain.Enums;

namespace IVF.Domain.Entities;

public class SpermWashing : BaseEntity
{
    public Guid CycleId { get; private set; }
    public Guid PatientId { get; private set; }
    public string Method { get; private set; } = string.Empty; // Gradient, Swim-up
    public decimal? PreWashConcentration { get; private set; } // million/ml
    public decimal? PostWashConcentration { get; private set; } // million/ml
    public decimal? PostWashMotility { get; private set; } // %
    public DateTime WashDate { get; private set; }
    public string? Notes { get; private set; }
    public TicketStatus Status { get; private set; } // reuse TicketStatus or add new one? Using TicketStatus for consistency with queue for now, or just simple string/enum if needed. 
    // Actually, distinct status is better. Let's use string or generic status. 
    // Existing SemenAnalysis mock used "Completed", "Pending". 
    // Let's use a specific enum if possible, but for simplicity/speed let's use TicketStatus which has "Completed", "InService".
    
    // Navigation
    public TreatmentCycle Cycle { get; private set; } = null!;
    public Patient Patient { get; private set; } = null!;

    private SpermWashing() { }

    public static SpermWashing Create(
        Guid cycleId,
        Guid patientId,
        string method,
        DateTime washDate)
    {
        return new SpermWashing
        {
            Id = Guid.NewGuid(),
            CycleId = cycleId,
            PatientId = patientId,
            Method = method,
            WashDate = washDate,
            Status = TicketStatus.Waiting, // Default
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateResult(
        decimal? preWashConc,
        decimal? postWashConc,
        decimal? postWashMotility,
        string? notes)
    {
        PreWashConcentration = preWashConc;
        PostWashConcentration = postWashConc;
        PostWashMotility = postWashMotility;
        Notes = notes;
        Status = TicketStatus.Completed; // Auto complete on result
        SetUpdated();
    }
}
