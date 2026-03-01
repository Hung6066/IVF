using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

/// <summary>
/// DEK rotation service with versioning.
/// - Current DEK stored at "dek-{purpose}" (always latest)
/// - Previous versions stored at "dek-{purpose}-v{N}"
/// - Version metadata stored in vault settings as "dek-version-{purpose}"
/// </summary>
public class DekRotationService : IDekRotationService
{
    private readonly IKeyVaultService _kvService;
    private readonly IVaultRepository _vaultRepo;
    private readonly IVaultDecryptionService _decryptionService;
    private readonly VaultMetrics _metrics;
    private readonly ILogger<DekRotationService> _logger;

    private const int KeyLength = 32; // 256-bit AES key

    public DekRotationService(
        IKeyVaultService kvService,
        IVaultRepository vaultRepo,
        IVaultDecryptionService decryptionService,
        VaultMetrics metrics,
        ILogger<DekRotationService> logger)
    {
        _kvService = kvService;
        _vaultRepo = vaultRepo;
        _decryptionService = decryptionService;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<DekRotationResult> RotateDekAsync(string dekPurpose, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dekPurpose);

        try
        {
            var dekName = $"dek-{dekPurpose.ToLowerInvariant()}";
            var versionKey = $"dek-version-{dekPurpose.ToLowerInvariant()}";

            // Get current version info
            var currentVersion = await GetCurrentVersionAsync(versionKey, ct);

            // Archive the current DEK before overwriting
            var currentDekBase64 = await TryGetSecretAsync(dekName, ct);
            if (!string.IsNullOrEmpty(currentDekBase64))
            {
                var archiveName = $"{dekName}-v{currentVersion}";
                await _kvService.SetSecretAsync(archiveName, currentDekBase64, ct);
                _logger.LogInformation("Archived DEK {DekName} as version {Version}", dekName, currentVersion);
            }

            // Generate and store the new DEK
            var newVersion = currentVersion + 1;
            var newDek = RandomNumberGenerator.GetBytes(KeyLength);
            await _kvService.SetSecretAsync(dekName, Convert.ToBase64String(newDek), ct);

            // Update version metadata
            var versionMeta = JsonSerializer.Serialize(new DekVersionMetadata
            {
                CurrentVersion = newVersion,
                RotatedAt = DateTime.UtcNow,
                OldVersionsKept = currentVersion
            });
            await _vaultRepo.SaveSettingAsync(versionKey, versionMeta, ct);

            // Audit log
            await _vaultRepo.AddAuditLogAsync(VaultAuditLog.Create(
                "dek.rotate",
                "DEK",
                dekPurpose,
                details: $"{{\"previousVersion\":{currentVersion},\"newVersion\":{newVersion}}}"));

            _metrics.RecordRotation(true);
            _logger.LogInformation("Rotated DEK {DekPurpose} from v{Old} to v{New}", dekPurpose, currentVersion, newVersion);

            return new DekRotationResult(true, dekPurpose, newVersion, currentVersion, null);
        }
        catch (Exception ex)
        {
            _metrics.RecordRotation(false);
            _logger.LogError(ex, "Failed to rotate DEK {DekPurpose}", dekPurpose);
            return new DekRotationResult(false, dekPurpose, 0, null, ex.Message);
        }
    }

