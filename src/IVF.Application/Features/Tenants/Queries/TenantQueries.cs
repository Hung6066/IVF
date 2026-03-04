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
