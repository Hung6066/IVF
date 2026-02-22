using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace IVF.API.Endpoints;

public static class PermissionDefinitionEndpoints
{
    public static void MapPermissionDefinitionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/permission-definitions")
            .WithTags("PermissionDefinitions");

        // ─── Public: get active permissions grouped ───
        group.MapGet("/", async (IPermissionDefinitionRepository repo) =>
        {
            var all = await repo.GetActiveAsync();
            var grouped = all
                .GroupBy(p => new { p.GroupCode, p.GroupDisplayName, p.GroupIcon, p.GroupSortOrder })
                .OrderBy(g => g.Key.GroupSortOrder)
                .Select(g => new
                {
                    groupCode = g.Key.GroupCode,
                    groupName = g.Key.GroupDisplayName,
                    groupIcon = g.Key.GroupIcon,
                    groupSortOrder = g.Key.GroupSortOrder,
                    permissions = g.OrderBy(p => p.SortOrder).Select(p => new
                    {
                        id = p.Id,
                        code = p.Code,
                        displayName = p.DisplayName,
                        sortOrder = p.SortOrder
                    })
                });

            return Results.Ok(grouped);
        }).RequireAuthorization();

        // ─── Admin: get all (including inactive) ───
        group.MapGet("/all", async (IPermissionDefinitionRepository repo) =>
        {
            var all = await repo.GetAllAsync();
            return Results.Ok(all.Select(p => new
            {
                id = p.Id,
                code = p.Code,
                displayName = p.DisplayName,
                groupCode = p.GroupCode,
                groupDisplayName = p.GroupDisplayName,
                groupIcon = p.GroupIcon,
                sortOrder = p.SortOrder,
                groupSortOrder = p.GroupSortOrder,
                isActive = p.IsActive
            }));
        }).RequireAuthorization("AdminOnly");

        // ─── Admin: create new permission definition ───
        group.MapPost("/", async (
            [FromBody] CreatePermissionDefinitionRequest request,
            IPermissionDefinitionRepository repo,
            IUnitOfWork uow) =>
        {
            var existing = await repo.GetByCodeAsync(request.Code);
            if (existing != null)
                return Results.Conflict(new { message = $"Permission code '{request.Code}' already exists" });

            var entity = PermissionDefinition.Create(
                request.Code,
                request.DisplayName,
                request.GroupCode,
                request.GroupDisplayName,
                request.GroupIcon,
                request.SortOrder,
                request.GroupSortOrder);

            await repo.AddAsync(entity);
            await uow.SaveChangesAsync();

            return Results.Created($"/api/permission-definitions/{entity.Id}", new { id = entity.Id });
        }).RequireAuthorization("AdminOnly");

        // ─── Admin: update permission definition ───
        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdatePermissionDefinitionRequest request,
            IPermissionDefinitionRepository repo,
            IUnitOfWork uow) =>
        {
            var entity = await repo.GetByIdAsync(id);
            if (entity == null)
                return Results.NotFound();

            entity.Update(
                request.DisplayName,
                request.GroupCode,
                request.GroupDisplayName,
                request.GroupIcon,
                request.SortOrder,
                request.GroupSortOrder);

            await repo.UpdateAsync(entity);
            await uow.SaveChangesAsync();

            return Results.Ok(new { message = "Updated" });
        }).RequireAuthorization("AdminOnly");

        // ─── Admin: toggle active/inactive ───
        group.MapPatch("/{id:guid}/toggle", async (
            Guid id,
            IPermissionDefinitionRepository repo,
            IUnitOfWork uow) =>
        {
            var entity = await repo.GetByIdAsync(id);
            if (entity == null)
                return Results.NotFound();

            if (entity.IsActive)
                entity.Deactivate();
            else
                entity.Activate();

            await repo.UpdateAsync(entity);
            await uow.SaveChangesAsync();

            return Results.Ok(new { isActive = entity.IsActive });
        }).RequireAuthorization("AdminOnly");

        // ─── Admin: soft delete ───
        group.MapDelete("/{id:guid}", async (
            Guid id,
            IPermissionDefinitionRepository repo,
            IUnitOfWork uow) =>
        {
            var entity = await repo.GetByIdAsync(id);
            if (entity == null)
                return Results.NotFound();

            entity.MarkAsDeleted();
            await repo.UpdateAsync(entity);
            await uow.SaveChangesAsync();

            return Results.NoContent();
        }).RequireAuthorization("AdminOnly");
    }
}

public record CreatePermissionDefinitionRequest(
    string Code,
    string DisplayName,
    string GroupCode,
    string GroupDisplayName,
    string GroupIcon,
    int SortOrder,
    int GroupSortOrder);

public record UpdatePermissionDefinitionRequest(
    string DisplayName,
    string GroupCode,
    string GroupDisplayName,
    string GroupIcon,
    int SortOrder,
    int GroupSortOrder);
