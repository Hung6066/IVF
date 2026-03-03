using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.Application.Features.Users.Commands;

// ═══════════════════════════════════════════════════════════════
// SESSION MANAGEMENT
// ═══════════════════════════════════════════════════════════════

public record CreateUserSessionCommand(
    Guid UserId,
    string SessionToken,
    DateTime ExpiresAt,
    string? IpAddress,
    string? UserAgent,
    string? DeviceFingerprint,
    string? Country,
    string? City,
    string? DeviceType,
    string? OperatingSystem,
    string? Browser) : IRequest<Guid>;

public record RevokeUserSessionCommand(Guid SessionId, string Reason, string RevokedBy) : IRequest;

public record RevokeAllUserSessionsCommand(Guid UserId, string Reason, string RevokedBy) : IRequest<int>;

// ═══════════════════════════════════════════════════════════════
// GROUP MANAGEMENT
// ═══════════════════════════════════════════════════════════════

public record CreateUserGroupCommand(
    string Name,
    string? DisplayName,
    string? Description,
    string GroupType,
    Guid? ParentGroupId) : IRequest<Guid>;

public record UpdateUserGroupCommand(
    Guid Id,
    string Name,
    string? DisplayName,
    string? Description,
    string GroupType) : IRequest;

public record DeleteUserGroupCommand(Guid Id) : IRequest;

public record AddGroupMemberCommand(Guid GroupId, Guid UserId, string MemberRole, Guid? AddedBy) : IRequest<Guid>;

public record RemoveGroupMemberCommand(Guid GroupId, Guid UserId) : IRequest;

public record UpdateGroupMemberRoleCommand(Guid GroupId, Guid UserId, string MemberRole) : IRequest;

public record AssignGroupPermissionsCommand(Guid GroupId, List<string> Permissions, Guid? GrantedBy) : IRequest;

// ═══════════════════════════════════════════════════════════════
// LOGIN HISTORY & ANALYTICS
// ═══════════════════════════════════════════════════════════════

public record RecordLoginHistoryCommand(
    Guid UserId,
    string LoginMethod,
    bool IsSuccess,
    string? FailureReason,
    string? IpAddress,
    string? UserAgent,
    string? DeviceFingerprint,
    string? Country,
    string? City,
    string? DeviceType,
    string? OperatingSystem,
    string? Browser,
    decimal? RiskScore,
    bool IsSuspicious,
    string? RiskFactors) : IRequest<Guid>;

public record RecordLogoutCommand(Guid LoginHistoryId) : IRequest;

// ═══════════════════════════════════════════════════════════════
// CONSENT MANAGEMENT (GDPR/HIPAA)
// ═══════════════════════════════════════════════════════════════

public record GrantConsentCommand(
    Guid UserId,
    string ConsentType,
    string? ConsentVersion,
    string? IpAddress,
    string? UserAgent,
    DateTime? ExpiresAt) : IRequest<Guid>;

public record RevokeConsentCommand(Guid ConsentId, string? Reason) : IRequest;
