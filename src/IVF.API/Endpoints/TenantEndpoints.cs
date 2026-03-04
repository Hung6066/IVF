using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Tenants.Commands;
using IVF.Application.Features.Tenants.Queries;
using IVF.Application.Features.Pricing.Queries;
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

        group.MapGet("/pricing", async (IMediator mediator) =>
        {
            var plans = await mediator.Send(new GetDynamicPricingQuery());
            return Results.Ok(plans);
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
}
