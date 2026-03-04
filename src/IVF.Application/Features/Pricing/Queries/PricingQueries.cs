using IVF.Domain.Entities;
using IVF.Domain.Enums;
using MediatR;

namespace IVF.Application.Features.Pricing.Queries;

// ── DTOs ──────────────────────────────────────────────
public record PlanPricingDto(
    string Plan,
    string DisplayName,
    string? Description,
    decimal Price,
    string Currency,
    string Duration,
    int MaxUsers,
    int MaxPatients,
    double StorageGb,
    bool IsFeatured,
    List<PlanFeatureDto> Features);

public record PlanFeatureDto(
    string Code,
    string DisplayName,
    string? Description,
    string Icon,
    string Category);

public record FeatureDefinitionDto(
    Guid Id,
    string Code,
    string DisplayName,
    string? Description,
    string Icon,
    string Category,
    int SortOrder,
    bool IsActive);

public record TenantFeaturesDto(
    bool IsPlatformAdmin,
    List<string> EnabledFeatures,
    DataIsolationStrategy IsolationStrategy,
    int MaxUsers,
    int MaxPatients);

// ── Queries ──────────────────────────────────────────
public record GetDynamicPricingQuery() : IRequest<List<PlanPricingDto>>;
public record GetTenantDynamicFeaturesQuery(Guid? TenantId, bool IsPlatformAdmin) : IRequest<TenantFeaturesDto>;
public record GetAllFeatureDefinitionsQuery() : IRequest<List<FeatureDefinitionDto>>;
