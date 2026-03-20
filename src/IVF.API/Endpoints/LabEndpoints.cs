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

        // ==================== Lab Orders ====================
        group.MapGet("/orders/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetLabOrderByIdQuery(id));
            return result != null ? Results.Ok(result) : Results.NotFound();
        });

        group.MapGet("/orders/patient/{patientId:guid}", async (Guid patientId, ISender sender) =>
            Results.Ok(await sender.Send(new GetLabOrdersByPatientQuery(patientId))));

        group.MapGet("/orders/cycle/{cycleId:guid}", async (Guid cycleId, ISender sender) =>
            Results.Ok(await sender.Send(new GetLabOrdersByCycleQuery(cycleId))));

        group.MapGet("/orders", async (ISender sender, string? q, string? status, string? orderType, DateTime? from, DateTime? to, int page = 1, int pageSize = 20) =>
        {
            var r = await sender.Send(new SearchLabOrdersQuery(q, status, orderType, from, to, page, pageSize));
            return Results.Ok(new { Items = r.Items, Total = r.Total });
        });

        group.MapGet("/orders/statistics", async (ISender sender) =>
            Results.Ok(await sender.Send(new GetLabOrderStatisticsQuery())));

        group.MapPost("/orders", async ([FromBody] CreateLabOrderCommand cmd, ISender sender) =>
        {
            var r = await sender.Send(cmd);
            return r.IsSuccess ? Results.Created($"/api/lab/orders/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPut("/orders/{id:guid}/collect-sample", async (Guid id, ISender sender) =>
        {
            var r = await sender.Send(new CollectLabSampleCommand(id));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPut("/orders/{id:guid}/results", async (Guid id, [FromBody] EnterLabResultRequest req, ISender sender) =>
        {
            var r = await sender.Send(new EnterLabResultCommand(id, req.PerformedByUserId, req.Results));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPut("/orders/{id:guid}/deliver", async (Guid id, [FromBody] DeliverLabResultRequest req, ISender sender) =>
        {
            var r = await sender.Send(new DeliverLabResultCommand(id, req.DeliveredByUserId, req.DeliveredTo));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        });
    }
}

// Request DTOs
public record EnterLabResultRequest(Guid PerformedByUserId, List<LabTestResultInput> Results);
public record DeliverLabResultRequest(Guid DeliveredByUserId, string DeliveredTo);
