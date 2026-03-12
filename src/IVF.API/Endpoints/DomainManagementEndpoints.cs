using IVF.Application.Common.Interfaces;
using IVF.Application.Features.DomainManagement.Commands;
using IVF.Application.Features.DomainManagement.Queries;
using MediatR;

namespace IVF.API.Endpoints;

public static class DomainManagementEndpoints
{
    public static void MapDomainManagementEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/domains")
            .WithTags("Domain Management")
            .RequireAuthorization();

        // GET /api/admin/domains — list all tenant domains
        group.MapGet("/", async (IMediator mediator, ICurrentUserService currentUser) =>
        {
            if (!currentUser.IsPlatformAdmin)
                return Results.Forbid();

            var result = await mediator.Send(new GetTenantDomainsQuery());
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        })
        .WithSummary("Danh sách domain của tất cả tenant");

        // GET /api/admin/domains/preview — preview generated Caddyfile
        group.MapGet("/preview", async (IMediator mediator, ICurrentUserService currentUser) =>
        {
            if (!currentUser.IsPlatformAdmin)
                return Results.Forbid();

            var result = await mediator.Send(new GetCaddyfilePreviewQuery());
            return result.IsSuccess
                ? Results.Text(result.Value, "text/plain")
                : Results.BadRequest(result.Error);
        })
        .WithSummary("Xem trước Caddyfile sẽ được tạo");

        // GET /api/admin/domains/current — get current running Caddy config (JSON)
        group.MapGet("/current", async (IMediator mediator, ICurrentUserService currentUser) =>
        {
            if (!currentUser.IsPlatformAdmin)
                return Results.Forbid();

            var result = await mediator.Send(new GetCurrentCaddyConfigQuery());
            return result.IsSuccess
                ? Results.Text(result.Value, "application/json")
                : Results.BadRequest(result.Error);
        })
        .WithSummary("Cấu hình Caddy đang chạy (JSON)");

        // POST /api/admin/domains/sync — sync Caddy config from DB
        group.MapPost("/sync", async (IMediator mediator, ICurrentUserService currentUser) =>
        {
            if (!currentUser.IsPlatformAdmin)
                return Results.Forbid();

            var result = await mediator.Send(new SyncCaddyConfigCommand());
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        })
        .WithSummary("Đồng bộ cấu hình Caddy từ database");
    }
}
