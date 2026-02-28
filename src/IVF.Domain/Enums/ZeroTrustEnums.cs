namespace IVF.Domain.Enums;

/// <summary>
/// Vault actions that require Zero Trust evaluation
/// </summary>
public enum ZTVaultAction
{
    VaultUnseal,
    SecretRead,
    SecretWrite,
    SecretDelete,
    SecretExport,
    KeyRotate,
    BreakGlassAccess
}

/// <summary>
/// Authentication levels from lowest to highest
/// </summary>
public enum AuthLevel
{
    None = 0,
    Session = 1,
    FreshSession = 2,
    Password = 3,
    MFA = 4,
    Biometric = 5
}

/// <summary>
/// Risk levels for device and context assessment
/// </summary>
public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}
