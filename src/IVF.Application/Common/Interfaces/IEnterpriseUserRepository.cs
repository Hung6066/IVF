using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

public interface IEnterpriseUserRepository
{
    // Sessions
    Task AddSessionAsync(UserSession session, CancellationToken ct = default);
    Task<UserSession?> GetSessionByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<UserSession>> GetUserSessionsAsync(Guid userId, bool activeOnly, CancellationToken ct = default);
    Task<int> RevokeAllSessionsAsync(Guid userId, string reason, string revokedBy, CancellationToken ct = default);

    // Groups
    Task AddGroupAsync(UserGroup group, CancellationToken ct = default);
    Task<UserGroup?> GetGroupByIdAsync(Guid id, CancellationToken ct = default);
    Task<(List<UserGroup> Items, int Total)> GetGroupsPagedAsync(string? search, string? groupType, int page, int pageSize, CancellationToken ct = default);
    Task<int> GetGroupMemberCountAsync(Guid groupId, CancellationToken ct = default);
    Task<int> GetGroupPermissionCountAsync(Guid groupId, CancellationToken ct = default);

    // Group Members
    Task AddGroupMemberAsync(UserGroupMember member, CancellationToken ct = default);
    Task<UserGroupMember?> GetGroupMemberAsync(Guid groupId, Guid userId, CancellationToken ct = default);
    Task<List<(UserGroupMember Member, string Username, string FullName, string Role)>> GetGroupMembersWithUserInfoAsync(Guid groupId, CancellationToken ct = default);

    // Group Permissions
    Task<List<string>> GetGroupPermissionCodesAsync(Guid groupId, CancellationToken ct = default);
    Task ReplaceGroupPermissionsAsync(Guid groupId, List<UserGroupPermission> permissions, CancellationToken ct = default);

    // Login History
    Task AddLoginHistoryAsync(UserLoginHistory history, CancellationToken ct = default);
    Task<UserLoginHistory?> GetLoginHistoryByIdAsync(Guid id, CancellationToken ct = default);
    Task<(List<UserLoginHistory> Items, int Total)> GetLoginHistoriesPagedAsync(Guid? userId, bool? isSuccess, bool? isSuspicious, int page, int pageSize, CancellationToken ct = default);

    // Consent
    Task AddConsentAsync(UserConsent consent, CancellationToken ct = default);
    Task<UserConsent?> GetConsentByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<UserConsent>> GetUserConsentsAsync(Guid userId, CancellationToken ct = default);
    Task SupersedeConsentsAsync(Guid userId, string consentType, CancellationToken ct = default);

    // Analytics
    Task<int> GetTotalUsersAsync(CancellationToken ct = default);
    Task<int> GetActiveUsersAsync(CancellationToken ct = default);
    Task<int> GetActiveSessionCountAsync(CancellationToken ct = default);
    Task<int> GetMfaEnabledCountAsync(CancellationToken ct = default);
    Task<int> GetPasskeyCountAsync(CancellationToken ct = default);
    Task<int> GetTotalGroupsAsync(CancellationToken ct = default);
    Task<Dictionary<string, int>> GetRoleDistributionAsync(CancellationToken ct = default);
    Task<int> GetLoginCountSinceAsync(DateTime since, CancellationToken ct = default);
    Task<int> GetFailedLoginCountSinceAsync(DateTime since, CancellationToken ct = default);
    Task<int> GetSuspiciousLoginCountSinceAsync(DateTime since, CancellationToken ct = default);
    Task<List<(string Date, int Success, int Failed, int Suspicious)>> GetLoginTrendsAsync(DateTime since, CancellationToken ct = default);
    Task<List<(Guid UserId, string Username, string FullName, int LoginCount, DateTime LastLogin)>> GetTopActiveUsersAsync(int count, DateTime since, CancellationToken ct = default);
    Task<List<(Guid UserId, string Username, string FullName, decimal AvgRisk, int SuspiciousCount)>> GetHighRiskUsersAsync(int count, DateTime since, CancellationToken ct = default);

    // User Detail
    Task<User?> GetUserByIdAsync(Guid userId, CancellationToken ct = default);
    Task<(bool Enabled, string? Method)> GetUserMfaInfoAsync(Guid userId, CancellationToken ct = default);
    Task<int> GetUserPasskeyCountAsync(Guid userId, CancellationToken ct = default);
    Task<int> GetUserActiveSessionCountAsync(Guid userId, CancellationToken ct = default);
    Task<List<string>> GetUserGroupNamesAsync(Guid userId, CancellationToken ct = default);
    Task<List<string>> GetUserDirectPermissionsAsync(Guid userId, CancellationToken ct = default);
    Task<List<string>> GetUserGroupPermissionsAsync(Guid userId, CancellationToken ct = default);
    Task<List<UserLoginHistory>> GetUserRecentLoginsAsync(Guid userId, int days, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
