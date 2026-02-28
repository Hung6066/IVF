using IVF.Domain.Enums;

namespace IVF.Application.Common.Interfaces;

public record ZTAccessContext(
    string UserId,
    string DeviceId,
    string IpAddress,
    string? Country,
    AuthLevel CurrentAuthLevel,
    DateTime? LastPasswordVerification,
    bool IsVpn,
    bool IsTor,
    bool HasActiveAnomaly
);

public record ZTAccessDecision(
    bool Allowed,
    ZTVaultAction Action,
    string Reason,
    List<string> FailedChecks,
    RiskLevel? DeviceRiskLevel,
    decimal? DeviceRiskScore,
    bool RequiresStepUp,
    AuthLevel? RequiredAuthLevel,
    bool BreakGlassOverrideUsed,
    DateTime DecisionTime
);

public record CheckZTAccessRequest(
    ZTVaultAction Action,
    ZTAccessContext Context,
    bool UseBreakGlassOverride = false,
    string? BreakGlassRequestId = null
);

public interface IZeroTrustService
{
    Task<ZTAccessDecision> CheckVaultAccessAsync(CheckZTAccessRequest request, CancellationToken ct = default);
    Task<List<Domain.Entities.ZTPolicy>> GetAllPoliciesAsync(CancellationToken ct = default);
    Task<bool> UpdatePolicyAsync(ZTVaultAction action, Domain.Entities.ZTPolicy policy, CancellationToken ct = default);
    Task RefreshPoliciesAsync(CancellationToken ct = default);
}
