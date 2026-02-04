using IVF.Application.Features.Users.Commands;
using IVF.Application.Features.Users.Queries;
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
