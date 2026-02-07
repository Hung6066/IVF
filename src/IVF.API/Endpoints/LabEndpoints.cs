using IVF.API.Contracts;
using IVF.Application.Features.Lab.Commands;
using IVF.Application.Features.Lab.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IVF.API.Endpoints;

public static class LabEndpoints
{
    public static void MapLabEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lab")
            .WithTags("Lab")
            .RequireAuthorization();

        // Stats
        group.MapGet("/stats", async (ISender sender) =>
        {
            var result = await sender.Send(new GetLabStatsQuery());
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        // Schedule
        group.MapGet("/schedule", async ([AsParameters] GetLabScheduleQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        group.MapPost("/schedule/{id}/toggle", async (string id, ISender sender) =>
        {
            var result = await sender.Send(new ToggleScheduleStatusCommand(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        group.MapPost("/schedule", async ([FromBody] CreateLabScheduleCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        // Cryo Locations
        group.MapGet("/cryo-locations", async (ISender sender) =>
        {
            var result = await sender.Send(new GetCryoLocationsQuery());
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        group.MapPost("/cryo-locations", async ([FromBody] CreateCryoLocationCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });
        
        group.MapDelete("/cryo-locations/{tank}", async (string tank, ISender sender) =>
        {
            var result = await sender.Send(new DeleteCryoLocationCommand(tank));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        group.MapPut("/cryo-locations/{tank}", async (string tank, [FromBody] UpdateCryoTankRequest req, ISender sender) =>
        {
            var result = await sender.Send(new UpdateCryoTankCommand(tank, req.Used, req.SpecimenType));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        // Embryo Report
        group.MapGet("/embryo-report", async ([AsParameters] GetEmbryoReportQuery query, ISender sender) =>
        {
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });
    }
}
