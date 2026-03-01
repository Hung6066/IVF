namespace IVF.Application.Common.Interfaces;

/// <summary>
/// Vault-specific disaster recovery — encrypted backup/restore of vault state
/// (secrets, policies, encryption configs, settings) with integrity verification.
/// </summary>
public interface IVaultDrService
{
    /// <summary>Create an encrypted backup of all vault state.</summary>
    Task<VaultBackupResult> BackupAsync(string backupKey, CancellationToken ct = default);

    /// <summary>Restore vault state from an encrypted backup.</summary>
    Task<VaultRestoreResult> RestoreAsync(byte[] backupData, string backupKey, CancellationToken ct = default);

    /// <summary>Verify backup integrity without restoring.</summary>
    Task<VaultBackupValidation> ValidateBackupAsync(byte[] backupData, string backupKey, CancellationToken ct = default);

    /// <summary>Get current DR readiness status.</summary>
    Task<DrReadinessStatus> GetReadinessAsync(CancellationToken ct = default);
}

public sealed record VaultBackupResult(
    bool Success,
    byte[] BackupData,
    string BackupId,
    DateTime CreatedAt,
    int SecretsCount,
    int PoliciesCount,
    int SettingsCount,
    int EncryptionConfigsCount,
    string IntegrityHash);

public sealed record VaultRestoreResult(
    bool Success,
    string? Error,
    int SecretsRestored,
    int PoliciesRestored,
    int SettingsRestored,
    int EncryptionConfigsRestored);

public sealed record VaultBackupValidation(
    bool Valid,
    string? Error,
    string BackupId,
    DateTime CreatedAt,
    string IntegrityHash);

public sealed record DrReadinessStatus(
    bool AutoUnsealConfigured,
    bool EncryptionActive,
    int ActiveSecrets,
    int ActivePolicies,
    DateTime? LastBackupAt,
    string ReadinessGrade);
