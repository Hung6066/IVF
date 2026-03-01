namespace IVF.Domain.Enums;

/// <summary>
/// Security event severity levels aligned with CVSS scoring.
/// </summary>
public enum SecuritySeverity
{
    Info = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

/// <summary>
/// Threat categories aligned with MITRE ATT&CK framework.
/// </summary>
public enum ThreatCategory
{
    None = 0,
    Reconnaissance,
    InitialAccess,
    CredentialAccess,
    LateralMovement,
    PrivilegeEscalation,
    DataExfiltration,
    Evasion,
    Impact,
    Persistence
}

/// <summary>
/// Actions the system can take in response to a Zero Trust violation.
/// </summary>
public enum ZeroTrustAction
{
    Allow,
    AllowWithMonitoring,
    RequireStepUp,
    RequireMfa,
    RateLimit,
    BlockTemporary,
    BlockPermanent,
    Quarantine,
    AlertOnly
}

/// <summary>
/// Device trust status aligned with Microsoft Endpoint Manager / Google BeyondCorp.
/// </summary>
public enum DeviceTrustLevel
{
    Unknown = 0,
    Untrusted = 1,
    PartiallyTrusted = 2,
    Trusted = 3,
    FullyManaged = 4
}
