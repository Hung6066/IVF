using IVF.API.Contracts;
using IVF.Application.Features.SpermBank.Commands;
using IVF.Application.Features.SpermBank.Queries;
using MediatR;

namespace IVF.API.Endpoints;

public static class SpermBankEndpoints
{
    public static void MapSpermBankEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/spermbank").WithTags("SpermBank").RequireAuthorization();

        group.MapGet("/donors", async (IMediator m, string? q, int page = 1, int pageSize = 20) =>
            Results.Ok(await m.Send(new SearchDonorsQuery(q, page, pageSize))));

        group.MapGet("/donors/{id:guid}", async (Guid id, IMediator m) =>
        {
            var r = await m.Send(new GetDonorByIdQuery(id));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapPost("/donors", async (CreateDonorCommand cmd, IMediator m) =>
        {
            var r = await m.Send(cmd);
            return r.IsSuccess ? Results.Created($"/api/spermbank/donors/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPut("/donors/{id:guid}/profile", async (Guid id, UpdateDonorProfileRequest req, IMediator m) =>
        {
            var r = await m.Send(new UpdateDonorProfileCommand(id, req.BloodType, req.Height, req.Weight, 
                req.EyeColor, req.HairColor, req.Ethnicity, req.Education, req.Occupation));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapGet("/samples/donor/{donorId:guid}", async (Guid donorId, IMediator m) =>
            Results.Ok(await m.Send(new GetSamplesByDonorQuery(donorId))));

        group.MapGet("/samples/available", async (IMediator m) =>
            Results.Ok(await m.Send(new GetAvailableSamplesQuery())));

        group.MapPost("/samples", async (CreateSampleCommand cmd, IMediator m) =>
        {
            var r = await m.Send(cmd);
            return r.IsSuccess ? Results.Created($"/api/spermbank/samples/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPut("/samples/{id:guid}/quality", async (Guid id, RecordQualityRequest req, IMediator m) =>
        {
            var r = await m.Send(new RecordSampleQualityCommand(id, req.Volume, req.Concentration, req.Motility, req.VialCount));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });
    }
}
