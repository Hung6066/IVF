namespace IVF.Application.Common.Interfaces;

/// <summary>
/// Automated secret rotation engine — AWS Secrets Manager rotation Lambda pattern.
/// Supports configurable schedules (30/60/90 days) and multi-step rotation workflow:
/// Generate new → Validate → Swap → Retire old (grace period).
/// </summary>
public interface ISecretRotationService
{
    /// <summary>
    /// Set a rotation schedule for a vault secret path.
    /// </summary>
    Task<RotationSchedule> SetRotationScheduleAsync(
        string secretPath,
        RotationConfig config,
        CancellationToken ct = default);

    /// <summary>
    /// Remove the rotation schedule for a secret path.
    /// </summary>
    Task RemoveRotationScheduleAsync(string secretPath, CancellationToken ct = default);

    /// <summary>
    /// Trigger immediate rotation for a specific secret.
    /// </summary>
    Task<RotationResult> RotateNowAsync(string secretPath, Guid? triggeredBy = null, CancellationToken ct = default);

    /// <summary>
    /// Execute all pending rotations (called by background service).
    /// </summary>
    Task<RotationBatchResult> ExecutePendingRotationsAsync(CancellationToken ct = default);

    /// <summary>
    /// Get all active rotation schedules.
    /// </summary>
    Task<IReadOnlyList<RotationSchedule>> GetSchedulesAsync(CancellationToken ct = default);

    /// <summary>
    /// Get rotation history for a secret path.
    /// </summary>
    Task<IReadOnlyList<RotationHistoryEntry>> GetRotationHistoryAsync(
        string secretPath,
        int limit = 20,
        CancellationToken ct = default);
}

/// <summary>
/// Configuration for automated secret rotation.
/// </summary>
public record RotationConfig(
    int RotationIntervalDays,
    int GracePeriodHours = 24,
    bool AutomaticallyRotate = true,
    string? RotationStrategy = null, // "generate" | "callback"
    string? CallbackUrl = null);

/// <summary>
/// Persisted rotation schedule for a secret.
/// </summary>
public record RotationSchedule(
    string SecretPath,
    int RotationIntervalDays,
    int GracePeriodHours,
    bool AutomaticallyRotate,
    DateTime? LastRotatedAt,
    DateTime NextRotationAt,
    string? RotationStrategy);

/// <summary>
/// Result of a single secret rotation.
/// </summary>
public record RotationResult(
    bool Success,
    string SecretPath,
    int? NewVersion,
    int? OldVersion,
    string? Error,
    DateTime RotatedAt);

/// <summary>
/// Result of batch rotation execution.
/// </summary>
public record RotationBatchResult(
    int TotalScheduled,
    int Succeeded,
    int Failed,
    IReadOnlyList<RotationResult> Results);

/// <summary>
/// Historical record of a rotation event.
/// </summary>
public record RotationHistoryEntry(
    string SecretPath,
    int OldVersion,
    int NewVersion,
    DateTime RotatedAt,
    Guid? TriggeredBy,
    bool Success,
    string? Error);
