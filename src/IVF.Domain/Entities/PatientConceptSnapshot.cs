using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Materialized latest known value for a (Patient, Concept) pair.
/// Updated on every form submission for O(1) cross-form data lookup.
/// </summary>
public class PatientConceptSnapshot : BaseEntity
{
    public Guid PatientId { get; private set; }
    public Guid ConceptId { get; private set; }
    public Guid FormResponseId { get; private set; }
    public Guid FormFieldId { get; private set; }
    public Guid? CycleId { get; private set; }

    // Polymorphic value storage (mirrors FormFieldValue)
    public string? TextValue { get; private set; }
    public decimal? NumericValue { get; private set; }
    public DateTime? DateValue { get; private set; }
    public bool? BooleanValue { get; private set; }
    public string? JsonValue { get; private set; }

    public DateTime CapturedAt { get; private set; }

    // Navigation
    public Patient Patient { get; private set; } = null!;
    public Concept Concept { get; private set; } = null!;
    public FormResponse FormResponse { get; private set; } = null!;
    public FormField FormField { get; private set; } = null!;
    public TreatmentCycle? Cycle { get; private set; }

    private PatientConceptSnapshot() { }

    public static PatientConceptSnapshot Create(
        Guid patientId,
        Guid conceptId,
        Guid formResponseId,
        Guid formFieldId,
        Guid? cycleId,
        DateTime capturedAt,
        string? textValue = null,
        decimal? numericValue = null,
        DateTime? dateValue = null,
        bool? booleanValue = null,
        string? jsonValue = null)
    {
        return new PatientConceptSnapshot
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            ConceptId = conceptId,
            FormResponseId = formResponseId,
            FormFieldId = formFieldId,
            CycleId = cycleId,
            CapturedAt = capturedAt,
            TextValue = textValue,
            NumericValue = numericValue,
            DateValue = dateValue,
            BooleanValue = booleanValue,
            JsonValue = jsonValue,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateValue(
        Guid formResponseId,
        Guid formFieldId,
        DateTime capturedAt,
        string? textValue,
        decimal? numericValue,
        DateTime? dateValue,
        bool? booleanValue,
        string? jsonValue)
    {
        FormResponseId = formResponseId;
        FormFieldId = formFieldId;
        CapturedAt = capturedAt;
        TextValue = textValue;
        NumericValue = numericValue;
        DateValue = dateValue;
        BooleanValue = booleanValue;
        JsonValue = jsonValue;
        SetUpdated();
    }

    /// <summary>
    /// Returns the best display value from the polymorphic storage
    /// </summary>
    public string GetDisplayValue()
    {
        if (!string.IsNullOrEmpty(TextValue)) return TextValue;
        if (NumericValue.HasValue) return NumericValue.Value.ToString();
        if (DateValue.HasValue) return DateValue.Value.ToString("yyyy-MM-dd");
        if (BooleanValue.HasValue) return BooleanValue.Value ? "Có" : "Không";
        if (!string.IsNullOrEmpty(JsonValue)) return JsonValue;
        return string.Empty;
    }
}
