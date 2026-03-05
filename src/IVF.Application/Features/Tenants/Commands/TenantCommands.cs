using IVF.Domain.Enums;
using MediatR;

namespace IVF.Application.Features.Tenants.Commands;

public record CreateTenantCommand(
    string Name,
    string Slug,
    string? Email,
    string? Phone,
    string? Address,
    SubscriptionPlan Plan = SubscriptionPlan.Trial,
    BillingCycle BillingCycle = BillingCycle.Monthly,
    DataIsolationStrategy IsolationStrategy = DataIsolationStrategy.SharedDatabase,
    // Admin user for the new tenant
    string AdminUsername = "admin",
    string AdminPassword = "Admin@123",
    string AdminFullName = "Administrator") : IRequest<Guid>;

public record UpdateTenantCommand(
    Guid Id,
    string Name,
    string? Address,
    string? Phone,
    string? Email,
    string? Website,
    string? TaxId) : IRequest;

public record UpdateTenantBrandingCommand(
    Guid Id,
    string? LogoUrl,
    string? PrimaryColor,
    string? CustomDomain) : IRequest;

public record UpdateTenantLimitsCommand(
    Guid Id,
    int MaxUsers,
    int MaxPatientsPerMonth,
    long StorageLimitMb,
    bool AiEnabled,
    bool DigitalSigningEnabled,
    bool BiometricsEnabled,
    bool AdvancedReportingEnabled) : IRequest;

public record ActivateTenantCommand(Guid Id) : IRequest;
public record SuspendTenantCommand(Guid Id, string? Reason) : IRequest;
public record CancelTenantCommand(Guid Id) : IRequest;

public record UpdateSubscriptionCommand(
    Guid TenantId,
    SubscriptionPlan Plan,
    BillingCycle BillingCycle,
    decimal MonthlyPrice,
    decimal? DiscountPercent) : IRequest;

public record UpdateTenantIsolationCommand(
    Guid Id,
    DataIsolationStrategy IsolationStrategy,
    string? ConnectionString,
    string? DatabaseSchema) : IRequest;

// Custom Domain
public record VerifyCustomDomainCommand(Guid TenantId) : IRequest<CustomDomainVerificationResult>;
public record RemoveCustomDomainCommand(Guid TenantId) : IRequest;
public record CustomDomainVerificationResult(bool IsVerified, string Message);
