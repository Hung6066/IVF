using IVF.Application.Common.Interfaces;
using MediatR;

namespace IVF.Application.Features.Users.Queries;

// ═══════════════════════════════════════════════════════════════
// QUERY HANDLERS
// ═══════════════════════════════════════════════════════════════

public class GetUserSessionsHandler(IEnterpriseUserRepository repo) : IRequestHandler<GetUserSessionsQuery, List<UserSessionDto>>
{
    public async Task<List<UserSessionDto>> Handle(GetUserSessionsQuery r, CancellationToken ct)
    {
        var sessions = await repo.GetUserSessionsAsync(r.UserId, r.ActiveOnly, ct);
        return sessions.Select(s => new UserSessionDto(
            s.Id, s.UserId, s.IpAddress, s.Country, s.City,
            s.DeviceType, s.OperatingSystem, s.Browser,
            s.StartedAt, s.ExpiresAt, s.LastActivityAt,
            s.IsRevoked, s.RevokedReason, s.RevokedAt)).ToList();
    }
}

public class GetUserGroupsHandler(IEnterpriseUserRepository repo) : IRequestHandler<GetUserGroupsQuery, UserGroupListResponse>
{
    public async Task<UserGroupListResponse> Handle(GetUserGroupsQuery r, CancellationToken ct)
    {
        var (groups, total) = await repo.GetGroupsPagedAsync(r.Search, r.GroupType, r.Page, r.PageSize, ct);

        var items = new List<UserGroupDto>();
        foreach (var g in groups)
        {
            var memberCount = await repo.GetGroupMemberCountAsync(g.Id, ct);
            var permCount = await repo.GetGroupPermissionCountAsync(g.Id, ct);
            var consentCount = await repo.GetGroupConsentCountAsync(g.Id, ct);
            items.Add(new UserGroupDto(
                g.Id, g.Name, g.DisplayName, g.Description, g.GroupType,
                g.ParentGroupId, g.IsSystem, g.IsActive, memberCount, permCount, consentCount));
        }

        return new UserGroupListResponse(items, total, r.Page, r.PageSize);
    }
}

public class GetUserGroupDetailHandler(IEnterpriseUserRepository repo) : IRequestHandler<GetUserGroupDetailQuery, UserGroupDetailDto>
{
    public async Task<UserGroupDetailDto> Handle(GetUserGroupDetailQuery r, CancellationToken ct)
    {
        var group = await repo.GetGroupByIdAsync(r.GroupId, ct)
            ?? throw new KeyNotFoundException($"Group {r.GroupId} not found");

        var membersRaw = await repo.GetGroupMembersWithUserInfoAsync(r.GroupId, ct);
        var members = membersRaw.Select(m => new UserGroupMemberDto(
            m.Member.Id, m.Member.UserId, m.Username, m.FullName, m.Role, m.Member.MemberRole, m.Member.JoinedAt)).ToList();

        var permissions = await repo.GetGroupPermissionCodesAsync(r.GroupId, ct);

        return new UserGroupDetailDto(
            group.Id, group.Name, group.DisplayName, group.Description,
            group.GroupType, group.IsSystem, group.IsActive, members, permissions);
    }
}

public class GetUserLoginHistoryHandler(IEnterpriseUserRepository repo) : IRequestHandler<GetUserLoginHistoryQuery, LoginHistoryListResponse>
{
    public async Task<LoginHistoryListResponse> Handle(GetUserLoginHistoryQuery r, CancellationToken ct)
    {
        var (items, total) = await repo.GetLoginHistoriesPagedAsync(r.UserId, r.IsSuccess, r.IsSuspicious, r.Page, r.PageSize, ct);

        var dtos = items.Select(h => new UserLoginHistoryDto(
            h.Id, h.UserId, h.LoginMethod, h.IsSuccess, h.FailureReason,
            h.IpAddress, h.Country, h.City, h.DeviceType, h.OperatingSystem, h.Browser,
            h.RiskScore, h.IsSuspicious, h.RiskFactors,
            h.SessionDuration, h.LoginAt, h.LogoutAt)).ToList();

        return new LoginHistoryListResponse(dtos, total, r.Page, r.PageSize);
    }
}

public class GetUserConsentsHandler(IEnterpriseUserRepository repo) : IRequestHandler<GetUserConsentsQuery, List<UserConsentDto>>
{
    public async Task<List<UserConsentDto>> Handle(GetUserConsentsQuery r, CancellationToken ct)
    {
        var consents = await repo.GetUserConsentsAsync(r.UserId, ct);
        return consents.Select(c => new UserConsentDto(
            c.Id, c.UserId, c.ConsentType, c.IsGranted, c.ConsentVersion,
            c.ConsentedAt, c.RevokedAt, c.ExpiresAt)).ToList();
    }
}

