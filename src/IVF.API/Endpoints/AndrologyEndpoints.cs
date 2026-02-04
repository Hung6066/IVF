using IVF.API.Contracts;
using IVF.Application.Features.Andrology.Commands;
using IVF.Application.Features.Andrology.Queries;
using MediatR;

namespace IVF.API.Endpoints;

public static class AndrologyEndpoints
{
    public static void MapAndrologyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/andrology").WithTags("Andrology").RequireAuthorization();

        group.MapGet("/patient/{patientId:guid}", async (Guid patientId, IMediator m) =>
            Results.Ok(await m.Send(new GetAnalysesByPatientQuery(patientId))));

        group.MapGet("/cycle/{cycleId:guid}", async (Guid cycleId, IMediator m) =>
            Results.Ok(await m.Send(new GetAnalysesByCycleQuery(cycleId))));

        group.MapPost("/", async (CreateSemenAnalysisCommand cmd, IMediator m) =>
        {
            var r = await m.Send(cmd);
            return r.IsSuccess ? Results.Created($"/api/andrology/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPut("/{id:guid}/macroscopic", async (Guid id, RecordMacroscopicRequest req, IMediator m) =>
        {
            var r = await m.Send(new RecordMacroscopicCommand(id, req.Volume, req.Appearance, req.Liquefaction, req.Ph));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapPut("/{id:guid}/microscopic", async (Guid id, RecordMicroscopicRequest req, IMediator m) =>
        {
            var r = await m.Send(new RecordMicroscopicCommand(id, req.Concentration, req.TotalCount, req.ProgressiveMotility,
                req.NonProgressiveMotility, req.Immotile, req.NormalMorphology, req.Vitality));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });
    }
}
