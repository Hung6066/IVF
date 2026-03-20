using IVF.Application.Features.EggBank.Commands;
using IVF.Application.Features.EggBank.Queries;
using MediatR;

namespace IVF.API.Endpoints;

public static class EggBankEndpoints
{
    public static void MapEggBankEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/eggbank").WithTags("EggBank").RequireAuthorization("LabAccess");

        // Donors
        group.MapGet("/donors", async (IMediator m, string? q, int page = 1, int pageSize = 20) =>
            Results.Ok(await m.Send(new SearchEggDonorsQuery(q, page, pageSize))));

        group.MapGet("/donors/{id:guid}", async (Guid id, IMediator m) =>
        {
            var r = await m.Send(new GetEggDonorByIdQuery(id));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapPost("/donors", async (CreateEggDonorCommand cmd, IMediator m) =>
        {
            var r = await m.Send(cmd);
            return r.IsSuccess ? Results.Created($"/api/eggbank/donors/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPut("/donors/{id:guid}/profile", async (Guid id, UpdateEggDonorProfileRequest req, IMediator m) =>
        {
            var r = await m.Send(new UpdateEggDonorProfileCommand(id, req.BloodType, req.Height, req.Weight,
                req.EyeColor, req.HairColor, req.Ethnicity, req.Education, req.Occupation,
                req.AmhLevel, req.AntralFollicleCount, req.MenstrualHistory));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        // Oocyte Samples
        group.MapGet("/samples/donor/{donorId:guid}", async (Guid donorId, IMediator m) =>
            Results.Ok(await m.Send(new GetOocyteSamplesByDonorQuery(donorId))));

        group.MapGet("/samples/available", async (IMediator m) =>
            Results.Ok(await m.Send(new GetAvailableOocyteSamplesQuery())));

        group.MapPost("/samples", async (CreateOocyteSampleCommand cmd, IMediator m) =>
        {
            var r = await m.Send(cmd);
            return r.IsSuccess ? Results.Created($"/api/eggbank/samples/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPut("/samples/{id:guid}/quality", async (Guid id, RecordOocyteQualityRequest req, IMediator m) =>
        {
            var r = await m.Send(new RecordOocyteQualityCommand(id, req.TotalOocytes, req.MatureOocytes,
                req.ImmatureOocytes, req.DegeneratedOocytes, req.Notes));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapPut("/samples/{id:guid}/vitrify", async (Guid id, VitrifyOocytesRequest req, IMediator m) =>
        {
            var r = await m.Send(new VitrifyOocytesCommand(id, req.Count, req.FreezeDate, req.CryoLocationId));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });
    }
}

// Request DTOs
public record UpdateEggDonorProfileRequest(
    string? BloodType, decimal? Height, decimal? Weight,
    string? EyeColor, string? HairColor, string? Ethnicity,
    string? Education, string? Occupation,
    int? AmhLevel, int? AntralFollicleCount, string? MenstrualHistory);

public record RecordOocyteQualityRequest(int? TotalOocytes, int? MatureOocytes, int? ImmatureOocytes, int? DegeneratedOocytes, string? Notes);
public record VitrifyOocytesRequest(int Count, DateTime FreezeDate, Guid? CryoLocationId);
