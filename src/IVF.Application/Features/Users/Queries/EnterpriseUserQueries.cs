using IVF.Application.Common.Interfaces;
using MediatR;

namespace IVF.Application.Features.Users.Queries;

// ═══════════════════════════════════════════════════════════════
// DTOs
// ═══════════════════════════════════════════════════════════════

public record UserSessionDto(
    Guid Id, Guid UserId, string? IpAddress, string? Country, string? City,
    string? DeviceType, string? OperatingSystem, string? Browser,
    DateTime StartedAt, DateTime ExpiresAt, DateTime LastActivityAt,
    bool IsRevoked, string? RevokedReason, DateTime? RevokedAt);

public record UserGroupDto(
    Guid Id, string Name, string? DisplayName, string? Description,
    string GroupType, Guid? ParentGroupId, bool IsSystem, bool IsActive,
    int MemberCount, int PermissionCount, int ConsentCount);

public record UserGroupMemberDto(
    Guid Id, Guid UserId, string Username, string FullName, string Role,
    string MemberRole, DateTime JoinedAt);

public record UserGroupDetailDto(
    Guid Id, string Name, string? DisplayName, string? Description,
    string GroupType, bool IsSystem, bool IsActive,
    List<UserGroupMemberDto> Members, List<string> Permissions);

public record UserLoginHistoryDto(
    Guid Id, Guid UserId, string LoginMethod, bool IsSuccess,
    string? FailureReason, string? IpAddress, string? Country, string? City,
    string? DeviceType, string? OperatingSystem, string? Browser,
    decimal? RiskScore, bool IsSuspicious, string? RiskFactors,
    TimeSpan? SessionDuration, DateTime LoginAt, DateTime? LogoutAt);

public record UserConsentDto(
    Guid Id, Guid UserId, string ConsentType, bool IsGranted,
    string? ConsentVersion, DateTime ConsentedAt, DateTime? RevokedAt, DateTime? ExpiresAt);

public record UserAnalyticsDto(
    int TotalUsers, int ActiveUsers, int InactiveUsers,
    int MfaEnabledCount, int PasskeyCount,
    Dictionary<string, int> UsersByRole,
    int TotalLogins24h, int FailedLogins24h, int SuspiciousLogins24h,
    int ActiveSessions, int TotalGroups,
    List<LoginTrendDto> LoginTrend7Days,
    List<TopUserDto> MostActiveUsers,
    List<RiskUserDto> HighRiskUsers);

public record LoginTrendDto(string Date, int SuccessCount, int FailedCount, int SuspiciousCount);

public record TopUserDto(Guid UserId, string Username, string FullName, int LoginCount, DateTime LastLogin);

public record RiskUserDto(Guid UserId, string Username, string FullName, decimal AvgRiskScore, int SuspiciousCount);

public record UserDetailDto(
    Guid Id, string Username, string FullName, string Role, string? Department,
    bool IsActive, DateTime CreatedAt, DateTime? UpdatedAt,
    bool MfaEnabled, string? MfaMethod, int PasskeyCount,
    int ActiveSessionCount, List<string> Groups, List<string> Permissions,
    List<string> GroupPermissions, UserLoginSummaryDto LoginSummary);

public record UserLoginSummaryDto(
    int TotalLogins, int FailedLogins, int SuspiciousLogins,
    DateTime? LastLoginAt, string? LastLoginIp, string? LastLoginCountry,
    DateTime? LastFailedAt, decimal AvgRiskScore);

// ═══════════════════════════════════════════════════════════════
// QUERIES
// ═══════════════════════════════════════════════════════════════

public record GetUserSessionsQuery(Guid UserId, bool ActiveOnly = true) : IRequest<List<UserSessionDto>>;

public record GetUserGroupsQuery(string? Search, string? GroupType, int Page = 1, int PageSize = 20) : IRequest<UserGroupListResponse>;
public record UserGroupListResponse(List<UserGroupDto> Items, int Total, int Page, int PageSize);

public record GetUserGroupDetailQuery(Guid GroupId) : IRequest<UserGroupDetailDto>;

public record GetUserLoginHistoryQuery(Guid? UserId, int Page = 1, int PageSize = 50, bool? IsSuccess = null, bool? IsSuspicious = null) : IRequest<LoginHistoryListResponse>;
public record LoginHistoryListResponse(List<UserLoginHistoryDto> Items, int Total, int Page, int PageSize);

public record GetUserConsentsQuery(Guid UserId) : IRequest<List<UserConsentDto>>;

public record GetUserAnalyticsQuery() : IRequest<UserAnalyticsDto>;

public record GetUserDetailQuery(Guid UserId) : IRequest<UserDetailDto>;
