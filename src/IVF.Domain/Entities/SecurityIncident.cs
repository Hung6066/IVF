using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Security incident for automated response workflows.
/// Inspired by PagerDuty + Microsoft Sentinel incident management.
/// Created automatically when SecurityEvent matches IncidentResponseRule criteria.
/// </summary>
public class SecurityIncident : BaseEntity
{
    public string IncidentType { get; private set; } = string.Empty; // brute_force, impossible_travel, account_takeover, etc.
    public string Severity { get; private set; } = "Medium"; // Low, Medium, High, Critical
    public string Status { get; private set; } = "Open"; // Open, Investigating, Resolved, Closed, FalsePositive
    public Guid? UserId { get; private set; }
    public string? Username { get; private set; }
    public string? IpAddress { get; private set; }
    public string? Description { get; private set; }
    public string? Details { get; private set; } // JSON: event correlation data
    public string? ActionsTaken { get; private set; } // JSON: ["account_locked","sessions_revoked","admin_notified"]
    public Guid? AssignedTo { get; private set; }
    public string? Resolution { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public Guid? ResolvedBy { get; private set; }
    public string? RelatedEventIds { get; private set; } // JSON: correlated SecurityEvent Ids

    private SecurityIncident() { }

    public static SecurityIncident Create(
        string incidentType,
        string severity,
        Guid? userId,
        string? username,
        string? ipAddress,
        string? description,
        string? details = null,
        string? relatedEventIds = null)
    {
        return new SecurityIncident
        {
            IncidentType = incidentType,
            Severity = severity,
            UserId = userId,
            Username = username,
            IpAddress = ipAddress,
            Description = description,
            Details = details,
            RelatedEventIds = relatedEventIds
        };
    }

    public void Investigate(Guid assignedTo)
    {
        Status = "Investigating";
        AssignedTo = assignedTo;
        SetUpdated();
    }

    public void RecordActions(string actionsTakenJson)
    {
        ActionsTaken = actionsTakenJson;
        SetUpdated();
    }

    public void Resolve(string resolution, Guid resolvedBy)
    {
        Status = "Resolved";
        Resolution = resolution;
        ResolvedAt = DateTime.UtcNow;
        ResolvedBy = resolvedBy;
        SetUpdated();
    }

    public void Close()
    {
        Status = "Closed";
        SetUpdated();
    }

    public void MarkFalsePositive(string reason, Guid markedBy)
    {
        Status = "FalsePositive";
        Resolution = reason;
        ResolvedAt = DateTime.UtcNow;
        ResolvedBy = markedBy;
        SetUpdated();
    }
}

/// <summary>
/// Rules mapping security event patterns to automated incident response actions.
/// When a SecurityEvent matches a rule's criteria, an incident is created and actions executed.
/// </summary>
public class IncidentResponseRule : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsEnabled { get; private set; } = true;
    public int Priority { get; private set; } // Lower = evaluated first

    // Trigger conditions
    public string? TriggerEventTypes { get; private set; } // JSON: ["AUTH_BRUTE_FORCE","THREAT_IMPOSSIBLE_TRAVEL"]
    public string? TriggerSeverities { get; private set; } // JSON: ["High","Critical"]
    public int? TriggerThreshold { get; private set; } // Number of events within window to trigger
    public int? TriggerWindowMinutes { get; private set; } // Sliding window for threshold

    // Automated actions (JSON array)
    public string Actions { get; private set; } = "[]"; // JSON: ["lock_account","revoke_sessions","notify_admin","block_ip"]

    // Incident creation config
    public string IncidentSeverity { get; private set; } = "High";
    public string? NotifyRoles { get; private set; } // JSON: ["Admin"] — roles to notify

    public Guid? CreatedBy { get; private set; }

    private IncidentResponseRule() { }

    public static IncidentResponseRule Create(
        string name,
        string? description,
        int priority,
        string? triggerEventTypes,
        string? triggerSeverities,
        string actions,
        string incidentSeverity,
        Guid? createdBy)
    {
        return new IncidentResponseRule
        {
            Name = name,
            Description = description,
            Priority = priority,
            TriggerEventTypes = triggerEventTypes,
            TriggerSeverities = triggerSeverities,
            Actions = actions,
            IncidentSeverity = incidentSeverity,
            CreatedBy = createdBy
        };
    }

    public void Update(
        string name,
        string? description,
        int priority,
        string? triggerEventTypes,
        string? triggerSeverities,
        int? triggerThreshold,
        int? triggerWindowMinutes,
        string actions,
        string incidentSeverity,
        string? notifyRoles,
        bool isEnabled)
    {
        Name = name;
        Description = description;
        Priority = priority;
        TriggerEventTypes = triggerEventTypes;
        TriggerSeverities = triggerSeverities;
        TriggerThreshold = triggerThreshold;
        TriggerWindowMinutes = triggerWindowMinutes;
        Actions = actions;
        IncidentSeverity = incidentSeverity;
        NotifyRoles = notifyRoles;
        IsEnabled = isEnabled;
        SetUpdated();
    }
}
