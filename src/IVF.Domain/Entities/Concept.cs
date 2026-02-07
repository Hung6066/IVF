using IVF.Domain.Common;
using NpgsqlTypes;

namespace IVF.Domain.Entities;

/// <summary>
/// Represents a medical or clinical concept in your local terminology
/// Can be mapped to external terminologies like SNOMED CT, HL7, LOINC
/// </summary>
public class Concept : BaseEntity
{
    public string Code { get; private set; } = string.Empty;
    public string Display { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string System { get; private set; } = "LOCAL"; // Your own system identifier
    public ConceptType ConceptType { get; private set; }
    
    // Full-text search (PostgreSQL TsVector)
    public NpgsqlTsVector? SearchVector { get; private set; }

    // Navigation
    public ICollection<ConceptMapping> Mappings { get; private set; } = new List<ConceptMapping>();
    public ICollection<FormField> FormFields { get; private set; } = new List<FormField>();
    public ICollection<FormFieldOption> FormFieldOptions { get; private set; } = new List<FormFieldOption>();

    private Concept() { }

    public static Concept Create(
        string code,
        string display,
        string? description = null,
        string system = "LOCAL",
        ConceptType conceptType = ConceptType.Clinical)
    {
        return new Concept
        {
            Id = Guid.NewGuid(),
            Code = code,
            Display = display,
            Description = description,
            System = system,
            ConceptType = conceptType,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(
        string display,
        string? description)
    {
        Display = display;
        Description = description;
        SetUpdated();
    }

    /// <summary>
    /// Map this concept to an external terminology (SNOMED CT, HL7, LOINC, etc.)
    /// </summary>
    public ConceptMapping AddMapping(
        string targetSystem,
        string targetCode,
        string targetDisplay,
        string? relationship = "equivalent")
    {
        var mapping = ConceptMapping.Create(
            this.Id,
            targetSystem,
            targetCode,
            targetDisplay,
            relationship
        );

        Mappings.Add(mapping);
        SetUpdated();
        return mapping;
    }
}

/// <summary>
/// Type of concept for categorization
/// </summary>
public enum ConceptType
{
    Clinical = 0,      // Clinical observations, procedures
    Laboratory = 1,    // Lab tests, results
    Medication = 2,    // Drugs, medications
    Diagnosis = 3,     // Diseases, conditions
    Procedure = 4,     // Medical procedures
    Anatomical = 5,    // Body structures
    Administrative = 6 // Administrative concepts
}
