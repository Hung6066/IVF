using IVF.Domain.Enums;
using MediatR;

namespace IVF.Application.Features.Pricing.Commands;

// ── Feature Definition CRUD ──
public record CreateFeatureDefinitionCommand(
    string Code,
    string DisplayName,
    string? Description,
    string Icon,
    string Category,
    int SortOrder) : IRequest<Guid>;

public record UpdateFeatureDefinitionCommand(
    Guid Id,
    string DisplayName,
    string? Description,
    string Icon,
    string Category,
    int SortOrder,
    bool IsActive) : IRequest;

public record DeleteFeatureDefinitionCommand(Guid Id) : IRequest;

// ── Plan Definition CRUD ──
public record CreatePlanDefinitionCommand(
    SubscriptionPlan Plan,
    string DisplayName,
    string? Description,
    decimal MonthlyPrice,
    string Currency,
    string Duration,
    int MaxUsers,
    int MaxPatientsPerMonth,
    long StorageLimitMb,
    int SortOrder,
    bool IsFeatured) : IRequest<Guid>;

public record UpdatePlanDefinitionCommand(
    Guid Id,
    string DisplayName,
    string? Description,
    decimal MonthlyPrice,
    string Duration,
    int MaxUsers,
    int MaxPatientsPerMonth,
    long StorageLimitMb,
    int SortOrder,
    bool IsFeatured,
    bool IsActive) : IRequest;

public record DeletePlanDefinitionCommand(Guid Id) : IRequest;

// ── Plan-Feature Mapping ──
public record UpdatePlanFeaturesCommand(
    Guid PlanDefinitionId,
    List<Guid> FeatureDefinitionIds) : IRequest;

// ── Tenant Feature Override ──
public record UpdateTenantFeaturesCommand(
    Guid TenantId,
    List<TenantFeatureUpdate> Features) : IRequest;

public record TenantFeatureUpdate(Guid FeatureDefinitionId, bool IsEnabled);
