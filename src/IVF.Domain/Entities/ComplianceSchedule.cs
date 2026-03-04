using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Recurring compliance task scheduler for Phase 4 ongoing compliance operations.
/// Tracks monthly, quarterly, and annual compliance activities across all 7 frameworks.
/// </summary>
public class ComplianceSchedule : BaseEntity
{
    public string TaskName { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Framework { get; private set; } = string.Empty; // SOC2, ISO27001, HIPAA, GDPR, HITRUST, NIST_AI_RMF, ISO42001, ALL
    public string Frequency { get; private set; } = string.Empty; // Daily, Weekly, Monthly, Quarterly, SemiAnnual, Annual, OnChange
    public string Category { get; private set; } = string.Empty; // Monitoring, Audit, Assessment, Training, Testing, Review
    public string Owner { get; private set; } = string.Empty; // Role or team responsible
    public Guid? AssignedUserId { get; private set; }
    public DateTime? LastCompletedAt { get; private set; }
    public Guid? LastCompletedBy { get; private set; }
    public string? LastCompletionNotes { get; private set; }
    public DateTime? NextDueDate { get; private set; }
    public string Status { get; private set; } = ScheduleStatus.Active;
    public int CompletionCount { get; private set; }
    public bool AutoReminder { get; private set; } = true;
    public int ReminderDaysBefore { get; private set; } = 7;
    public string? EvidenceRequired { get; private set; } // Description of evidence to collect
    public string? RelatedDocumentId { get; private set; }
    public string Priority { get; private set; } = "Medium"; // Low, Medium, High, Critical

    private ComplianceSchedule() { }

    public static ComplianceSchedule Create(
        string taskName,
        string description,
        string framework,
        string frequency,
        string category,
        string owner,
        DateTime nextDueDate,
        string? evidenceRequired = null,
        string priority = "Medium")
    {
        return new ComplianceSchedule
        {
            TaskName = taskName,
            Description = description,
            Framework = framework,
            Frequency = frequency,
            Category = category,
            Owner = owner,
            NextDueDate = nextDueDate,
            EvidenceRequired = evidenceRequired,
            Priority = priority
        };
    }

    public void MarkCompleted(Guid completedBy, string? notes)
    {
        LastCompletedAt = DateTime.UtcNow;
        LastCompletedBy = completedBy;
        LastCompletionNotes = notes;
        CompletionCount++;
        NextDueDate = CalculateNextDueDate();
        SetUpdated();
    }

    public void Assign(Guid userId)
    {
        AssignedUserId = userId;
        SetUpdated();
    }

    public void Pause()
    {
        Status = ScheduleStatus.Paused;
        SetUpdated();
    }

    public void Resume()
    {
        Status = ScheduleStatus.Active;
        SetUpdated();
    }

    public void UpdateSchedule(string frequency, DateTime nextDueDate, int reminderDays)
    {
        Frequency = frequency;
        NextDueDate = nextDueDate;
        ReminderDaysBefore = reminderDays;
        SetUpdated();
    }

    public bool IsOverdue => NextDueDate.HasValue
                             && DateTime.UtcNow > NextDueDate.Value
                             && Status == ScheduleStatus.Active;

    public bool IsUpcoming => NextDueDate.HasValue
                              && DateTime.UtcNow <= NextDueDate.Value
                              && (NextDueDate.Value - DateTime.UtcNow).Days <= ReminderDaysBefore
                              && Status == ScheduleStatus.Active;

    private DateTime? CalculateNextDueDate()
    {
        if (!NextDueDate.HasValue) return null;
        return Frequency switch
        {
            ComplianceFrequency.Daily => NextDueDate.Value.AddDays(1),
            ComplianceFrequency.Weekly => NextDueDate.Value.AddDays(7),
            ComplianceFrequency.Monthly => NextDueDate.Value.AddMonths(1),
            ComplianceFrequency.Quarterly => NextDueDate.Value.AddMonths(3),
            ComplianceFrequency.SemiAnnual => NextDueDate.Value.AddMonths(6),
            ComplianceFrequency.Annual => NextDueDate.Value.AddYears(1),
            _ => null // OnChange — no automatic recurrence
        };
    }
}

public static class ScheduleStatus
{
    public const string Active = "Active";
    public const string Paused = "Paused";
    public const string Retired = "Retired";
}

public static class ComplianceFrequency
{
    public const string Daily = "Daily";
    public const string Weekly = "Weekly";
    public const string Monthly = "Monthly";
    public const string Quarterly = "Quarterly";
    public const string SemiAnnual = "SemiAnnual";
    public const string Annual = "Annual";
    public const string OnChange = "OnChange";
}

public static class ComplianceCategory
{
    public const string Monitoring = "Monitoring";
    public const string Audit = "Audit";
    public const string Assessment = "Assessment";
    public const string Training = "Training";
    public const string Testing = "Testing";
    public const string Review = "Review";
}
