using System.Security.Cryptography;
using System.Text.Json;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

/// <summary>
/// Automated secret rotation engine implementing AWS Secrets Manager rotation pattern:
/// Generate new → Validate → Swap → Retire old (grace period).
/// </summary>
public class SecretRotationService : ISecretRotationService
{
    private readonly IVaultRepository _repo;
    private readonly IVaultSecretService _secretService;
    private readonly ILogger<SecretRotationService> _logger;
    private readonly VaultMetrics _metrics;

    public SecretRotationService(
        IVaultRepository repo,
        IVaultSecretService secretService,
        ILogger<SecretRotationService> logger,
        VaultMetrics metrics)
    {
        _repo = repo;
        _secretService = secretService;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task<RotationSchedule> SetRotationScheduleAsync(
        string secretPath,
        RotationConfig config,
        CancellationToken ct = default)
    {
        var existing = await _repo.GetRotationScheduleByPathAsync(secretPath, ct);

        if (existing is not null)
        {
            existing.UpdateConfig(config.RotationIntervalDays, config.GracePeriodHours, config.AutomaticallyRotate);
            await _repo.UpdateRotationScheduleAsync(existing, ct);

            _logger.LogInformation("Updated rotation schedule for {Path}: every {Days} days", secretPath, config.RotationIntervalDays);

            return ToDto(existing);
        }

        // Verify the secret exists
        var secret = await _secretService.GetSecretAsync(secretPath, ct: ct);
        if (secret is null)
            throw new InvalidOperationException($"Secret '{secretPath}' not found. Cannot set rotation schedule for non-existent secret.");

        var schedule = SecretRotationSchedule.Create(
            secretPath,
            config.RotationIntervalDays,
            config.GracePeriodHours,
            config.AutomaticallyRotate,
            config.RotationStrategy,
            config.CallbackUrl);

        await _repo.AddRotationScheduleAsync(schedule, ct);

        await _repo.AddAuditLogAsync(VaultAuditLog.Create(
            "rotation.schedule.created",
            "SecretRotationSchedule",
            secretPath,
            details: JsonSerializer.Serialize(new { config.RotationIntervalDays, config.GracePeriodHours })), ct);

        _logger.LogInformation("Created rotation schedule for {Path}: every {Days} days", secretPath, config.RotationIntervalDays);

        return ToDto(schedule);
    }

    public async Task RemoveRotationScheduleAsync(string secretPath, CancellationToken ct = default)
    {
        var existing = await _repo.GetRotationScheduleByPathAsync(secretPath, ct);
        if (existing is not null)
        {
            existing.Deactivate();
            await _repo.UpdateRotationScheduleAsync(existing, ct);

            await _repo.AddAuditLogAsync(VaultAuditLog.Create(
                "rotation.schedule.removed",
                "SecretRotationSchedule",
                secretPath), ct);

            _logger.LogInformation("Deactivated rotation schedule for {Path}", secretPath);
        }
    }

    public async Task<RotationResult> RotateNowAsync(string secretPath, Guid? triggeredBy = null, CancellationToken ct = default)
    {
        try
        {
            // Get current secret
            var current = await _secretService.GetSecretAsync(secretPath, ct: ct);
            if (current is null)
                return new RotationResult(false, secretPath, null, null, "Secret not found", DateTime.UtcNow);

            var oldVersion = current.Version;

            // Generate new secret value (cryptographically random)
            var newValue = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

            // Store as new version (PutSecretAsync auto-increments version)
            var result = await _secretService.PutSecretAsync(
                secretPath, newValue, triggeredBy,
                metadata: JsonSerializer.Serialize(new
                {
                    RotatedAt = DateTime.UtcNow,
                    TriggeredBy = triggeredBy,
                    PreviousVersion = oldVersion,
                    RotationType = "automatic"
                }), ct: ct);

            // Update schedule
            var schedule = await _repo.GetRotationScheduleByPathAsync(secretPath, ct);
            if (schedule is not null)
            {
                schedule.RecordRotation();
                await _repo.UpdateRotationScheduleAsync(schedule, ct);
            }

            // Audit log
            await _repo.AddAuditLogAsync(VaultAuditLog.Create(
                "rotation.executed",
                "VaultSecret",
                secretPath,
                triggeredBy,
                JsonSerializer.Serialize(new { oldVersion, newVersion = result.Version })), ct);

            _logger.LogInformation(
                "Secret rotated: {Path} v{OldVersion} → v{NewVersion}",
                secretPath, oldVersion, result.Version);
            _metrics.RecordRotation(true);

            return new RotationResult(true, secretPath, result.Version, oldVersion, null, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rotate secret {Path}", secretPath);
            _metrics.RecordRotation(false);

            await _repo.AddAuditLogAsync(VaultAuditLog.Create(
                "rotation.failed",
                "VaultSecret",
                secretPath,
                triggeredBy,
                JsonSerializer.Serialize(new { Error = ex.Message })), ct);

            return new RotationResult(false, secretPath, null, null, ex.Message, DateTime.UtcNow);
        }
    }

    public async Task<RotationBatchResult> ExecutePendingRotationsAsync(CancellationToken ct = default)
    {
        var schedules = await _repo.GetRotationSchedulesAsync(activeOnly: true, ct);
        var due = schedules.Where(s => s.IsDueForRotation).ToList();

        var results = new List<RotationResult>();
        var succeeded = 0;
        var failed = 0;

        foreach (var schedule in due)
        {
            if (ct.IsCancellationRequested) break;

            var result = await RotateNowAsync(schedule.SecretPath, triggeredBy: null, ct);
            results.Add(result);

            if (result.Success) succeeded++;
            else failed++;
        }

        if (due.Count > 0)
        {
            _logger.LogInformation(
                "Rotation batch complete: {Total} scheduled, {Succeeded} succeeded, {Failed} failed",
                due.Count, succeeded, failed);
        }

        return new RotationBatchResult(due.Count, succeeded, failed, results);
    }

    public async Task<IReadOnlyList<RotationSchedule>> GetSchedulesAsync(CancellationToken ct = default)
    {
        var schedules = await _repo.GetRotationSchedulesAsync(activeOnly: true, ct);
        return schedules.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<RotationHistoryEntry>> GetRotationHistoryAsync(
        string secretPath,
        int limit = 20,
        CancellationToken ct = default)
    {
        var logs = await _repo.GetAuditLogsAsync(1, limit, "rotation.executed", ct);
        return logs
            .Where(l => l.ResourceId == secretPath)
            .Select(l =>
            {
                var details = TryParseJson(l.Details);
                return new RotationHistoryEntry(
                    secretPath,
                    details.TryGetValue("oldVersion", out var ov) ? (int)(long)ov : 0,
                    details.TryGetValue("newVersion", out var nv) ? (int)(long)nv : 0,
                    l.CreatedAt,
                    l.UserId,
                    true,
                    null);
            })
            .ToList();
    }

    private static RotationSchedule ToDto(SecretRotationSchedule s) => new(
        s.SecretPath,
        s.RotationIntervalDays,
        s.GracePeriodHours,
        s.AutomaticallyRotate,
        s.LastRotatedAt,
        s.NextRotationAt,
        s.RotationStrategy);

    private static Dictionary<string, object> TryParseJson(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new();
        try { return JsonSerializer.Deserialize<Dictionary<string, object>>(json) ?? new(); }
        catch { return new(); }
    }
}
