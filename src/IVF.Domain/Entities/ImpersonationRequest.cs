using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Admin impersonation request with dual-approval workflow.
/// Ensures audit trail integrity for all actions taken during impersonation.
/// Inspired by AWS STS AssumeRole + Google Workspace Admin delegation.
/// </summary>
public class ImpersonationRequest : BaseEntity
{
    public Guid RequestedBy { get; private set; } // Admin requesting impersonation
    public Guid TargetUserId { get; private set; } // User to impersonate
    public string Reason { get; private set; } = string.Empty;
    public string Status { get; private set; } = "Pending"; // Pending, Approved, Denied, Active, Expired, Ended
    public Guid? ApprovedBy { get; private set; } // Second admin who approved
    public DateTime? ApprovedAt { get; private set; }
    public DateTime? DeniedAt { get; private set; }
    public string? DenialReason { get; private set; }
    public DateTime? ActivatedAt { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public string? EndReason { get; private set; } // "manual", "expired", "security_violation"
    public string? SessionToken { get; private set; } // Impersonation session token

    private ImpersonationRequest() { }

    public static ImpersonationRequest Create(
        Guid requestedBy,
        Guid targetUserId,
        string reason)
    {
        return new ImpersonationRequest
        {
            RequestedBy = requestedBy,
            TargetUserId = targetUserId,
            Reason = reason
        };
    }

    public void Approve(Guid approvedBy, int durationMinutes = 30)
    {
        Status = "Approved";
        ApprovedBy = approvedBy;
        ApprovedAt = DateTime.UtcNow;
        ExpiresAt = DateTime.UtcNow.AddMinutes(durationMinutes);
        SetUpdated();
    }

    public void Deny(Guid deniedBy, string? reason)
    {
        Status = "Denied";
        ApprovedBy = deniedBy; // reuse field for who acted
        DeniedAt = DateTime.UtcNow;
        DenialReason = reason;
        SetUpdated();
    }

    public void Activate(string sessionToken)
    {
        Status = "Active";
        ActivatedAt = DateTime.UtcNow;
        SessionToken = sessionToken;
        SetUpdated();
    }

    public void End(string reason)
    {
        Status = "Ended";
        EndedAt = DateTime.UtcNow;
        EndReason = reason;
        SetUpdated();
    }

    public bool IsExpired() => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;

    public bool IsActive() => Status == "Active" && !IsExpired() && !IsDeleted;
}
