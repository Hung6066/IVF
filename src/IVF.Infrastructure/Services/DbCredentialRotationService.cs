using System.Text.Json;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

/// <summary>
/// Google-style dual-credential rotation for the main application DB connection.
/// Maintains two slots (A/B). On rotation, the standby slot gets a fresh credential
/// and becomes active. The old slot enters a grace period before revocation.
/// State is persisted in VaultSettings under "db-rotation-state".
/// </summary>
public class DbCredentialRotationService : IDbCredentialRotationService
{
    private readonly IDynamicCredentialProvider _credProvider;
    private readonly IVaultRepository _vaultRepo;
    private readonly VaultMetrics _metrics;
    private readonly ILogger<DbCredentialRotationService> _logger;

    private const string StateKey = "db-rotation-state";
    private const int DefaultTtlSeconds = 86400; // 24 hours

    public DbCredentialRotationService(
        IDynamicCredentialProvider credProvider,
        IVaultRepository vaultRepo,
        VaultMetrics metrics,
        ILogger<DbCredentialRotationService> logger)
    {
        _credProvider = credProvider;
        _vaultRepo = vaultRepo;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<DbCredentialRotationResult> RotateAsync(CancellationToken ct = default)
    {
        try
        {
            var state = await LoadStateAsync(ct);

            // Determine which slot to rotate (the standby one)
            var rotatingSlot = state.ActiveSlot == "A" ? "B" : "A";

            // Revoke the old credential in the standby slot before creating a new one
            var oldCredId = rotatingSlot == "A" ? state.SlotACredentialId : state.SlotBCredentialId;
            if (oldCredId != Guid.Empty)
            {
                try
                {
                    await _credProvider.RevokeCredentialAsync(oldCredId, ct);
                    _logger.LogInformation("Revoked old credential in slot {Slot}", rotatingSlot);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to revoke old credential in slot {Slot}", rotatingSlot);
                }
            }

            // Generate a new credential for the standby slot
            var request = new DynamicCredentialRequest(
                state.DbHost, state.DbPort, state.DbName,
                state.AdminUsername, state.AdminPassword,
                DefaultTtlSeconds);

            var newCred = await _credProvider.GenerateCredentialAsync(request, ct);

            // Update state: new credential goes into standby slot, then standby becomes active
            if (rotatingSlot == "A")
            {
                state.SlotACredentialId = newCred.Id;
                state.SlotAUsername = newCred.Username;
                state.SlotAExpiresAt = newCred.ExpiresAt;
                state.SlotAConnectionString = newCred.ConnectionString;
            }
            else
            {
                state.SlotBCredentialId = newCred.Id;
                state.SlotBUsername = newCred.Username;
                state.SlotBExpiresAt = newCred.ExpiresAt;
                state.SlotBConnectionString = newCred.ConnectionString;
            }

            // Swap active slot
            state.ActiveSlot = rotatingSlot;
            state.LastRotatedAt = DateTime.UtcNow;
            state.RotationCount++;

            await SaveStateAsync(state, ct);

            // Store active connection string in vault config for VaultConfigurationProvider to pick up
            await _vaultRepo.SaveSettingAsync(
                "config/ConnectionStrings/DefaultConnection",
                JsonSerializer.Serialize(newCred.ConnectionString),
                ct);

            // Audit log
            await _vaultRepo.AddAuditLogAsync(VaultAuditLog.Create(
                "db.credential.rotate",
                "DbCredential",
                rotatingSlot,
                details: JsonSerializer.Serialize(new
                {
                    newUsername = newCred.Username,
                    expiresAt = newCred.ExpiresAt,
                    rotationCount = state.RotationCount
                })));

            _metrics.RecordRotation(true);
            _logger.LogInformation(
                "DB credential rotated: slot {Slot} active, user {Username}, rotation #{Count}",
                rotatingSlot, newCred.Username, state.RotationCount);

            return new DbCredentialRotationResult(true, rotatingSlot, newCred.Username, newCred.ExpiresAt, null);
        }
        catch (Exception ex)
        {
            _metrics.RecordRotation(false);
            _logger.LogError(ex, "DB credential rotation failed");
            return new DbCredentialRotationResult(false, "unknown", null, null, ex.Message);
        }
    }

    public async Task<DualCredentialStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var state = await LoadStateAsync(ct);

        return new DualCredentialStatus(
            state.ActiveSlot,
            state.SlotAUsername,
            state.SlotAExpiresAt,
            SlotAActive: state.ActiveSlot == "A",
            state.SlotBUsername,
            state.SlotBExpiresAt,
            SlotBActive: state.ActiveSlot == "B",
            state.LastRotatedAt,
            state.RotationCount);
    }

    public async Task<string?> GetActiveConnectionStringAsync(CancellationToken ct = default)
    {
        var state = await LoadStateAsync(ct);

        return state.ActiveSlot == "A"
            ? state.SlotAConnectionString
            : state.SlotBConnectionString;
    }

    // ─── State Persistence ──────────────────────────────

    private async Task<DbRotationState> LoadStateAsync(CancellationToken ct)
    {
        var setting = await _vaultRepo.GetSettingAsync(StateKey, ct);
        if (setting is null)
            return new DbRotationState();

        return JsonSerializer.Deserialize<DbRotationState>(setting.ValueJson) ?? new DbRotationState();
    }

    private async Task SaveStateAsync(DbRotationState state, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(state);
        await _vaultRepo.SaveSettingAsync(StateKey, json, ct);
    }

    public sealed class DbRotationState
    {
        public string ActiveSlot { get; set; } = "A";
        public Guid SlotACredentialId { get; set; }
        public string? SlotAUsername { get; set; }
        public DateTime? SlotAExpiresAt { get; set; }
        public string? SlotAConnectionString { get; set; }
        public Guid SlotBCredentialId { get; set; }
        public string? SlotBUsername { get; set; }
        public DateTime? SlotBExpiresAt { get; set; }
        public string? SlotBConnectionString { get; set; }
        public DateTime? LastRotatedAt { get; set; }
        public int RotationCount { get; set; }

        // DB connection info for credential generation
        public string DbHost { get; set; } = "localhost";
        public int DbPort { get; set; } = 5433;
        public string DbName { get; set; } = "ivf_db";
        public string AdminUsername { get; set; } = "postgres";
        public string AdminPassword { get; set; } = "postgres";
    }
}
