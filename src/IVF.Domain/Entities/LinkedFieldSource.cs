using IVF.Domain.Common;
using IVF.Domain.Enums;

namespace IVF.Domain.Entities;

/// <summary>
/// Explicit field-to-field data link configuration.
/// When opening TargetField's form, its value can be pre-filled from SourceField's latest response.
/// </summary>
public class LinkedFieldSource : BaseEntity
{
    /// <summary>The field that receives the linked value (target form field)</summary>
    public Guid TargetFieldId { get; private set; }

    /// <summary>The template that contains the source data</summary>
    public Guid SourceTemplateId { get; private set; }

    /// <summary>The specific field in the source template to pull data from</summary>
    public Guid SourceFieldId { get; private set; }

    /// <summary>How the linked data should be presented: AutoFill, Suggest, Reference, Copy</summary>
    public DataFlowType FlowType { get; private set; }

    /// <summary>Higher priority wins when multiple sources exist for the same target field</summary>
    public int Priority { get; private set; }

    /// <summary>Whether this link is active</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>Optional description/label for this link</summary>
    public string? Description { get; private set; }

    // Navigation
    public FormField TargetField { get; private set; } = null!;
    public FormTemplate SourceTemplate { get; private set; } = null!;
    public FormField SourceField { get; private set; } = null!;

    private LinkedFieldSource() { }

    public static LinkedFieldSource Create(
        Guid targetFieldId,
        Guid sourceTemplateId,
        Guid sourceFieldId,
        DataFlowType flowType = DataFlowType.Suggest,
        int priority = 0,
        string? description = null)
    {
        return new LinkedFieldSource
        {
            Id = Guid.NewGuid(),
            TargetFieldId = targetFieldId,
            SourceTemplateId = sourceTemplateId,
            SourceFieldId = sourceFieldId,
            FlowType = flowType,
            Priority = priority,
            IsActive = true,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(
        Guid? sourceTemplateId,
        Guid? sourceFieldId,
        DataFlowType? flowType,
        int? priority,
        string? description)
    {
        if (sourceTemplateId.HasValue) SourceTemplateId = sourceTemplateId.Value;
        if (sourceFieldId.HasValue) SourceFieldId = sourceFieldId.Value;
        if (flowType.HasValue) FlowType = flowType.Value;
        if (priority.HasValue) Priority = priority.Value;
        Description = description;
        SetUpdated();
    }

    public void Activate() { IsActive = true; SetUpdated(); }
    public void Deactivate() { IsActive = false; SetUpdated(); }
}
