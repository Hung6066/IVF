using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.Infrastructure.Repositories;

public class EnterpriseUserRepository(IvfDbContext db) : IEnterpriseUserRepository
{
    // ═══ Sessions ═══

    public async Task AddSessionAsync(UserSession session, CancellationToken ct)
    {
        db.UserSessions.Add(session);
        await Task.CompletedTask;
    }

    public async Task<UserSession?> GetSessionByIdAsync(Guid id, CancellationToken ct)
        => await db.UserSessions.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct);

    public async Task<List<UserSession>> GetUserSessionsAsync(Guid userId, bool activeOnly, CancellationToken ct)
    {
        var query = db.UserSessions.Where(s => s.UserId == userId && !s.IsDeleted);
        if (activeOnly)
            query = query.Where(s => !s.IsRevoked && s.ExpiresAt > DateTime.UtcNow);

        return await query.OrderByDescending(s => s.LastActivityAt).ToListAsync(ct);
    }

    public async Task<int> RevokeAllSessionsAsync(Guid userId, string reason, string revokedBy, CancellationToken ct)
    {
        var sessions = await db.UserSessions
            .Where(s => s.UserId == userId && !s.IsRevoked && !s.IsDeleted)
            .ToListAsync(ct);

        foreach (var s in sessions)
            s.Revoke(reason, revokedBy);

        return sessions.Count;
    }

    // ═══ Groups ═══

    public async Task AddGroupAsync(UserGroup group, CancellationToken ct)
    {
        db.UserGroups.Add(group);
        await Task.CompletedTask;
    }

    public async Task<UserGroup?> GetGroupByIdAsync(Guid id, CancellationToken ct)
        => await db.UserGroups.FirstOrDefaultAsync(g => g.Id == id && !g.IsDeleted, ct);

    public async Task<(List<UserGroup> Items, int Total)> GetGroupsPagedAsync(string? search, string? groupType, int page, int pageSize, CancellationToken ct)
    {
        var query = db.UserGroups.Where(g => !g.IsDeleted);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(g => g.Name.Contains(search) || (g.DisplayName != null && g.DisplayName.Contains(search)));
        if (!string.IsNullOrWhiteSpace(groupType))
            query = query.Where(g => g.GroupType == groupType);

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(g => g.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public async Task<int> GetGroupMemberCountAsync(Guid groupId, CancellationToken ct)
        => await db.UserGroupMembers.CountAsync(m => m.GroupId == groupId && !m.IsDeleted, ct);

    public async Task<int> GetGroupPermissionCountAsync(Guid groupId, CancellationToken ct)
        => await db.UserGroupPermissions.CountAsync(p => p.GroupId == groupId && !p.IsDeleted, ct);

    // ═══ Group Members ═══

    public async Task AddGroupMemberAsync(UserGroupMember member, CancellationToken ct)
    {
        db.UserGroupMembers.Add(member);
        await Task.CompletedTask;
    }

    public async Task<UserGroupMember?> GetGroupMemberAsync(Guid groupId, Guid userId, CancellationToken ct)
        => await db.UserGroupMembers.FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId && !m.IsDeleted, ct);

    public async Task<List<(UserGroupMember Member, string Username, string FullName, string Role)>> GetGroupMembersWithUserInfoAsync(Guid groupId, CancellationToken ct)
    {
        return await (from m in db.UserGroupMembers
                      join u in db.Users on m.UserId equals u.Id
                      where m.GroupId == groupId && !m.IsDeleted && !u.IsDeleted
                      select new { m, u.Username, u.FullName, u.Role })
            .AsNoTracking()
            .Select(x => ValueTuple.Create(x.m, x.Username, x.FullName, x.Role))
            .ToListAsync(ct);
    }

    // ═══ Group Permissions ═══

    public async Task<List<string>> GetGroupPermissionCodesAsync(Guid groupId, CancellationToken ct)
        => await db.UserGroupPermissions
            .Where(p => p.GroupId == groupId && !p.IsDeleted)
            .Select(p => p.PermissionCode)
            .ToListAsync(ct);

    public async Task ReplaceGroupPermissionsAsync(Guid groupId, List<UserGroupPermission> permissions, CancellationToken ct)
    {
        var existing = await db.UserGroupPermissions
            .Where(p => p.GroupId == groupId && !p.IsDeleted)
            .ToListAsync(ct);
        foreach (var p in existing) p.MarkAsDeleted();
        db.UserGroupPermissions.AddRange(permissions);
    }

    // ═══ Login History ═══

    public async Task AddLoginHistoryAsync(UserLoginHistory history, CancellationToken ct)
    {
        db.UserLoginHistories.Add(history);
        await Task.CompletedTask;
    }

    public async Task<UserLoginHistory?> GetLoginHistoryByIdAsync(Guid id, CancellationToken ct)
        => await db.UserLoginHistories.FirstOrDefaultAsync(h => h.Id == id && !h.IsDeleted, ct);

    public async Task<(List<UserLoginHistory> Items, int Total)> GetLoginHistoriesPagedAsync(Guid? userId, bool? isSuccess, bool? isSuspicious, int page, int pageSize, CancellationToken ct)
    {
        var query = db.UserLoginHistories.Where(h => !h.IsDeleted);
        if (userId.HasValue) query = query.Where(h => h.UserId == userId.Value);
        if (isSuccess.HasValue) query = query.Where(h => h.IsSuccess == isSuccess.Value);
        if (isSuspicious.HasValue) query = query.Where(h => h.IsSuspicious == isSuspicious.Value);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(h => h.LoginAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    // ═══ Consent ═══

    public async Task AddConsentAsync(UserConsent consent, CancellationToken ct)
    {
        db.UserConsents.Add(consent);
        await Task.CompletedTask;
    }

    public async Task<UserConsent?> GetConsentByIdAsync(Guid id, CancellationToken ct)
        => await db.UserConsents.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, ct);

    public async Task<List<UserConsent>> GetUserConsentsAsync(Guid userId, CancellationToken ct)
        => await db.UserConsents
            .Where(c => c.UserId == userId && !c.IsDeleted)
            .OrderByDescending(c => c.ConsentedAt)
            .ToListAsync(ct);

    public async Task SupersedeConsentsAsync(Guid userId, string consentType, CancellationToken ct)
    {
        var existing = await db.UserConsents
            .Where(c => c.UserId == userId && c.ConsentType == consentType && c.IsGranted && !c.IsDeleted)
            .ToListAsync(ct);
        foreach (var c in existing) c.Revoke("Superseded by new consent");
    }

    // ═══ Analytics ═══

    public async Task<int> GetTotalUsersAsync(CancellationToken ct)
        => await db.Users.CountAsync(u => !u.IsDeleted, ct);

    public async Task<int> GetActiveUsersAsync(CancellationToken ct)
        => await db.Users.CountAsync(u => !u.IsDeleted && u.IsActive, ct);

    public async Task<int> GetActiveSessionCountAsync(CancellationToken ct)
        => await db.UserSessions.CountAsync(s => !s.IsRevoked && !s.IsDeleted && s.ExpiresAt > DateTime.UtcNow, ct);

    public async Task<int> GetMfaEnabledCountAsync(CancellationToken ct)
        => await db.UserMfaSettings.CountAsync(m => m.IsMfaEnabled && !m.IsDeleted, ct);

    public async Task<int> GetPasskeyCountAsync(CancellationToken ct)
        => await db.PasskeyCredentials.CountAsync(p => p.IsActive && !p.IsDeleted, ct);

    public async Task<int> GetTotalGroupsAsync(CancellationToken ct)
        => await db.UserGroups.CountAsync(g => !g.IsDeleted && g.IsActive, ct);

    public async Task<Dictionary<string, int>> GetRoleDistributionAsync(CancellationToken ct)
        => await db.Users
            .Where(u => !u.IsDeleted && u.IsActive)
            .GroupBy(u => u.Role)
            .Select(g => new { Role = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Role, x => x.Count, ct);

    public async Task<int> GetLoginCountSinceAsync(DateTime since, CancellationToken ct)
        => await db.UserLoginHistories.CountAsync(h => h.LoginAt >= since, ct);

    public async Task<int> GetFailedLoginCountSinceAsync(DateTime since, CancellationToken ct)
        => await db.UserLoginHistories.CountAsync(h => h.LoginAt >= since && !h.IsSuccess, ct);

    public async Task<int> GetSuspiciousLoginCountSinceAsync(DateTime since, CancellationToken ct)
        => await db.UserLoginHistories.CountAsync(h => h.LoginAt >= since && h.IsSuspicious, ct);

    public async Task<List<(string Date, int Success, int Failed, int Suspicious)>> GetLoginTrendsAsync(DateTime since, CancellationToken ct)
    {
        var raw = await db.UserLoginHistories
            .Where(h => h.LoginAt >= since)
            .GroupBy(h => h.LoginAt.Date)
            .Select(g => new
            {
                Date = g.Key,
                Success = g.Count(h => h.IsSuccess),
                Failed = g.Count(h => !h.IsSuccess),
                Suspicious = g.Count(h => h.IsSuspicious)
            })
            .OrderBy(t => t.Date)
            .ToListAsync(ct);

        return raw.Select(t => (t.Date.ToString("yyyy-MM-dd"), t.Success, t.Failed, t.Suspicious)).ToList();
    }

    public async Task<List<(Guid UserId, string Username, string FullName, int LoginCount, DateTime LastLogin)>> GetTopActiveUsersAsync(int count, DateTime since, CancellationToken ct)
    {
        var topLogins = await db.UserLoginHistories
            .Where(h => h.LoginAt >= since && h.IsSuccess)
            .GroupBy(h => h.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count(), LastLogin = g.Max(h => h.LoginAt) })
            .OrderByDescending(x => x.Count)
            .Take(count)
            .ToListAsync(ct);

        var userIds = topLogins.Select(x => x.UserId).ToList();
        var users = await db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Username, u.FullName })
            .ToDictionaryAsync(u => u.Id, ct);

        return topLogins.Select(x =>
        {
            var info = users.GetValueOrDefault(x.UserId);
            return (x.UserId, info?.Username ?? "unknown", info?.FullName ?? "Unknown", x.Count, x.LastLogin);
        }).ToList();
    }

    public async Task<List<(Guid UserId, string Username, string FullName, decimal AvgRisk, int SuspiciousCount)>> GetHighRiskUsersAsync(int count, DateTime since, CancellationToken ct)
    {
        var riskData = await db.UserLoginHistories
            .Where(h => h.LoginAt >= since && h.RiskScore > 0)
            .GroupBy(h => h.UserId)
            .Select(g => new { UserId = g.Key, AvgRisk = g.Average(h => (double)(h.RiskScore ?? 0)), SuspCount = g.Count(h => h.IsSuspicious) })
            .Where(x => x.AvgRisk > 30 || x.SuspCount > 0)
            .OrderByDescending(x => x.AvgRisk)
            .Take(count)
            .ToListAsync(ct);

        var userIds = riskData.Select(x => x.UserId).ToList();
        var users = await db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Username, u.FullName })
            .ToDictionaryAsync(u => u.Id, ct);

        return riskData.Select(x =>
        {
            var info = users.GetValueOrDefault(x.UserId);
            return (x.UserId, info?.Username ?? "unknown", info?.FullName ?? "Unknown", (decimal)x.AvgRisk, x.SuspCount);
        }).ToList();
    }

    // ═══ User Detail ═══

    public async Task<User?> GetUserByIdAsync(Guid userId, CancellationToken ct)
        => await db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted, ct);

    public async Task<(bool Enabled, string? Method)> GetUserMfaInfoAsync(Guid userId, CancellationToken ct)
    {
        var mfa = await db.UserMfaSettings.FirstOrDefaultAsync(m => m.UserId == userId && !m.IsDeleted, ct);
        return (mfa?.IsMfaEnabled ?? false, mfa?.MfaMethod);
    }

    public async Task<int> GetUserPasskeyCountAsync(Guid userId, CancellationToken ct)
        => await db.PasskeyCredentials.CountAsync(p => p.UserId == userId && p.IsActive && !p.IsDeleted, ct);

    public async Task<int> GetUserActiveSessionCountAsync(Guid userId, CancellationToken ct)
        => await db.UserSessions.CountAsync(s => s.UserId == userId && !s.IsRevoked && !s.IsDeleted && s.ExpiresAt > DateTime.UtcNow, ct);

    public async Task<List<string>> GetUserGroupNamesAsync(Guid userId, CancellationToken ct)
        => await (from m in db.UserGroupMembers
                  join g in db.UserGroups on m.GroupId equals g.Id
                  where m.UserId == userId && !m.IsDeleted && !g.IsDeleted
                  select g.DisplayName ?? g.Name)
            .ToListAsync(ct);

    public async Task<List<string>> GetUserDirectPermissionsAsync(Guid userId, CancellationToken ct)
        => await db.UserPermissions
            .Where(p => p.UserId == userId && !p.IsDeleted)
            .Select(p => p.PermissionCode)
            .ToListAsync(ct);

    public async Task<List<string>> GetUserGroupPermissionsAsync(Guid userId, CancellationToken ct)
    {
        var groupIds = await db.UserGroupMembers
            .Where(m => m.UserId == userId && !m.IsDeleted)
            .Select(m => m.GroupId)
            .ToListAsync(ct);

        return await db.UserGroupPermissions
            .Where(p => groupIds.Contains(p.GroupId) && !p.IsDeleted)
            .Select(p => p.PermissionCode)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<List<UserLoginHistory>> GetUserRecentLoginsAsync(Guid userId, int days, CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddDays(-days);
        return await db.UserLoginHistories
            .Where(h => h.UserId == userId && h.LoginAt >= since)
            .OrderByDescending(h => h.LoginAt)
            .ToListAsync(ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct)
        => await db.SaveChangesAsync(ct);
}
