using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Configuration Management Database (CMDB) asset register.
/// Tracks all information and system assets per ISO 27001 A.5.9 and SOC 2 CC6.1.
/// </summary>
public class AssetInventory : BaseEntity
{
    public string AssetName { get; private set; } = null!;
    public string AssetType { get; private set; } = null!; // database, server, application, storage, network, endpoint, api, backup
    public string Classification { get; private set; } = null!; // public, internal, confidential, restricted
    public string Owner { get; private set; } = null!;
    public string? Department { get; private set; }
    public string CriticalityLevel { get; private set; } = null!; // Low, Medium, High, Critical
    public string? Location { get; private set; } // on-premise, cloud, hybrid
    public string? Environment { get; private set; } // production, staging, development
    public string? Version { get; private set; }
    public string? IpAddress { get; private set; }
    public string? Hostname { get; private set; }
    public bool ContainsPhi { get; private set; }
    public bool ContainsPii { get; private set; }
    public bool HasEncryption { get; private set; }
    public bool HasBackup { get; private set; }
    public bool HasAccessControl { get; private set; }
    public bool HasMonitoring { get; private set; }
    public string Status { get; private set; } = null!; // Active, Inactive, Decommissioned, Maintenance
    public string? Dependencies { get; private set; } // JSON array of dependent asset names
    public string? SecurityControls { get; private set; } // JSON array of applied controls
    public string? Notes { get; private set; }
    public DateTime? LastAuditedAt { get; private set; }
    public DateTime? NextAuditDueAt { get; private set; }
    public DateTime? DecommissionedAt { get; private set; }

    private AssetInventory() { }

    public static AssetInventory Create(
        string assetName,
        string assetType,
        string classification,
        string owner,
        string criticalityLevel,
        bool containsPhi,
        bool containsPii,
        string? department = null,
        string? location = null,
        string? environment = null,
        string? version = null)
    {
        return new AssetInventory
        {
            AssetName = assetName,
            AssetType = assetType,
            Classification = classification,
            Owner = owner,
            CriticalityLevel = criticalityLevel,
            ContainsPhi = containsPhi,
            ContainsPii = containsPii,
            Department = department,
            Location = location,
            Environment = environment,
            Version = version,
            Status = AssetStatus.Active,
            HasEncryption = false,
            HasBackup = false,
            HasAccessControl = false,
            HasMonitoring = false
        };
    }

    public void UpdateSecurityPosture(bool hasEncryption, bool hasBackup, bool hasAccessControl, bool hasMonitoring)
    {
        HasEncryption = hasEncryption;
        HasBackup = hasBackup;
        HasAccessControl = hasAccessControl;
        HasMonitoring = hasMonitoring;
        SetUpdated();
    }

    public void Update(
        string assetName, string assetType, string classification,
        string owner, string criticalityLevel,
        bool containsPhi, bool containsPii,
        string? department, string? location, string? environment, string? version)
    {
        AssetName = assetName;
        AssetType = assetType;
        Classification = classification;
        Owner = owner;
        CriticalityLevel = criticalityLevel;
        ContainsPhi = containsPhi;
        ContainsPii = containsPii;
        Department = department;
        Location = location;
        Environment = environment;
        Version = version;
        SetUpdated();
    }

    public void MarkAudited(DateTime? nextAuditDue = null)
    {
        LastAuditedAt = DateTime.UtcNow;
        NextAuditDueAt = nextAuditDue ?? DateTime.UtcNow.AddMonths(
            CriticalityLevel is "Critical" or "High" ? 6 : 12);
        SetUpdated();
    }

    public void Decommission()
    {
        Status = AssetStatus.Decommissioned;
        DecommissionedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public bool IsOverdueForAudit() =>
        NextAuditDueAt.HasValue && NextAuditDueAt.Value < DateTime.UtcNow;

    public int CalculateRiskScore()
    {
        var score = 0;
        if (ContainsPhi) score += 30;
        if (ContainsPii) score += 20;
        if (!HasEncryption) score += 15;
        if (!HasBackup) score += 10;
        if (!HasAccessControl) score += 15;
        if (!HasMonitoring) score += 10;
        if (CriticalityLevel is "Critical") score += 20;
        else if (CriticalityLevel is "High") score += 10;
        if (IsOverdueForAudit()) score += 10;
        return Math.Min(score, 100);
    }
}

public static class AssetStatus
{
    public const string Active = "Active";
    public const string Inactive = "Inactive";
    public const string Decommissioned = "Decommissioned";
    public const string Maintenance = "Maintenance";
}

public static class AssetTypes
{
    public const string Database = "database";
    public const string Server = "server";
    public const string Application = "application";
    public const string Storage = "storage";
    public const string Network = "network";
    public const string Endpoint = "endpoint";
    public const string Api = "api";
    public const string Backup = "backup";
    public const string Container = "container";
    public const string Certificate = "certificate";
}
