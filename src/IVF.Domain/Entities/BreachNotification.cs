using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Tracks data breach incidents and notification requirements.
/// HIPAA §164.404-408: Notify individuals within 60 days, HHS if >500 records.
/// GDPR Art. 33-34: Notify DPA within 72 hours, data subjects without undue delay.
/// </summary>
public class BreachNotification : BaseEntity
{
    public Guid IncidentId { get; private set; }
    public string BreachType { get; private set; } = string.Empty; // unauthorized_access, data_loss, ransomware, insider_threat, system_compromise
    public string Severity { get; private set; } = "High"; // Low, Medium, High, Critical
    public string Status { get; private set; } = "Detected"; // Detected, Assessing, Containment, Notification, Resolved, Closed
    public DateTime DetectedAt { get; private set; }
    public DateTime? ContainedAt { get; private set; }
    public DateTime? NotificationDeadline { get; private set; }

    // Scope Assessment
    public int AffectedRecordCount { get; private set; }
    public string? AffectedDataTypes { get; private set; } // JSON: ["PHI","PII","financial","biometric"]
    public string? AffectedSystems { get; private set; } // JSON: ["patient_db","minio","auth"]
    public string? AffectedUserIds { get; private set; } // JSON: Guid[]

    // Root Cause
    public string? RootCause { get; private set; }
    public string? AttackVector { get; private set; } // phishing, brute_force, sql_injection, insider, third_party, unknown
    public string? MitreAttackId { get; private set; } // e.g., T1078

    // Notification Tracking
    public bool DpaNotified { get; private set; } // Data Protection Authority (GDPR Art. 33)
    public DateTime? DpaNotifiedAt { get; private set; }
    public string? DpaReference { get; private set; } // Reference number from authority

    public bool SubjectsNotified { get; private set; } // GDPR Art. 34 / HIPAA §164.404
    public DateTime? SubjectsNotifiedAt { get; private set; }
    public int SubjectsNotifiedCount { get; private set; }

    public bool HhsNotified { get; private set; } // HHS (HIPAA — required if >500 records)
    public DateTime? HhsNotifiedAt { get; private set; }

    public bool MediaNotified { get; private set; } // HIPAA §164.406 (>500 in a state)
    public DateTime? MediaNotifiedAt { get; private set; }

    // Remediation
    public string? RemediationSteps { get; private set; } // JSON array of steps taken
    public string? PreventionMeasures { get; private set; } // JSON array of future prevention
    public Guid? AssignedTo { get; private set; }
    public Guid? ResolvedBy { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public string? LessonsLearned { get; private set; }

    private BreachNotification() { }

    public static BreachNotification Create(
        Guid incidentId,
        string breachType,
        string severity,
        int affectedRecordCount,
        string? affectedDataTypes = null,
        string? affectedSystems = null,
        string? rootCause = null,
        string? attackVector = null)
    {
        var breach = new BreachNotification
        {
            IncidentId = incidentId,
            BreachType = breachType,
            Severity = severity,
            DetectedAt = DateTime.UtcNow,
            AffectedRecordCount = affectedRecordCount,
            AffectedDataTypes = affectedDataTypes,
            AffectedSystems = affectedSystems,
            RootCause = rootCause,
            AttackVector = attackVector
        };

        // GDPR: 72 hours from detection
        breach.NotificationDeadline = breach.DetectedAt.AddHours(72);

        return breach;
    }

    public void Assess(int affectedRecordCount, string? affectedDataTypes, string? affectedUserIds)
    {
        Status = "Assessing";
        AffectedRecordCount = affectedRecordCount;
        AffectedDataTypes = affectedDataTypes;
        AffectedUserIds = affectedUserIds;
        SetUpdated();
    }

    public void Contain(string? remediationSteps)
    {
        Status = "Containment";
        ContainedAt = DateTime.UtcNow;
        RemediationSteps = remediationSteps;
        SetUpdated();
    }

    public void NotifyDpa(string? reference)
    {
        DpaNotified = true;
        DpaNotifiedAt = DateTime.UtcNow;
        DpaReference = reference;
        SetUpdated();
    }

    public void NotifySubjects(int count)
    {
        SubjectsNotified = true;
        SubjectsNotifiedAt = DateTime.UtcNow;
        SubjectsNotifiedCount = count;
        Status = "Notification";
        SetUpdated();
    }

    public void NotifyHhs()
    {
        HhsNotified = true;
        HhsNotifiedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void NotifyMedia()
    {
        MediaNotified = true;
        MediaNotifiedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void Resolve(string? lessonsLearned, string? preventionMeasures, Guid resolvedBy)
    {
        Status = "Resolved";
        LessonsLearned = lessonsLearned;
        PreventionMeasures = preventionMeasures;
        ResolvedBy = resolvedBy;
        ResolvedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void Close()
    {
        Status = "Closed";
        SetUpdated();
    }

    public void AssignTo(Guid userId)
    {
        AssignedTo = userId;
        SetUpdated();
    }

    public void SetMitreAttackId(string mitreId)
    {
        MitreAttackId = mitreId;
        SetUpdated();
    }

    /// <summary>
    /// Whether GDPR 72-hour notification deadline is at risk.
    /// </summary>
    public bool IsDeadlineAtRisk() =>
        !DpaNotified && NotificationDeadline.HasValue && DateTime.UtcNow > NotificationDeadline.Value.AddHours(-12);

    /// <summary>
    /// Whether HIPAA HHS notification is required (>500 affected records).
    /// </summary>
    public bool RequiresHhsNotification() => AffectedRecordCount >= 500;

    /// <summary>
    /// Whether media notification is required (HIPAA: >500 in a jurisdiction).
    /// </summary>
    public bool RequiresMediaNotification() => AffectedRecordCount >= 500;
}
