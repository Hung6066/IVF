using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Maps a local concept to external terminology systems
/// Example: Your "Blood Pressure" concept → SNOMED CT "271649006"
/// Example: Your "Blood Pressure" concept → HL7 "8480-6"
/// </summary>
public class ConceptMapping : BaseEntity
{
    public Guid ConceptId { get; private set; }
    public string TargetSystem { get; private set; } = string.Empty;
    public string TargetCode { get; private set; } = string.Empty;
    public string TargetDisplay { get; private set; } = string.Empty;
    public string? Relationship { get; private set; } // "equivalent", "broader", "narrower", "related"
    public bool IsActive { get; private set; } = true;

    // Navigation
    public Concept Concept { get; private set; } = null!;

    private ConceptMapping() { }

    public static ConceptMapping Create(
        Guid conceptId,
        string targetSystem,
        string targetCode,
        string targetDisplay,
        string? relationship = "equivalent")
    {
        return new ConceptMapping
        {
            Id = Guid.NewGuid(),
            ConceptId = conceptId,
            TargetSystem = targetSystem,
            TargetCode = targetCode,
            TargetDisplay = targetDisplay,
            Relationship = relationship,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(
        string targetCode,
        string targetDisplay,
        string? relationship)
    {
        TargetCode = targetCode;
        TargetDisplay = targetDisplay;
        Relationship = relationship;
        SetUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        SetUpdated();
    }

    public void Activate()
    {
        IsActive = true;
        SetUpdated();
    }
}
