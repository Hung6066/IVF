using System.Security.Claims;
using IVF.API.Contracts;
using IVF.Application.Features.Queue.Commands;
using IVF.Application.Features.Queue.Queries;
using MediatR;

namespace IVF.API.Endpoints;

public static class QueueEndpoints
{
    public static void MapQueueEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/queue").WithTags("Queue").RequireAuthorization();

        group.MapGet("/{departmentCode}", async (string departmentCode, IMediator m) =>
            Results.Ok(await m.Send(new GetQueueByDepartmentQuery(departmentCode))));

        group.MapPost("/issue", async (IssueTicketCommand cmd, IMediator m) =>
        {
            var r = await m.Send(cmd);
            return r.IsSuccess ? Results.Created($"/api/queue/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPost("/{id:guid}/call", async (Guid id, ClaimsPrincipal principal, IMediator m) =>
        {
            var userId = Guid.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var r = await m.Send(new CallTicketCommand(id, userId));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapPost("/{id:guid}/complete", async (Guid id, IMediator m) =>
        {
            var r = await m.Send(new CompleteTicketCommand(id));
            return r.IsSuccess ? Results.NoContent() : Results.NotFound(r.Error);
        });

        group.MapPost("/{id:guid}/skip", async (Guid id, IMediator m) =>
        {
            var r = await m.Send(new SkipTicketCommand(id));
            return r.IsSuccess ? Results.NoContent() : Results.NotFound(r.Error);
        });
    }
}
