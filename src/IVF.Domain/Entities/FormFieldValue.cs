using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Individual field value within a form response
/// </summary>
public class FormFieldValue : BaseEntity
{
    public Guid FormResponseId { get; private set; }
    public Guid FormFieldId { get; private set; }
    public string? TextValue { get; private set; }
    public decimal? NumericValue { get; private set; }
    public DateTime? DateValue { get; private set; }
    public bool? BooleanValue { get; private set; }
    public string? JsonValue { get; private set; }

    // Navigation
    public FormResponse FormResponse { get; private set; } = null!;
    public FormField FormField { get; private set; } = null!;

    private FormFieldValue() { }

    public static FormFieldValue Create(
        Guid formResponseId,
        Guid formFieldId,
        string? textValue = null,
        decimal? numericValue = null,
        DateTime? dateValue = null,
        bool? booleanValue = null,
        string? jsonValue = null)
    {
        return new FormFieldValue
        {
            Id = Guid.NewGuid(),
            FormResponseId = formResponseId,
            FormFieldId = formFieldId,
            TextValue = textValue,
            NumericValue = numericValue,
            DateValue = dateValue,
            BooleanValue = booleanValue,
            JsonValue = jsonValue,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(
        string? textValue,
        decimal? numericValue,
        DateTime? dateValue,
        bool? booleanValue,
        string? jsonValue)
    {
        TextValue = textValue;
        NumericValue = numericValue;
        DateValue = dateValue;
        BooleanValue = booleanValue;
        JsonValue = jsonValue;
        SetUpdated();
    }

    /// <summary>
    /// Get the value as a display string
    /// </summary>
    public string GetDisplayValue()
    {
        if (!string.IsNullOrEmpty(TextValue)) return TextValue;
        if (NumericValue.HasValue) return NumericValue.Value.ToString();
        if (DateValue.HasValue) return DateValue.Value.ToString("yyyy-MM-dd");
        if (BooleanValue.HasValue) return BooleanValue.Value ? "Yes" : "No";
        if (!string.IsNullOrEmpty(JsonValue)) return JsonValue;
        return string.Empty;
    }
}
