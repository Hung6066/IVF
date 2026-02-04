using IVF.API.Contracts;
using IVF.Application.Features.Ultrasounds.Commands;
using IVF.Application.Features.Ultrasounds.Queries;
using MediatR;

namespace IVF.API.Endpoints;

public static class UltrasoundEndpoints
{
    public static void MapUltrasoundEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ultrasounds").WithTags("Ultrasounds").RequireAuthorization("DoctorOrAdmin");

        group.MapGet("/cycle/{cycleId:guid}", async (Guid cycleId, IMediator m) =>
            Results.Ok(await m.Send(new GetUltrasoundsByCycleQuery(cycleId))));

        group.MapPost("/", async (CreateUltrasoundCommand cmd, IMediator m) =>
        {
            var r = await m.Send(cmd);
            return r.IsSuccess ? Results.Created($"/api/ultrasounds/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPut("/{id:guid}/follicles", async (Guid id, RecordFolliclesRequest req, IMediator m) =>
        {
            var r = await m.Send(new RecordFolliclesCommand(id, req.LeftOvaryCount, req.RightOvaryCount, 
                req.LeftFollicles, req.RightFollicles, req.EndometriumThickness, req.Findings));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });
    }
}
