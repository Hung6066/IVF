namespace IVF.Application.Common.Interfaces;

/// <summary>
/// Manages DEK (Data Encryption Key) rotation with versioning.
/// Supports Google-style sharded encryption: version N encrypts new data,
/// versions N..1 are available for decryption until re-encryption completes.
/// </summary>
public interface IDekRotationService
{
    /// <summary>
    /// Rotate the DEK for a given purpose. Creates a new version and
    /// stores the old version with a suffix for decryption during migration.
    /// </summary>
    Task<DekRotationResult> RotateDekAsync(string dekPurpose, CancellationToken ct = default);

    /// <summary>
    /// Re-encrypt all data in a table that was encrypted with an older DEK version.
    /// Processes in batches with progress tracking.
    /// </summary>
    Task<ReEncryptionResult> ReEncryptTableAsync(string tableName, string dekPurpose, int batchSize = 100, CancellationToken ct = default);

    /// <summary>
    /// Get current DEK version info for a purpose.
    /// </summary>
    Task<DekVersionInfo?> GetDekVersionInfoAsync(string dekPurpose, CancellationToken ct = default);

    /// <summary>
    /// Get re-encryption progress for all tables.
    /// </summary>
    Task<IReadOnlyList<ReEncryptionProgress>> GetReEncryptionProgressAsync(CancellationToken ct = default);
}

public record DekRotationResult(
    bool Success,
    string DekPurpose,
    int NewVersion,
    int? PreviousVersion,
    string? Error);

public record DekVersionInfo(
    string DekPurpose,
    int CurrentVersion,
    DateTime? LastRotatedAt,
    int OldVersionsKept);

public record ReEncryptionResult(
    string TableName,
    int TotalRows,
    int ReEncrypted,
    int Failed,
    int Skipped,
    TimeSpan Duration);

public record ReEncryptionProgress(
    string TableName,
    string DekPurpose,
    int TotalRows,
    int ProcessedRows,
    bool IsComplete,
    DateTime? LastProcessedAt);
