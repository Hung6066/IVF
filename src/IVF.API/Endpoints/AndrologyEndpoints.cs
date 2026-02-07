using IVF.API.Contracts;
using IVF.Application.Features.Andrology.Commands;
using IVF.Application.Features.Andrology.Queries;
using MediatR;



namespace IVF.API.Endpoints;

public static class AndrologyEndpoints
{
    public static void MapAndrologyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/andrology").WithTags("Andrology").RequireAuthorization("AndrologyAccess");

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
        
        // New Endpoints
        group.MapGet("/analyses", async (IMediator m, string? q, DateTime? from, DateTime? to, string? status, int page = 1, int pageSize = 20) =>
        {
            var r = await m.Send(new SearchSemenAnalysesQuery(q, from, to, status, page, pageSize));
            return Results.Ok(new { Items = r.Items, Total = r.Total });
        });

        group.MapGet("/statistics", async (IMediator m) =>
            Results.Ok(await m.Send(new GetAndrologyStatisticsQuery())));

        group.MapGet("/washings", async (IMediator m, string? method, DateTime? from, DateTime? to, int page = 1, int pageSize = 20) =>
        {
            var r = await m.Send(new SearchSpermWashingsQuery(method, from, to, page, pageSize));
            return Results.Ok(new { Items = r.Items, Total = r.Total });
        });

        group.MapPost("/washings", async (CreateSpermWashingCommand cmd, IMediator m) =>
        {
            var r = await m.Send(cmd);
            return r.IsSuccess ? Results.Created($"/api/andrology/washings/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPut("/washings/{id:guid}", async (Guid id, UpdateSpermWashingRequest req, IMediator m) =>
        {
            var r = await m.Send(new UpdateSpermWashingCommand(id, req.Notes, req.PreWashConcentration, req.PostWashConcentration, req.PostWashMotility));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });
    }
}

public record UpdateSpermWashingRequest(string? Notes, decimal? PreWashConcentration, decimal? PostWashConcentration, decimal? PostWashMotility);
