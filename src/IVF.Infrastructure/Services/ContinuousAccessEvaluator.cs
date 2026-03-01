using System.Text.Json;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

/// <summary>
/// Google CAE-style continuous access evaluation.
/// - Re-evaluates sessions for IP changes, country changes, anomalies
/// - Enforces step-up auth for critical vault operations
/// - Binds vault tokens to sessions for correlated revocation
/// </summary>
public class ContinuousAccessEvaluator : IContinuousAccessEvaluator
{
    private readonly IVaultRepository _vaultRepo;
    private readonly IZeroTrustService _ztService;
    private readonly ISecurityEventPublisher _securityEvents;
    private readonly ILogger<ContinuousAccessEvaluator> _logger;

    // Session max age before forced re-auth
    private static readonly TimeSpan MaxSessionAge = TimeSpan.FromHours(8);
    // Max time between evaluations
    private static readonly TimeSpan EvaluationInterval = TimeSpan.FromMinutes(5);

    // Actions requiring step-up authentication
    private static readonly HashSet<ZTVaultAction> StepUpActions = new()
    {
        ZTVaultAction.SecretDelete,
        ZTVaultAction.SecretExport,
        ZTVaultAction.KeyRotate,
        ZTVaultAction.BreakGlassAccess,
    };

    public ContinuousAccessEvaluator(
        IVaultRepository vaultRepo,
        IZeroTrustService ztService,
        ISecurityEventPublisher securityEvents,
        ILogger<ContinuousAccessEvaluator> logger)
    {
        _vaultRepo = vaultRepo;
        _ztService = ztService;
        _securityEvents = securityEvents;
        _logger = logger;
    }

    public async Task<CaeDecision> EvaluateSessionAsync(CaeSessionContext session, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // 1. Session age check
        if (now - session.SessionStartedAt > MaxSessionAge)
        {
            await PublishSessionEvent(session, "session.expired", "Session exceeded maximum age");
            return new CaeDecision(false, "Session expired — maximum age exceeded", true, AuthLevel.Password, now);
        }

        // 2. IP change detection (potential session hijack)
        if (session.IpChanged)
        {
            await PublishSessionEvent(session, "session.ip_changed", "IP address changed during session");
            return new CaeDecision(false, "IP address changed — re-authentication required", true, AuthLevel.Password, now);
        }

        // 3. Country change detection (impossible travel)
        if (session.CountryChanged)
        {
            await PublishSessionEvent(session, "session.country_changed", "Country changed during session (impossible travel)");
            return new CaeDecision(false, "Country changed — possible session hijack", true, AuthLevel.MFA, now);
        }

        // 4. Re-validate with Zero Trust service
        var ztContext = new ZTAccessContext(
            session.UserId, session.DeviceId, session.IpAddress,
            session.Country, session.CurrentAuthLevel,
            null, false, false, false);

        var ztDecision = await _ztService.CheckVaultAccessAsync(
            new CheckZTAccessRequest(ZTVaultAction.SecretRead, ztContext), ct);

        if (!ztDecision.Allowed)
        {
            await PublishSessionEvent(session, "session.zt_denied", ztDecision.Reason);
            return new CaeDecision(false, $"Zero Trust re-evaluation failed: {ztDecision.Reason}",
                ztDecision.RequiresStepUp, ztDecision.RequiredAuthLevel, now);
        }

        return new CaeDecision(true, "Session valid", false, null, now);
    }

    public Task<StepUpRequirement> CheckStepUpRequirementAsync(
        ZTVaultAction action, string userId, AuthLevel currentLevel, CancellationToken ct = default)
    {
        if (!StepUpActions.Contains(action))
            return Task.FromResult(new StepUpRequirement(false, currentLevel, "No step-up required", 0));

        // Critical actions require MFA
        if (currentLevel < AuthLevel.MFA)
        {
            return Task.FromResult(new StepUpRequirement(
                true, AuthLevel.MFA,
                $"Action '{action}' requires MFA authentication",
                TimeoutSeconds: 300)); // 5-minute window to complete MFA
        }

        return Task.FromResult(new StepUpRequirement(false, currentLevel, "Auth level sufficient", 0));
    }

    public async Task BindSessionTokenAsync(string sessionId, Guid vaultTokenId, string userId, CancellationToken ct = default)
    {
        await _vaultRepo.SaveSettingAsync(
            $"session-binding-{sessionId}",
            JsonSerializer.Serialize(new SessionTokenBinding
            {
                SessionId = sessionId,
                VaultTokenId = vaultTokenId,
                UserId = userId,
                BoundAt = DateTime.UtcNow
            }),
            ct);

        _logger.LogInformation("Bound vault token {TokenId} to session {SessionId} for user {UserId}",
            vaultTokenId, sessionId, userId);
    }

    public async Task<int> RevokeSessionTokensAsync(string sessionId, string reason, CancellationToken ct = default)
    {
        var setting = await _vaultRepo.GetSettingAsync($"session-binding-{sessionId}", ct);
        if (setting is null) return 0;

        var binding = JsonSerializer.Deserialize<SessionTokenBinding>(setting.ValueJson);
        if (binding is null) return 0;

        try
        {
            await _vaultRepo.RevokeTokenAsync(binding.VaultTokenId, ct);

            await _vaultRepo.AddAuditLogAsync(VaultAuditLog.Create(
                "session.token.revoke",
                "VaultToken",
                binding.VaultTokenId.ToString(),
                userId: Guid.TryParse(binding.UserId, out var uid) ? uid : null,
                details: JsonSerializer.Serialize(new { sessionId, reason })));

            // Clean up binding
            await _vaultRepo.DeleteSettingAsync($"session-binding-{sessionId}", ct);

            _logger.LogWarning("Revoked vault token {TokenId} for session {SessionId}: {Reason}",
                binding.VaultTokenId, sessionId, reason);

            return 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke session token for {SessionId}", sessionId);
            return 0;
        }
    }

    private async Task PublishSessionEvent(CaeSessionContext session, string eventType, string reason)
    {
        await _securityEvents.PublishAsync(new VaultSecurityEvent
        {
            EventType = eventType,
            Severity = SecuritySeverity.High,
            Source = "ContinuousAccessEvaluator",
            Action = "session.evaluate",
            UserId = session.UserId,
            IpAddress = session.IpAddress,
            ResourceType = "Session",
            ResourceId = session.SessionId,
            Outcome = "deny",
            Reason = reason
        });
    }

    private sealed class SessionTokenBinding
    {
        public string SessionId { get; set; } = "";
        public Guid VaultTokenId { get; set; }
        public string UserId { get; set; } = "";
        public DateTime BoundAt { get; set; }
    }
}
