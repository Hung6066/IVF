using IVF.Domain.Entities;
using IVF.Domain.Enums;
using MediatR;

namespace IVF.Application.Features.Tenants.Queries;

// DTOs
public record TenantDto(
    Guid Id,
    string Name,
    string Slug,
    string? LogoUrl,
    string? Address,
    string? Phone,
    string? Email,
    string? Website,
    string? TaxId,
    TenantStatus Status,
    int MaxUsers,
    int MaxPatientsPerMonth,
    long StorageLimitMb,
    bool AiEnabled,
    bool DigitalSigningEnabled,
    bool BiometricsEnabled,
    bool AdvancedReportingEnabled,
    DataIsolationStrategy IsolationStrategy,
    bool IsRootTenant,
    string? DatabaseSchema,
    string? ConnectionString,
    string? PrimaryColor,
    string? Locale,
    string? TimeZone,
    string? CustomDomain,
    CustomDomainStatus CustomDomainStatus,
    DateTime? CustomDomainVerifiedAt,
    string? CustomDomainVerificationToken,
    DateTime CreatedAt,
    SubscriptionDto? ActiveSubscription,
    UsageDto? CurrentUsage);

public record SubscriptionDto(
    Guid Id,
    SubscriptionPlan Plan,
    SubscriptionStatus Status,
    BillingCycle BillingCycle,
    decimal MonthlyPrice,
    decimal? DiscountPercent,
    string Currency,
    DateTime StartDate,
    DateTime? EndDate,
    DateTime? TrialEndDate,
    DateTime? NextBillingDate,
    bool AutoRenew,
    decimal EffectivePrice);

public record UsageDto(
    int Year,
    int Month,
    int ActiveUsers,
    int NewPatients,
    int TreatmentCycles,
    int FormResponses,
    int SignedDocuments,
    long StorageUsedMb,
    int ApiCalls);

public record TenantListItemDto(
    Guid Id,
    string Name,
    string Slug,
    TenantStatus Status,
    DataIsolationStrategy IsolationStrategy,
    SubscriptionPlan? Plan,
    int ActiveUsers,
    int TotalPatients,
    DateTime CreatedAt);

// Queries
public record GetAllTenantsQuery(int Page = 1, int PageSize = 20, string? Search = null, TenantStatus? Status = null)
    : IRequest<PagedResult<TenantListItemDto>>;

public record GetTenantByIdQuery(Guid Id) : IRequest<TenantDto?>;

public record GetTenantStatsQuery() : IRequest<TenantPlatformStats>;

public record TenantPlatformStats(
    int TotalTenants,
    int ActiveTenants,
    int TrialTenants,
    int SuspendedTenants,
    decimal MonthlyRevenue,
    int TotalUsers,
    int TotalPatients,
    long TotalStorageMb);

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

// ═══════════════ Advanced Usage Analytics ═══════════════

public record GetTenantUsageHistoryQuery(Guid TenantId, int Months = 12) : IRequest<TenantUsageAnalytics>;

public record TenantUsageAnalytics(
    UsageSnapshotDto CurrentUsage,
    LimitComparisonDto LimitComparison,
    List<UsageHistoryItemDto> History,
    List<UsageAlertDto> Alerts);

public record UsageSnapshotDto(
    int Year,
    int Month,
    int ActiveUsers,
    int NewPatients,
    int TreatmentCycles,
    int FormResponses,
    int SignedDocuments,
    long StorageUsedMb,
    int ApiCalls);

public record LimitComparisonDto(
    int MaxUsers,
    int CurrentUsers,
    double UsersPercent,
    int MaxPatientsPerMonth,
    int CurrentPatients,
    double PatientsPercent,
    long StorageLimitMb,
    long CurrentStorageMb,
    double StoragePercent);

public record UsageHistoryItemDto(
    int Year,
    int Month,
    int ActiveUsers,
    int NewPatients,
    int TreatmentCycles,
    int FormResponses,
    int SignedDocuments,
    long StorageUsedMb,
    int ApiCalls);

public record UsageAlertDto(
    string Type,      // "warning" | "critical" | "info"
    string Metric,    // "users" | "patients" | "storage"
    string Message,
    double Percent);

public record RefreshTenantUsageCommand(Guid TenantId) : IRequest<UsageSnapshotDto>;

// ═══════════════ Usage Detail Drill-Down ═══════════════

public record GetTenantUsageDetailQuery(Guid TenantId, string Metric, int Year, int Month) : IRequest<UsageDetailResult>;

public record UsageDetailResult(
    string Metric,
    string Title,
    int TotalCount,
    List<UsageDetailItemDto> Items);

public record UsageDetailItemDto(
    Guid Id,
    string Name,
    string? Description,
    string? Status,
    DateTime CreatedAt,
    Dictionary<string, string>? Extra);

// ═══════════════ Tenant User Management ═══════════════

public record GetTenantUsersQuery(Guid TenantId, string? Search = null, string? Role = null, bool? IsActive = null, int Page = 1, int PageSize = 20)
    : IRequest<TenantUsersResult>;

public record TenantUsersResult(
    List<TenantUserDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public record TenantUserDto(
    Guid Id,
    string Username,
    string FullName,
    string Role,
    string? Department,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

public record AdminResetPasswordCommand(Guid TenantId, Guid UserId, string NewPassword) : IRequest;

// ═══════════════ API Call Logs ═══════════════

public record GetTenantApiCallsQuery(Guid TenantId, int Page = 1, int PageSize = 50, string? Method = null, int? StatusCode = null)
    : IRequest<TenantApiCallsResult>;

public record TenantApiCallsResult(
    List<ApiCallLogDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    ApiCallStatsDto Stats);

public record ApiCallLogDto(
    Guid Id,
    string? Username,
    string Method,
    string Path,
    int StatusCode,
    long DurationMs,
    string? IpAddress,
    DateTime RequestedAt);

public record ApiCallStatsDto(
    int TotalCalls,
    int SuccessCalls,
    int ErrorCalls,
    double AvgDurationMs,
    long MaxDurationMs,
    Dictionary<string, int> ByMethod,
    Dictionary<string, int> TopPaths);
