using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace IVF.API.Endpoints;

public static class MenuEndpoints
{
    public static void MapMenuEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/menu").WithTags("Menu");

        // ── Public: Get active menu items (for sidebar rendering) ──
        group.MapGet("/", async (IMenuItemRepository repo) =>
        {
            var items = await repo.GetActiveAsync();

            // Group by section for the frontend
            var sections = items
                .GroupBy(i => i.Section ?? "")
                .OrderBy(g => g.Key == "" ? 0 : 1) // main menu first
                .Select(g => new
                {
                    section = g.Key == "" ? null : g.Key,
                    header = g.FirstOrDefault(i => i.SectionHeader != null)?.SectionHeader,
                    adminOnly = g.All(i => i.AdminOnly),
                    items = g.OrderBy(i => i.SortOrder).Select(i => new
                    {
                        i.Id,
                        i.Icon,
                        i.Label,
                        i.Route,
                        i.Permission,
                        i.AdminOnly,
                        i.SortOrder
                    })
                });

            return Results.Ok(sections);
        }).RequireAuthorization();

        // ── Admin: Get all menu items (including inactive) ──
        group.MapGet("/all", async (IMenuItemRepository repo) =>
        {
            var items = await repo.GetAllAsync();
            return Results.Ok(items.Select(i => new
            {
                i.Id,
                i.Section,
                i.SectionHeader,
                i.Icon,
                i.Label,
                i.Route,
                i.Permission,
                i.AdminOnly,
                i.SortOrder,
                i.IsActive,
                i.CreatedAt,
                i.UpdatedAt
            }));
        }).RequireAuthorization("AdminOnly");

        // ── Admin: Create menu item ──
        group.MapPost("/", async (
            [FromBody] CreateMenuItemRequest request,
            IMenuItemRepository repo,
            IUnitOfWork uow) =>
        {
            var existing = await repo.GetByRouteAsync(request.Route);
            if (existing != null)
                return Results.BadRequest(new { message = "Route đã tồn tại" });

            var item = MenuItem.Create(
                request.Section,
                request.SectionHeader,
                request.Icon,
                request.Label,
                request.Route,
                request.Permission,
                request.AdminOnly,
                request.SortOrder,
                request.IsActive);

            await repo.AddAsync(item);
            await uow.SaveChangesAsync();

            return Results.Created($"/api/menu/{item.Id}", new { id = item.Id });
        }).RequireAuthorization("AdminOnly");

        // ── Admin: Update menu item ──
        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateMenuItemRequest request,
            IMenuItemRepository repo,
            IUnitOfWork uow) =>
        {
            var item = await repo.GetByIdAsync(id);
            if (item == null) return Results.NotFound();

            // Check route uniqueness if changed
            if (item.Route != request.Route)
            {
                var existing = await repo.GetByRouteAsync(request.Route);
                if (existing != null)
                    return Results.BadRequest(new { message = "Route đã tồn tại" });
            }

            item.Update(
                request.Section,
                request.SectionHeader,
                request.Icon,
                request.Label,
                request.Route,
                request.Permission,
                request.AdminOnly,
                request.SortOrder,
                request.IsActive);

            await repo.UpdateAsync(item);
            await uow.SaveChangesAsync();

            return Results.Ok();
        }).RequireAuthorization("AdminOnly");

        // ── Admin: Delete (soft delete) menu item ──
        group.MapDelete("/{id:guid}", async (
            Guid id,
            IMenuItemRepository repo,
            IUnitOfWork uow) =>
        {
            var item = await repo.GetByIdAsync(id);
            if (item == null) return Results.NotFound();

            item.MarkAsDeleted();
            await repo.UpdateAsync(item);
            await uow.SaveChangesAsync();

            return Results.NoContent();
        }).RequireAuthorization("AdminOnly");

        // ── Admin: Toggle active status ──
        group.MapPatch("/{id:guid}/toggle", async (
            Guid id,
            IMenuItemRepository repo,
            IUnitOfWork uow) =>
        {
            var item = await repo.GetByIdAsync(id);
            if (item == null) return Results.NotFound();

            if (item.IsActive)
                item.Deactivate();
            else
                item.Activate();

            await repo.UpdateAsync(item);
            await uow.SaveChangesAsync();

            return Results.Ok(new { isActive = item.IsActive });
        }).RequireAuthorization("AdminOnly");

        // ── Admin: Reorder menu items ──
        group.MapPut("/reorder", async (
            [FromBody] ReorderMenuRequest request,
            IMenuItemRepository repo,
            IUnitOfWork uow) =>
        {
            foreach (var entry in request.Items)
            {
                var item = await repo.GetByIdAsync(entry.Id);
                if (item != null)
                {
                    item.SetOrder(entry.SortOrder);
                    await repo.UpdateAsync(item);
                }
            }

            await uow.SaveChangesAsync();
            return Results.Ok();
        }).RequireAuthorization("AdminOnly");
    }
}

public record CreateMenuItemRequest(
    string? Section,
    string? SectionHeader,
    string Icon,
    string Label,
    string Route,
    string? Permission,
    bool AdminOnly,
    int SortOrder,
    bool IsActive = true);

public record UpdateMenuItemRequest(
    string? Section,
    string? SectionHeader,
    string Icon,
    string Label,
    string Route,
    string? Permission,
    bool AdminOnly,
    int SortOrder,
    bool IsActive);

public record ReorderMenuRequest(List<ReorderEntry> Items);
public record ReorderEntry(Guid Id, int SortOrder);
