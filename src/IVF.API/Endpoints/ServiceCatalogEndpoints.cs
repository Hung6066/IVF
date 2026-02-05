using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace IVF.API.Endpoints;

public static class ServiceCatalogEndpoints
{
    public static void MapServiceCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/services").WithTags("Services");

        // Get all services (public for selection)
        group.MapGet("/", async (
            IServiceCatalogRepository repo,
            string? q,
            string? category,
            bool? isActive,
            int page = 1,
            int pageSize = 50) =>
        {
            ServiceCategory? cat = null;
            if (!string.IsNullOrEmpty(category) && Enum.TryParse<ServiceCategory>(category, out var parsed))
                cat = parsed;

            var items = await repo.SearchAsync(q, cat, page, pageSize);
            var total = await repo.CountAsync(q, cat);
            
            return Results.Ok(new
            {
                items = items.Select(s => new
                {
                    s.Id,
                    s.Code,
                    s.Name,
                    category = s.Category.ToString(),
                    s.UnitPrice,
                    s.Unit,
                    s.Description,
                    s.IsActive
                }),
                total,
                page,
                pageSize
            });
        });

        // Get categories
        group.MapGet("/categories", () =>
        {
            var categories = Enum.GetValues<ServiceCategory>()
                .Select(c => new { name = c.ToString(), value = (int)c })
                .ToList();
            return Results.Ok(categories);
        });

        // Get by ID
        group.MapGet("/{id:guid}", async (Guid id, IServiceCatalogRepository repo) =>
        {
            var service = await repo.GetByIdAsync(id);
            if (service == null) return Results.NotFound();
            return Results.Ok(new
            {
                service.Id,
                service.Code,
                service.Name,
                category = service.Category.ToString(),
                service.UnitPrice,
                service.Unit,
                service.Description,
                service.IsActive
            });
        });

        // Create (Admin only)
        group.MapPost("/", async (
            [FromBody] CreateServiceRequest request,
            IServiceCatalogRepository repo,
            IUnitOfWork uow) =>
        {
            var existing = await repo.GetByCodeAsync(request.Code);
            if (existing != null)
                return Results.BadRequest(new { message = "Mã dịch vụ đã tồn tại" });

            if (!Enum.TryParse<ServiceCategory>(request.Category, out var category))
                return Results.BadRequest(new { message = "Danh mục không hợp lệ" });

            var service = ServiceCatalog.Create(
                request.Code,
                request.Name,
                category,
                request.UnitPrice,
                request.Unit ?? "lần",
                request.Description);

            await repo.AddAsync(service);
            await uow.SaveChangesAsync();

            return Results.Created($"/api/services/{service.Id}", new { id = service.Id });
        }).RequireAuthorization("AdminOnly");

        // Update
        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateServiceRequest request,
            IServiceCatalogRepository repo,
            IUnitOfWork uow) =>
        {
            var service = await repo.GetByIdAsync(id);
            if (service == null) return Results.NotFound();

            if (!Enum.TryParse<ServiceCategory>(request.Category, out var category))
                return Results.BadRequest(new { message = "Danh mục không hợp lệ" });

            service.Update(request.Name, category, request.UnitPrice, request.Unit ?? "lần", request.Description);
            await repo.UpdateAsync(service);
            await uow.SaveChangesAsync();

            return Results.Ok();
        }).RequireAuthorization("AdminOnly");

        // Toggle active status
        group.MapPatch("/{id:guid}/toggle", async (
            Guid id,
            IServiceCatalogRepository repo,
            IUnitOfWork uow) =>
        {
            var service = await repo.GetByIdAsync(id);
            if (service == null) return Results.NotFound();

            if (service.IsActive)
                service.Deactivate();
            else
                service.Activate();

            await repo.UpdateAsync(service);
            await uow.SaveChangesAsync();

            return Results.Ok(new { isActive = service.IsActive });
        }).RequireAuthorization("AdminOnly");
    }
}

public record CreateServiceRequest(string Code, string Name, string Category, decimal UnitPrice, string? Unit, string? Description);
public record UpdateServiceRequest(string Name, string Category, decimal UnitPrice, string? Unit, string? Description);