    public async Task<ReEncryptionResult> ReEncryptTableAsync(
        string tableName, string dekPurpose, int batchSize = 100, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(dekPurpose);

        var sw = Stopwatch.StartNew();
        var reEncrypted = 0;
        var failed = 0;
        var skipped = 0;

        try
        {
            // Get encryption config for this table
            var configs = await _vaultRepo.GetAllEncryptionConfigsAsync(ct);
            var config = configs.FirstOrDefault(c =>
                string.Equals(c.TableName, tableName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(c.DekPurpose, dekPurpose, StringComparison.OrdinalIgnoreCase));

            if (config is null || !config.IsEnabled)
            {
                _logger.LogWarning("No enabled encryption config found for table {Table}", tableName);
                return new ReEncryptionResult(tableName, 0, 0, 0, 0, sw.Elapsed);
            }

            var purpose = Enum.TryParse<KeyPurpose>(dekPurpose, true, out var kp)
                ? kp : KeyPurpose.Data;

            // Get all versioned DEKs for fallback decryption
            var versionKey = $"dek-version-{dekPurpose.ToLowerInvariant()}";
            var currentVersion = await GetCurrentVersionAsync(versionKey, ct);
            var dekName = $"dek-{dekPurpose.ToLowerInvariant()}";

            // Load current (new) DEK for re-encryption
            var currentDekBase64 = await _kvService.GetSecretAsync(dekName, ct);
            if (string.IsNullOrEmpty(currentDekBase64))
                throw new InvalidOperationException($"Current DEK for purpose '{dekPurpose}' not found");

            // Load all old DEK versions for decryption
            var oldDeks = new Dictionary<int, byte[]>();
            for (var v = 1; v <= currentVersion - 1; v++)
            {
                var oldDekBase64 = await TryGetSecretAsync($"{dekName}-v{v}", ct);
                if (!string.IsNullOrEmpty(oldDekBase64))
                    oldDeks[v] = Convert.FromBase64String(oldDekBase64);
            }

            // Also include the "current version - 1" DEK as the one data was encrypted with before rotation
            // This is what we need to decrypt with
            var totalRows = 0;

            // Re-encrypt using raw SQL via the vault repository
            // For each encrypted field, read all rows, attempt decrypt with old keys, re-encrypt with new key
            foreach (var field in config.EncryptedFields)
            {
                var rows = await ReadEncryptedFieldsAsync(tableName, field, ct);
                totalRows += rows.Count;

                foreach (var batch in rows.Chunk(batchSize))
                {
                    foreach (var row in batch)
                    {
                        ct.ThrowIfCancellationRequested();

                        if (!_decryptionService.IsEncrypted(row.Value))
                        {
                            skipped++;
                            continue;
                        }

                        try
                        {
                            // Decrypt with any available version (current purpose key handles this)
                            var plaintext = await _decryptionService.DecryptFieldAsync(row.Value!, dekPurpose, ct);

                            // If decryption returned the same value (failed silently), skip
                            if (plaintext == row.Value)
                            {
                                skipped++;
                                continue;
                            }

                            // Re-encrypt with the current (new) DEK
                            var encrypted = await _kvService.EncryptAsync(
                                Encoding.UTF8.GetBytes(plaintext), purpose, ct);

                            var newEncrypted = JsonSerializer.Serialize(new { c = encrypted.CiphertextBase64, iv = encrypted.IvBase64 });

                            await UpdateEncryptedFieldAsync(tableName, field, row.Id, newEncrypted, ct);
                            reEncrypted++;
                        }
                        catch (Exception ex)
                        {
                            failed++;
                            _logger.LogWarning(ex, "Failed to re-encrypt row {Id} field {Field} in {Table}",
                                row.Id, field, tableName);
                        }
                    }
                }
            }

            // Audit log
            await _vaultRepo.AddAuditLogAsync(VaultAuditLog.Create(
                "dek.reencrypt",
                "Table",
                tableName,
                details: JsonSerializer.Serialize(new
                {
                    dekPurpose,
                    totalRows,
                    reEncrypted,
                    failed,
                    skipped,
                    durationMs = sw.ElapsedMilliseconds
                })));

            _logger.LogInformation(
                "Re-encrypted {Table}: {ReEncrypted}/{Total} rows ({Failed} failed, {Skipped} skipped) in {Duration}ms",
                tableName, reEncrypted, totalRows, failed, skipped, sw.ElapsedMilliseconds);

            return new ReEncryptionResult(tableName, totalRows, reEncrypted, failed, skipped, sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Re-encryption failed for table {Table}", tableName);
            return new ReEncryptionResult(tableName, 0, reEncrypted, failed + 1, skipped, sw.Elapsed);
        }
    }

    public async Task<DekVersionInfo?> GetDekVersionInfoAsync(string dekPurpose, CancellationToken ct = default)
    {
        var versionKey = $"dek-version-{dekPurpose.ToLowerInvariant()}";
        var setting = await _vaultRepo.GetSettingAsync(versionKey, ct);

        if (setting is null)
        {
            // Check if DEK exists at all (version 1, never rotated)
            var dekName = $"dek-{dekPurpose.ToLowerInvariant()}";
            var dek = await TryGetSecretAsync(dekName, ct);
            if (string.IsNullOrEmpty(dek))
                return null;

            return new DekVersionInfo(dekPurpose, 1, null, 0);
        }

        var meta = JsonSerializer.Deserialize<DekVersionMetadata>(setting.ValueJson);
        if (meta is null)
            return new DekVersionInfo(dekPurpose, 1, null, 0);

        return new DekVersionInfo(dekPurpose, meta.CurrentVersion, meta.RotatedAt, meta.OldVersionsKept);
    }

    public async Task<IReadOnlyList<ReEncryptionProgress>> GetReEncryptionProgressAsync(CancellationToken ct = default)
    {
        var configs = await _vaultRepo.GetAllEncryptionConfigsAsync(ct);
        var progress = new List<ReEncryptionProgress>();

        foreach (var config in configs.Where(c => c.IsEnabled))
        {
            var totalRows = 0;
            var encryptedRows = 0;

            foreach (var field in config.EncryptedFields)
            {
                var rows = await ReadEncryptedFieldsAsync(config.TableName, field, ct);
                totalRows += rows.Count;
                encryptedRows += rows.Count(r => _decryptionService.IsEncrypted(r.Value));
            }

            progress.Add(new ReEncryptionProgress(
                config.TableName,
                config.DekPurpose,
                totalRows,
                encryptedRows,
                IsComplete: encryptedRows == totalRows,
                LastProcessedAt: null));
        }

        return progress;
    }

    // ─── Private Helpers ─────────────────────────────────

    private async Task<int> GetCurrentVersionAsync(string versionKey, CancellationToken ct)
    {
        var setting = await _vaultRepo.GetSettingAsync(versionKey, ct);
        if (setting is null) return 1;

        var meta = JsonSerializer.Deserialize<DekVersionMetadata>(setting.ValueJson);
        return meta?.CurrentVersion ?? 1;
    }

    private async Task<string?> TryGetSecretAsync(string name, CancellationToken ct)
    {
        try
        {
            return await _kvService.GetSecretAsync(name, ct);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Read encrypted field values from a table. Returns (Id, FieldValue) pairs.</summary>
    private async Task<List<EncryptedFieldRow>> ReadEncryptedFieldsAsync(
        string tableName, string fieldName, CancellationToken ct)
    {
        // Use vault repo to get encryption configs; actual data access would go through
        // the DbContext. For now, we track which rows need re-encryption via audit logs.
        // In a production implementation, this would use raw SQL or the DbContext directly.
        // Return empty list — re-encryption is driven by the caller providing data.
        return [];
    }

    /// <summary>Update a single encrypted field value for a row.</summary>
    private Task UpdateEncryptedFieldAsync(
        string tableName, string fieldName, Guid rowId, string newValue, CancellationToken ct)
    {
        // In production, this would use parameterized SQL via DbContext.Database.ExecuteSqlRawAsync
        // to update the specific field. Placeholder for now.
        return Task.CompletedTask;
    }

    private sealed class DekVersionMetadata
    {
        public int CurrentVersion { get; set; } = 1;
        public DateTime? RotatedAt { get; set; }
        public int OldVersionsKept { get; set; }
    }

    private sealed record EncryptedFieldRow(Guid Id, string? Value);
}
