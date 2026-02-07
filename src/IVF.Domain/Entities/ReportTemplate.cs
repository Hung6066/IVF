using IVF.Domain.Common;
using IVF.Domain.Enums;

namespace IVF.Domain.Entities;

/// <summary>
/// Report template configuration for generating dynamic reports from form responses
/// </summary>
public class ReportTemplate : BaseEntity
{
    public Guid FormTemplateId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string ConfigurationJson { get; private set; } = "{}";
    public ReportType ReportType { get; private set; }
    public bool IsPublished { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    // Navigation
    public FormTemplate FormTemplate { get; private set; } = null!;
    public User CreatedByUser { get; private set; } = null!;

    private ReportTemplate() { }

    public static ReportTemplate Create(
        Guid formTemplateId,
        string name,
        ReportType reportType,
        Guid createdByUserId,
        string? description = null,
        string? configurationJson = null)
    {
        return new ReportTemplate
        {
            Id = Guid.NewGuid(),
            FormTemplateId = formTemplateId,
            Name = name,
            Description = description,
            ReportType = reportType,
            ConfigurationJson = configurationJson ?? "{}",
            IsPublished = false,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, string? description, ReportType reportType, string configurationJson)
    {
        Name = name;
        Description = description;
        ReportType = reportType;
        ConfigurationJson = configurationJson;
        SetUpdated();
    }

    public void UpdateConfiguration(string configurationJson)
    {
        ConfigurationJson = configurationJson;
        SetUpdated();
    }

    public void Publish()
    {
        IsPublished = true;
        SetUpdated();
    }

    public void Unpublish()
    {
        IsPublished = false;
        SetUpdated();
    }
}
