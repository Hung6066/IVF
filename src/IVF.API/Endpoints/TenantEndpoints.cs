using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Tenants.Commands;
using IVF.Application.Features.Tenants.Queries;
using IVF.Application.Features.Pricing.Queries;
using IVF.Application.Features.Pricing.Commands;
using IVF.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IVF.API.Endpoints;

public static class TenantEndpoints
{
    public static void MapTenantEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tenants")
            .WithTags("Multi-Tenant Management")
            .RequireAuthorization();

        // ═══════════════ Queries ═══════════════

        group.MapGet("/", async (
            [FromQuery] int page,
            [FromQuery] int pageSize,
            [FromQuery] string? search,
            [FromQuery] TenantStatus? status,
            IMediator mediator) =>
        {
            var result = await mediator.Send(new GetAllTenantsQuery(
                page > 0 ? page : 1,
                pageSize > 0 ? pageSize : 20,
                search,
                status));
            return Results.Ok(result);
        });

        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetTenantByIdQuery(id));
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapGet("/stats", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new GetTenantStatsQuery());
            return Results.Ok(result);
        });

        group.MapGet("/{id:guid}/usage-analytics", async (Guid id, [FromQuery] int months, IMediator mediator, ICurrentUserService currentUser) =>
        {
            if (!currentUser.IsPlatformAdmin)
                return Results.Forbid();
            var result = await mediator.Send(new GetTenantUsageHistoryQuery(id, months > 0 ? months : 12));
            return Results.Ok(result);
        });

        group.MapPost("/{id:guid}/refresh-usage", async (Guid id, IMediator mediator, ICurrentUserService currentUser) =>
        {
            if (!currentUser.IsPlatformAdmin)
                return Results.Forbid();
            var result = await mediator.Send(new RefreshTenantUsageCommand(id));
            return Results.Ok(result);
        });

        group.MapGet("/{id:guid}/usage-detail/{metric}", async (Guid id, string metric, [FromQuery] int year, [FromQuery] int month, IMediator mediator, ICurrentUserService currentUser) =>
        {
            if (!currentUser.IsPlatformAdmin)
                return Results.Forbid();
            var now = DateTime.UtcNow;
            var result = await mediator.Send(new GetTenantUsageDetailQuery(id, metric, year > 0 ? year : now.Year, month > 0 ? month : now.Month));
            return Results.Ok(result);
        });

        // ═══════════════ Tenant User Management ═══════════════

        group.MapGet("/{id:guid}/users", async (Guid id, [FromQuery] string? search, [FromQuery] string? role, [FromQuery] bool? isActive, [FromQuery] int page, [FromQuery] int pageSize, IMediator mediator, ICurrentUserService currentUser) =>
        {
            if (!currentUser.IsPlatformAdmin)
                return Results.Forbid();
            var result = await mediator.Send(new GetTenantUsersQuery(id, search, role, isActive, page > 0 ? page : 1, pageSize > 0 ? pageSize : 20));
            return Results.Ok(result);
        });

        group.MapPost("/{id:guid}/users/{userId:guid}/reset-password", async (Guid id, Guid userId, [FromBody] ResetPasswordRequest req, IMediator mediator, ICurrentUserService currentUser) =>
        {
            if (!currentUser.IsPlatformAdmin)
                return Results.Forbid();
            await mediator.Send(new AdminResetPasswordCommand(id, userId, req.NewPassword));
            return Results.Ok(new { message = "Mật khẩu đã được đặt lại thành công" });
        });

        // ═══════════════ API Call Logs ═══════════════

        group.MapGet("/{id:guid}/api-calls", async (Guid id, [FromQuery] int page, [FromQuery] int pageSize, [FromQuery] string? method, [FromQuery] int? statusCode, IMediator mediator, ICurrentUserService currentUser) =>
        {
            if (!currentUser.IsPlatformAdmin)
                return Results.Forbid();
            var result = await mediator.Send(new GetTenantApiCallsQuery(id, page > 0 ? page : 1, pageSize > 0 ? pageSize : 50, method, statusCode));
            return Results.Ok(result);
        });

        group.MapGet("/pricing", async (IMediator mediator) =>
        {
            var plans = await mediator.Send(new GetDynamicPricingQuery());
            return Results.Ok(plans);
        }).AllowAnonymous();

        // Caddy On-Demand TLS check — validates if domain belongs to a verified tenant
        group.MapGet("/domain-check", async ([FromQuery] string domain, ITenantRepository repo) =>
        {
            if (string.IsNullOrWhiteSpace(domain))
                return Results.BadRequest();
            var tenant = await repo.GetByCustomDomainAsync(domain.ToLowerInvariant());
            if (tenant is not null && tenant.CustomDomainStatus == CustomDomainStatus.Verified)
                return Results.Ok();
            return Results.NotFound(); // Caddy won't issue cert for unverified domains
        }).AllowAnonymous();

        // ═══════════════ Commands (Platform Admin only) ═══════════════

        group.MapPost("/", async (CreateTenantCommand command, IMediator mediator, ICurrentUserService currentUser) =>
        {
            if (!currentUser.IsPlatformAdmin)
                return Results.Forbid();
            var id = await mediator.Send(command);
            return Results.Created($"/api/tenants/{id}", new { id });
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateTenantCommand command, IMediator mediator, ICurrentUserService currentUser) =>
        {
            if (!currentUser.IsPlatformAdmin)
                return Results.Forbid();
            if (id != command.Id) return Results.BadRequest("ID mismatch");
            await mediator.Send(command);
            return Results.NoContent();
        });

        group.MapPut("/{id:guid}/branding", async (Guid id, UpdateTenantBrandingCommand command, IMediator mediator, ICurrentUserService currentUser) =>
        {
            if (!currentUser.IsPlatformAdmin)
                return Results.Forbid();
            if (id != command.Id) return Results.BadRequest("ID mismatch");
            await mediator.Send(command);
            return Results.NoContent();
        });

        // ═══════════════ Custom Domain ═══════════════

        group.MapPost("/{id:guid}/domain/verify", async (Guid id, IMediator mediator, ICurrentUserService currentUser) =>
        {
            if (!currentUser.IsPlatformAdmin)
                return Results.Forbid();
            var result = await mediator.Send(new VerifyCustomDomainCommand(id));
            return Results.Ok(result);
        });

        group.MapDelete("/{id:guid}/domain", async (Guid id, IMediator mediator, ICurrentUserService currentUser) =>
        {
            if (!currentUser.IsPlatformAdmin)
                return Results.Forbid();
            await mediator.Send(new RemoveCustomDomainCommand(id));
            return Results.NoContent();
        });

        group.MapPut("/{id:guid}/limits", async (Guid id, UpdateTenantLimitsCommand command, IMediator mediator, ICurrentUserService currentUser) =>
        {
            if (!currentUser.IsPlatformAdmin)
                return Results.Forbid();
            if (id != command.Id) return Results.BadRequest("ID mismatch");
            await mediator.Send(command);
            return Results.NoContent();
        });

        group.MapPut("/{id:guid}/isolation", async (Guid id, UpdateIsolationRequest req, IMediator mediator, ICurrentUserService currentUser) =>
        {
            if (!currentUser.IsPlatformAdmin)
                return Results.Forbid();
            await mediator.Send(new UpdateTenantIsolationCommand(id, req.IsolationStrategy, req.ConnectionString, req.DatabaseSchema));
            return Results.NoContent();
        });

        group.MapPost("/{id:guid}/activate", async (Guid id, IMediator mediator, ICurrentUserService currentUser) =>
        {
            if (!currentUser.IsPlatformAdmin)
                return Results.Forbid();
            await mediator.Send(new ActivateTenantCommand(id));
            return Results.NoContent();
        });

        group.MapPost("/{id:guid}/suspend", async (Guid id, [FromBody] SuspendRequest? req, IMediator mediator, ICurrentUserService currentUser) =>
        {
            if (!currentUser.IsPlatformAdmin)
                return Results.Forbid();
            await mediator.Send(new SuspendTenantCommand(id, req?.Reason));
            return Results.NoContent();
        });

        group.MapPost("/{id:guid}/cancel", async (Guid id, IMediator mediator, ICurrentUserService currentUser) =>
        {
            if (!currentUser.IsPlatformAdmin)
                return Results.Forbid();
            await mediator.Send(new CancelTenantCommand(id));
            return Results.NoContent();
        });

        group.MapPut("/{id:guid}/subscription", async (Guid id, UpdateSubscriptionRequest req, IMediator mediator, ICurrentUserService currentUser) =>
        {
            if (!currentUser.IsPlatformAdmin)
                return Results.Forbid();
            await mediator.Send(new UpdateSubscriptionCommand(id, req.Plan, req.BillingCycle, req.MonthlyPrice, req.DiscountPercent));
            return Results.NoContent();
        });

        // ═══════════════ Feature Visibility ═══════════════

        group.MapGet("/my-features", async (ICurrentUserService currentUser, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetTenantDynamicFeaturesQuery(
                currentUser.TenantId,
                currentUser.IsPlatformAdmin));
            return Results.Ok(result);
        });

        // ═══════════════ Feature/Plan Admin ═══════════════

        group.MapGet("/feature-definitions", async (IMediator mediator, ICurrentUserService currentUser) =>
        {
            if (!currentUser.IsPlatformAdmin)
                return Results.Forbid();
            var result = await mediator.Send(new GetAllFeatureDefinitionsQuery());
            return Results.Ok(result);
        });

        // ═══════════════ Feature CRUD (Platform Admin) ═══════════════

        group.MapPost("/feature-definitions", async (CreateFeatureDefinitionRequest req, IMediator mediator, ICurrentUserService currentUser) =>
        {
            if (!currentUser.IsPlatformAdmin) return Results.Forbid();
            var id = await mediator.Send(new CreateFeatureDefinitionCommand(
                req.Code, req.DisplayName, req.Description, req.Icon, req.Category, req.SortOrder));
            return Results.Created($"/api/tenants/feature-definitions/{id}", new { id });
        });

        group.MapPut("/feature-definitions/{id:guid}", async (Guid id, UpdateFeatureDefinitionRequest req, IMediator mediator, ICurrentUserService currentUser) =>
        {
            if (!currentUser.IsPlatformAdmin) return Results.Forbid();
            await mediator.Send(new UpdateFeatureDefinitionCommand(
                id, req.DisplayName, req.Description, req.Icon, req.Category, req.SortOrder, req.IsActive));
            return Results.NoContent();
        });

        group.MapDelete("/feature-definitions/{id:guid}", async (Guid id, IMediator mediator, ICurrentUserService currentUser) =>
        {
            if (!currentUser.IsPlatformAdmin) return Results.Forbid();
            await mediator.Send(new DeleteFeatureDefinitionCommand(id));
            return Results.NoContent();
        });

        // ═══════════════ Plan CRUD (Platform Admin) ═══════════════

        group.MapGet("/plan-definitions", async (IMediator mediator, ICurrentUserService currentUser) =>
        {
            if (!currentUser.IsPlatformAdmin) return Results.Forbid();
            var result = await mediator.Send(new GetAllPlanDefinitionsQuery());
            return Results.Ok(result);
        });

        group.MapPost("/plan-definitions", async (CreatePlanDefinitionRequest req, IMediator mediator, ICurrentUserService currentUser) =>
        {
            if (!currentUser.IsPlatformAdmin) return Results.Forbid();
            var id = await mediator.Send(new CreatePlanDefinitionCommand(
                req.Plan, req.DisplayName, req.Description, req.MonthlyPrice, req.Currency,
                req.Duration, req.MaxUsers, req.MaxPatientsPerMonth, req.StorageLimitMb,
                req.SortOrder, req.IsFeatured));
            return Results.Created($"/api/tenants/plan-definitions/{id}", new { id });
        });

        group.MapPut("/plan-definitions/{id:guid}", async (Guid id, UpdatePlanDefinitionRequest req, IMediator mediator, ICurrentUserService currentUser) =>
        {
            if (!currentUser.IsPlatformAdmin) return Results.Forbid();
            await mediator.Send(new UpdatePlanDefinitionCommand(
                id, req.DisplayName, req.Description, req.MonthlyPrice, req.Duration,
                req.MaxUsers, req.MaxPatientsPerMonth, req.StorageLimitMb,
                req.SortOrder, req.IsFeatured, req.IsActive));
            return Results.NoContent();
        });

        group.MapDelete("/plan-definitions/{id:guid}", async (Guid id, IMediator mediator, ICurrentUserService currentUser) =>
        {
            if (!currentUser.IsPlatformAdmin) return Results.Forbid();
            await mediator.Send(new DeletePlanDefinitionCommand(id));
            return Results.NoContent();
        });

        // ═══════════════ Plan-Feature Mapping (Platform Admin) ═══════════════

        group.MapPut("/plan-definitions/{id:guid}/features", async (Guid id, UpdatePlanFeaturesRequest req, IMediator mediator, ICurrentUserService currentUser) =>
        {
            if (!currentUser.IsPlatformAdmin) return Results.Forbid();
            await mediator.Send(new UpdatePlanFeaturesCommand(id, req.FeatureDefinitionIds));
            return Results.NoContent();
        });

        // ═══════════════ Tenant Feature Override (Platform Admin) ═══════════════

        group.MapGet("/{id:guid}/features", async (Guid id, IMediator mediator, ICurrentUserService currentUser) =>
        {
            if (!currentUser.IsPlatformAdmin) return Results.Forbid();
            var result = await mediator.Send(new GetTenantFeatureOverridesQuery(id));
            return Results.Ok(result);
        });

        group.MapPut("/{id:guid}/features", async (Guid id, UpdateTenantFeaturesRequest req, IMediator mediator, ICurrentUserService currentUser) =>
        {
            if (!currentUser.IsPlatformAdmin) return Results.Forbid();
            await mediator.Send(new UpdateTenantFeaturesCommand(id,
                req.Features.Select(f => new TenantFeatureUpdate(f.FeatureDefinitionId, f.IsEnabled)).ToList()));
            return Results.NoContent();
        });
    }

    record SuspendRequest(string? Reason);

    record UpdateSubscriptionRequest(
        SubscriptionPlan Plan,
        BillingCycle BillingCycle,
        decimal MonthlyPrice,
        decimal? DiscountPercent);

    record UpdateIsolationRequest(
        DataIsolationStrategy IsolationStrategy,
        string? ConnectionString,
        string? DatabaseSchema);

    record CreateFeatureDefinitionRequest(
        string Code, string DisplayName, string? Description,
        string Icon, string Category, int SortOrder);

    record UpdateFeatureDefinitionRequest(
        string DisplayName, string? Description,
        string Icon, string Category, int SortOrder, bool IsActive);

    record CreatePlanDefinitionRequest(
        SubscriptionPlan Plan, string DisplayName, string? Description,
        decimal MonthlyPrice, string Currency, string Duration,
        int MaxUsers, int MaxPatientsPerMonth, long StorageLimitMb,
        int SortOrder, bool IsFeatured);

    record UpdatePlanDefinitionRequest(
        string DisplayName, string? Description, decimal MonthlyPrice,
        string Duration, int MaxUsers, int MaxPatientsPerMonth,
        long StorageLimitMb, int SortOrder, bool IsFeatured, bool IsActive);

    record UpdatePlanFeaturesRequest(List<Guid> FeatureDefinitionIds);

    record UpdateTenantFeaturesRequest(List<TenantFeatureUpdateItem> Features);
    record TenantFeatureUpdateItem(Guid FeatureDefinitionId, bool IsEnabled);
    record ResetPasswordRequest(string NewPassword);
}
