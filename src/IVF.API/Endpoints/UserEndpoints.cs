using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Users.Commands;
using IVF.Application.Features.Users.Queries;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IVF.API.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").WithTags("Users").RequireAuthorization("AdminOnly");

        // Get Users (Search)
        group.MapGet("/", async (IMediator m, string? q, string? role, bool? isActive, int page = 1, int pageSize = 20) =>
        {
            var result = await m.Send(new GetUsersQuery(q, role, isActive, page, pageSize));
            return Results.Ok(result);
        });

        // Get Roles
        group.MapGet("/roles", () =>
        {
            var roles = Enum.GetNames(typeof(UserRole));
            return Results.Ok(roles);
        });

        // Get All Available Permissions (now from PermissionDefinition table)
        group.MapGet("/permissions", async (IPermissionDefinitionRepository defRepo) =>
        {
            var defs = await defRepo.GetActiveAsync();
            var permissions = defs.Select(p => new { name = p.Code, value = p.Code }).ToList();
            return Results.Ok(permissions);
        });

        // Get User Permissions
        group.MapGet("/{id}/permissions", async (Guid id, IUserPermissionRepository repo) =>
        {
            var permissions = await repo.GetByUserIdAsync(id);
            return Results.Ok(permissions.Select(p => p.PermissionCode));
        });

        // Assign Permissions to User
        group.MapPost("/{id}/permissions", async (Guid id, [FromBody] AssignPermissionsRequest request, IUserPermissionRepository repo, IUnitOfWork uow) =>
        {
            // Remove existing permissions
            await repo.DeleteAllByUserIdAsync(id);

            // Add new permissions (string-based, supports dynamic permissions)
            var permissions = request.Permissions
                .Select(p => UserPermission.Create(id, p, request.GrantedBy))
                .ToList();

            await repo.AddRangeAsync(permissions);
            await uow.SaveChangesAsync();

            return Results.Ok(new { message = "Permissions updated", count = permissions.Count });
        });

        // Revoke Single Permission
        group.MapDelete("/{id}/permissions/{permission}", async (Guid id, string permission, IUserPermissionRepository repo, IUnitOfWork uow) =>
        {
            await repo.DeleteAsync(id, permission);
            await uow.SaveChangesAsync();
            return Results.NoContent();
        });

        // Create User
        group.MapPost("/", async (IMediator m, [FromBody] CreateUserCommand command) =>
        {
            var id = await m.Send(command);
            return Results.Created($"/api/users/{id}", new { id });
        });

        // Update User
        group.MapPut("/{id}", async (IMediator m, Guid id, [FromBody] UpdateUserCommand command) =>
        {
            if (id != command.Id) return Results.BadRequest();
            await m.Send(command);
            return Results.NoContent();
        });

        // Delete (Deactivate) User
        group.MapDelete("/{id}", async (IMediator m, Guid id) =>
        {
            await m.Send(new DeleteUserCommand(id));
            return Results.NoContent();
        });
    }
}

public record AssignPermissionsRequest(List<string> Permissions, Guid? GrantedBy);

