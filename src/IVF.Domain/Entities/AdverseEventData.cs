using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Adverse Event data - Tab 8 of cycle details
/// </summary>
public class AdverseEventData : BaseEntity
{
    public Guid CycleId { get; private set; }
    public DateTime? EventDate { get; private set; }
    public string? EventType { get; private set; }
    public string? Severity { get; private set; }
    public string? Description { get; private set; }
    public string? Treatment { get; private set; }
    public string? Outcome { get; private set; }

    // Navigation
    public virtual TreatmentCycle Cycle { get; private set; } = null!;

    private AdverseEventData() { }

    public static AdverseEventData Create(
        Guid cycleId,
        DateTime? eventDate,
        string? eventType,
        string? severity,
        string? description,
        string? treatment,
        string? outcome)
    {
        return new AdverseEventData
        {
            CycleId = cycleId,
            EventDate = eventDate,
            EventType = eventType,
            Severity = severity,
            Description = description,
            Treatment = treatment,
            Outcome = outcome
        };
    }

    public void Update(
        DateTime? eventDate,
        string? eventType,
        string? severity,
        string? description,
        string? treatment,
        string? outcome)
    {
        EventDate = eventDate;
        EventType = eventType;
        Severity = severity;
        Description = description;
        Treatment = treatment;
        Outcome = outcome;
        SetUpdated();
    }
}
