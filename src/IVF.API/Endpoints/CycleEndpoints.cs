using IVF.API.Contracts;
using IVF.Application.Features.Cycles.Commands;
using IVF.Application.Features.Cycles.Queries;
using MediatR;

namespace IVF.API.Endpoints;

public static class CycleEndpoints
{
    public static void MapCycleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cycles").WithTags("Cycles").RequireAuthorization();

        group.MapGet("/{id:guid}", async (Guid id, IMediator m) =>
        {
            var r = await m.Send(new GetCycleByIdQuery(id));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapGet("/couple/{coupleId:guid}", async (Guid coupleId, IMediator m) =>
            Results.Ok(await m.Send(new GetCyclesByCoupleQuery(coupleId))));

        group.MapPost("/", async (CreateCycleCommand cmd, IMediator m) =>
        {
            var r = await m.Send(cmd);
            return r.IsSuccess ? Results.Created($"/api/cycles/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPut("/{id:guid}/phase", async (Guid id, AdvancePhaseRequest req, IMediator m) =>
        {
            var r = await m.Send(new AdvancePhaseCommand(id, req.Phase));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapPost("/{id:guid}/complete", async (Guid id, CompleteRequest req, IMediator m) =>
        {
            var r = await m.Send(new CompleteCycleCommand(id, req.Outcome));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapPost("/{id:guid}/cancel", async (Guid id, IMediator m) =>
        {
            var r = await m.Send(new CancelCycleCommand(id));
            return r.IsSuccess ? Results.NoContent() : Results.NotFound(r.Error);
        });
    }
}
