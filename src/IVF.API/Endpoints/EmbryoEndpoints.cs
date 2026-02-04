using IVF.API.Contracts;
using IVF.Application.Features.Embryos.Commands;
using IVF.Application.Features.Embryos.Queries;
using MediatR;

namespace IVF.API.Endpoints;

public static class EmbryoEndpoints
{
    public static void MapEmbryoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/embryos").WithTags("Embryos").RequireAuthorization();

        group.MapGet("/cycle/{cycleId:guid}", async (Guid cycleId, IMediator m) =>
            Results.Ok(await m.Send(new GetEmbryosByCycleQuery(cycleId))));

        group.MapGet("/active", async (IMediator m) =>
            Results.Ok(await m.Send(new GetActiveEmbryosQuery())));

        group.MapGet("/cryo-stats", async (IMediator m) =>
            Results.Ok(await m.Send(new GetCryoStorageStatsQuery())));

        group.MapPost("/", async (CreateEmbryoCommand cmd, IMediator m) =>
        {
            var r = await m.Send(cmd);
            return r.IsSuccess ? Results.Created($"/api/embryos/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPut("/{id:guid}/grade", async (Guid id, UpdateGradeRequest req, IMediator m) =>
        {
            var r = await m.Send(new UpdateEmbryoGradeCommand(id, req.Grade, req.Day));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapPost("/{id:guid}/transfer", async (Guid id, IMediator m) =>
        {
            var r = await m.Send(new TransferEmbryoCommand(id));
            return r.IsSuccess ? Results.NoContent() : Results.NotFound(r.Error);
        });

        group.MapPost("/{id:guid}/freeze", async (Guid id, FreezeRequest req, IMediator m) =>
        {
            var r = await m.Send(new FreezeEmbryoCommand(id, req.CryoLocationId));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPost("/{id:guid}/thaw", async (Guid id, IMediator m) =>
        {
            var r = await m.Send(new ThawEmbryoCommand(id));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });
    }
}
