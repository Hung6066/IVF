using IVF.Domain.Entities;
using IVF.Domain.Enums;

namespace IVF.Application.Common.Interfaces;

public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default);
    Task<(IReadOnlyList<Tenant> Items, int TotalCount)> GetAllAsync(int page, int pageSize, string? search, TenantStatus? status, CancellationToken ct = default);
    Task AddAsync(Tenant tenant, CancellationToken ct = default);

    // Subscriptions
    Task<TenantSubscription?> GetActiveSubscriptionAsync(Guid tenantId, CancellationToken ct = default);
    Task AddSubscriptionAsync(TenantSubscription subscription, CancellationToken ct = default);

    // Usage
    Task<TenantUsageRecord?> GetCurrentUsageAsync(Guid tenantId, CancellationToken ct = default);
    Task AddUsageRecordAsync(TenantUsageRecord usage, CancellationToken ct = default);

    // Stats
    Task<int> GetTenantUserCountAsync(Guid tenantId, CancellationToken ct = default);
    Task<int> GetTenantPatientCountAsync(Guid tenantId, CancellationToken ct = default);
    Task<decimal> GetTotalMonthlyRevenueAsync(CancellationToken ct = default);
    Task<List<TenantUsageRecord>> GetAllCurrentUsagesAsync(CancellationToken ct = default);

    // List with related data (for admin queries)
    Task<SubscriptionPlan?> GetTenantActiveSubscriptionPlanAsync(Guid tenantId, CancellationToken ct = default);
    Task<List<Tenant>> GetAllTenantsRawAsync(CancellationToken ct = default);

    // Real-time usage counts (computed from actual data)
    Task<int> GetTenantTreatmentCycleCountAsync(Guid tenantId, int year, int month, CancellationToken ct = default);
    Task<int> GetTenantFormResponseCountAsync(Guid tenantId, int year, int month, CancellationToken ct = default);
    Task<int> GetTenantSignedDocumentCountAsync(Guid tenantId, int year, int month, CancellationToken ct = default);
    Task<long> GetTenantStorageUsedBytesAsync(Guid tenantId, CancellationToken ct = default);
    Task<int> GetTenantNewPatientCountAsync(Guid tenantId, int year, int month, CancellationToken ct = default);

    // Usage history
    Task<List<TenantUsageRecord>> GetUsageHistoryAsync(Guid tenantId, int months = 12, CancellationToken ct = default);

    // Usage detail drill-down
    Task<List<(Guid Id, string FullName, string Username, string Role, bool IsActive, DateTime CreatedAt)>>
        GetTenantActiveUsersDetailAsync(Guid tenantId, CancellationToken ct = default);

    Task<List<(Guid Id, string PatientCode, string FullName, string? Phone, DateTime CreatedAt)>>
        GetTenantNewPatientsDetailAsync(Guid tenantId, int year, int month, CancellationToken ct = default);

    Task<List<(Guid Id, string CycleCode, string Method, string Phase, string Outcome, DateTime CreatedAt)>>
        GetTenantTreatmentCyclesDetailAsync(Guid tenantId, int year, int month, CancellationToken ct = default);

    Task<List<(Guid Id, string TemplateName, string? PatientName, string Status, DateTime CreatedAt)>>
        GetTenantFormResponsesDetailAsync(Guid tenantId, int year, int month, CancellationToken ct = default);

    Task<List<(Guid Id, string Title, string FileName, string DocumentType, long FileSizeBytes, string? SignedByName, DateTime? SignedAt, DateTime CreatedAt)>>
        GetTenantSignedDocumentsDetailAsync(Guid tenantId, int year, int month, CancellationToken ct = default);

    Task<List<(Guid Id, string Title, string FileName, string DocumentType, long FileSizeBytes, string? PatientName, DateTime CreatedAt)>>
        GetTenantStorageDetailAsync(Guid tenantId, CancellationToken ct = default);

    // Tenant user management
    Task<(List<User> Users, int TotalCount)> GetTenantUsersAsync(Guid tenantId, string? search, string? role, bool? isActive, int page, int pageSize, CancellationToken ct = default);
    Task<DateTime?> GetUserLastLoginAsync(Guid userId, CancellationToken ct = default);

    // API call logs
    Task<(List<ApiCallLog> Items, int TotalCount)> GetTenantApiCallLogsAsync(Guid tenantId, int page, int pageSize, string? method, int? statusCode, CancellationToken ct = default);
    Task<(int Total, int Success, int Error, double AvgMs, long MaxMs)> GetTenantApiCallStatsAsync(Guid tenantId, CancellationToken ct = default);
    Task<Dictionary<string, int>> GetTenantApiCallsByMethodAsync(Guid tenantId, CancellationToken ct = default);
    Task<Dictionary<string, int>> GetTenantApiCallsTopPathsAsync(Guid tenantId, int top, CancellationToken ct = default);
}
