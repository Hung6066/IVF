using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Tenants.Commands;
using IVF.Application.Features.Tenants.Queries;
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

        group.MapGet("/pricing", () =>
        {
            var plans = new object[]
            {
                new {
                    Plan = "Trial",
                    Price = 0,
                    Currency = "VND",
                    Duration = "30 ngày",
                    MaxUsers = 3,
                    MaxPatients = 20,
                    StorageGb = 0.5,
                    Features = new[] { "Quản lý bệnh nhân", "Lịch hẹn", "Hàng đợi", "Biểu mẫu cơ bản" }
                },
                new {
                    Plan = "Starter",
                    Price = 5_000_000,
                    Currency = "VND",
                    Duration = "Tháng",
                    MaxUsers = 10,
                    MaxPatients = 100,
                    StorageGb = 5,
                    Features = new[] { "Tất cả Trial", "Báo cáo nâng cao", "Export PDF", "Hỗ trợ email" }
                },
                new {
                    Plan = "Professional",
                    Price = 15_000_000,
                    Currency = "VND",
                    Duration = "Tháng",
                    MaxUsers = 30,
                    MaxPatients = 500,
                    StorageGb = 20,
                    Features = new[] { "Tất cả Starter", "AI hỗ trợ", "Ký số", "HIPAA/GDPR", "Hỗ trợ ưu tiên" }
                },
                new {
                    Plan = "Enterprise",
                    Price = 35_000_000,
                    Currency = "VND",
                    Duration = "Tháng",
                    MaxUsers = 100,
                    MaxPatients = 2000,
                    StorageGb = 100,
                    Features = new[] { "Tất cả Professional", "Sinh trắc học", "SSO/SAML", "SLA 99.9%", "Hỗ trợ 24/7", "Custom domain" }
                }
            };
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
            if (currentUser.IsPlatformAdmin)
            {
                return Results.Ok(new TenantFeatures(
                    IsPlatformAdmin: true,
                    CanManageTenants: true,
                    CanViewPlatformStats: true,
                    CanManageCompliance: true,
                    CanManageSecurity: true,
                    CanManageBackups: true,
                    CanManageUsers: true,
                    CanManageForms: true,
                    CanViewReports: true,
                    CanUseAi: true,
                    CanUseDigitalSigning: true,
                    CanUseBiometrics: true,
                    CanUseAdvancedReporting: true,
                    IsolationStrategy: DataIsolationStrategy.SharedDatabase,
                    MaxUsers: 999,
                    MaxPatients: 99999));
            }

            if (currentUser.TenantId is not { } tenantId || tenantId == Guid.Empty)
                return Results.Forbid();

            var tenant = await mediator.Send(new GetTenantByIdQuery(tenantId));
            if (tenant is null) return Results.NotFound();

            return Results.Ok(new TenantFeatures(
                IsPlatformAdmin: false,
                CanManageTenants: false,
                CanViewPlatformStats: false,
                CanManageCompliance: false,
                CanManageSecurity: false,
                CanManageBackups: false,
                CanManageUsers: true,
                CanManageForms: true,
                CanViewReports: true,
                CanUseAi: tenant.AiEnabled,
                CanUseDigitalSigning: tenant.DigitalSigningEnabled,
                CanUseBiometrics: tenant.BiometricsEnabled,
                CanUseAdvancedReporting: tenant.AdvancedReportingEnabled,
                IsolationStrategy: tenant.IsolationStrategy,
                MaxUsers: tenant.MaxUsers,
                MaxPatients: tenant.MaxPatientsPerMonth));
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

    record TenantFeatures(
        bool IsPlatformAdmin,
        bool CanManageTenants,
        bool CanViewPlatformStats,
        bool CanManageCompliance,
        bool CanManageSecurity,
        bool CanManageBackups,
        bool CanManageUsers,
        bool CanManageForms,
        bool CanViewReports,
        bool CanUseAi,
        bool CanUseDigitalSigning,
        bool CanUseBiometrics,
        bool CanUseAdvancedReporting,
        DataIsolationStrategy IsolationStrategy,
        int MaxUsers,
        int MaxPatients);
}