public class GetUserAnalyticsHandler(IEnterpriseUserRepository repo) : IRequestHandler<GetUserAnalyticsQuery, UserAnalyticsDto>
{
    public async Task<UserAnalyticsDto> Handle(GetUserAnalyticsQuery r, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var last24h = now.AddHours(-24);
        var last7days = now.AddDays(-7);

        var totalUsers = await repo.GetTotalUsersAsync(ct);
        var activeUsers = await repo.GetActiveUsersAsync(ct);
        var usersByRole = await repo.GetRoleDistributionAsync(ct);
        var mfaEnabled = await repo.GetMfaEnabledCountAsync(ct);
        var passkeyCount = await repo.GetPasskeyCountAsync(ct);
        var totalLogins24h = await repo.GetLoginCountSinceAsync(last24h, ct);
        var failedLogins24h = await repo.GetFailedLoginCountSinceAsync(last24h, ct);
        var suspiciousLogins24h = await repo.GetSuspiciousLoginCountSinceAsync(last24h, ct);
        var activeSessions = await repo.GetActiveSessionCountAsync(ct);
        var totalGroups = await repo.GetTotalGroupsAsync(ct);

        var loginTrendRaw = await repo.GetLoginTrendsAsync(last7days, ct);
        var loginTrend = loginTrendRaw.Select(t => new LoginTrendDto(t.Date, t.Success, t.Failed, t.Suspicious)).ToList();

        var topActiveRaw = await repo.GetTopActiveUsersAsync(10, last7days, ct);
        var topUsers = topActiveRaw.Select(u => new TopUserDto(u.UserId, u.Username, u.FullName, u.LoginCount, u.LastLogin)).ToList();

        var highRiskRaw = await repo.GetHighRiskUsersAsync(10, last7days, ct);
        var highRiskUsers = highRiskRaw.Select(u => new RiskUserDto(u.UserId, u.Username, u.FullName, u.AvgRisk, u.SuspiciousCount)).ToList();

        return new UserAnalyticsDto(
            totalUsers, activeUsers, totalUsers - activeUsers,
            mfaEnabled, passkeyCount, usersByRole,
            totalLogins24h, failedLogins24h, suspiciousLogins24h,
            activeSessions, totalGroups, loginTrend, topUsers, highRiskUsers);
    }
}

public class GetUserDetailHandler(IEnterpriseUserRepository repo) : IRequestHandler<GetUserDetailQuery, UserDetailDto>
{
    public async Task<UserDetailDto> Handle(GetUserDetailQuery r, CancellationToken ct)
    {
        var user = await repo.GetUserByIdAsync(r.UserId, ct)
            ?? throw new KeyNotFoundException($"User {r.UserId} not found");

        var (mfaEnabled, mfaMethod) = await repo.GetUserMfaInfoAsync(r.UserId, ct);
        var passkeyCount = await repo.GetUserPasskeyCountAsync(r.UserId, ct);
        var activeSessions = await repo.GetUserActiveSessionCountAsync(r.UserId, ct);
        var groups = await repo.GetUserGroupNamesAsync(r.UserId, ct);
        var directPermissions = await repo.GetUserDirectPermissionsAsync(r.UserId, ct);
        var groupPermissions = await repo.GetUserGroupPermissionsAsync(r.UserId, ct);

        var recentLogins = await repo.GetUserRecentLoginsAsync(r.UserId, 30, ct);
        var lastSuccess = recentLogins.Where(h => h.IsSuccess).MaxBy(h => h.LoginAt);
        var lastFailed = recentLogins.Where(h => !h.IsSuccess).MaxBy(h => h.LoginAt);

        var loginSummary = new UserLoginSummaryDto(
            recentLogins.Count,
            recentLogins.Count(h => !h.IsSuccess),
            recentLogins.Count(h => h.IsSuspicious),
            lastSuccess?.LoginAt,
            lastSuccess?.IpAddress,
            lastSuccess?.Country,
            lastFailed?.LoginAt,
            recentLogins.Any(h => h.RiskScore.HasValue)
                ? recentLogins.Where(h => h.RiskScore.HasValue).Average(h => h.RiskScore!.Value)
                : 0);

        return new UserDetailDto(
            user.Id, user.Username, user.FullName, user.Role, user.Department,
            user.IsActive, user.CreatedAt, user.UpdatedAt,
            mfaEnabled, mfaMethod,
            passkeyCount, activeSessions, groups, directPermissions, groupPermissions, loginSummary);
    }
}
