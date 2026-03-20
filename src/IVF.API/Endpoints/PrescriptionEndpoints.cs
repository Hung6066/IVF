using IVF.Application.Features.Prescriptions.Commands;
using IVF.Application.Features.Prescriptions.Queries;
using MediatR;

namespace IVF.API.Endpoints;

public static class PrescriptionEndpoints
{
    public static void MapPrescriptionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/prescriptions").WithTags("Prescriptions").RequireAuthorization();

        // Queries
        group.MapGet("/{id:guid}", async (Guid id, IMediator m) =>
        {
            var result = await m.Send(new GetPrescriptionByIdQuery(id));
            return result != null ? Results.Ok(result) : Results.NotFound();
        });

        group.MapGet("/patient/{patientId:guid}", async (Guid patientId, IMediator m) =>
            Results.Ok(await m.Send(new GetPrescriptionsByPatientQuery(patientId))));

        group.MapGet("/cycle/{cycleId:guid}", async (Guid cycleId, IMediator m) =>
            Results.Ok(await m.Send(new GetPrescriptionsByCycleQuery(cycleId))));

        group.MapGet("/", async (IMediator m, string? q, DateTime? from, DateTime? to, string? status, int page = 1, int pageSize = 20) =>
        {
            var r = await m.Send(new SearchPrescriptionsQuery(q, from, to, status, page, pageSize));
            return Results.Ok(new { Items = r.Items, Total = r.Total });
        });

        group.MapGet("/statistics", async (IMediator m) =>
            Results.Ok(await m.Send(new GetPrescriptionStatisticsQuery())));

        // Commands
        group.MapPost("/", async (CreatePrescriptionCommand cmd, IMediator m) =>
        {
            var r = await m.Send(cmd);
            return r.IsSuccess ? Results.Created($"/api/prescriptions/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPost("/{id:guid}/items", async (Guid id, AddPrescriptionItemRequest req, IMediator m) =>
        {
            var r = await m.Send(new AddPrescriptionItemCommand(id, req.DrugName, req.Quantity, req.DrugCode, req.Dosage, req.Frequency, req.Duration));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPut("/{id:guid}/enter", async (Guid id, EnterPrescriptionRequest req, IMediator m) =>
        {
            var r = await m.Send(new EnterPrescriptionCommand(id, req.EnteredByUserId));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPut("/{id:guid}/print", async (Guid id, IMediator m) =>
        {
            var r = await m.Send(new PrintPrescriptionCommand(id));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPut("/{id:guid}/dispense", async (Guid id, DispensePrescriptionRequest req, IMediator m) =>
        {
            var r = await m.Send(new DispensePrescriptionCommand(id, req.DispensedByUserId));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPut("/{id:guid}/cancel", async (Guid id, IMediator m) =>
        {
            var r = await m.Send(new CancelPrescriptionCommand(id));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPut("/{id:guid}/notes", async (Guid id, UpdateNotesRequest req, IMediator m) =>
        {
            var r = await m.Send(new UpdatePrescriptionNotesCommand(id, req.Notes));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        });
    }
}

// Request DTOs for endpoints that need body binding separate from route params
public record AddPrescriptionItemRequest(string DrugName, int Quantity, string? DrugCode, string? Dosage, string? Frequency, string? Duration);
public record EnterPrescriptionRequest(Guid EnteredByUserId);
public record DispensePrescriptionRequest(Guid DispensedByUserId);
public record UpdateNotesRequest(string? Notes);
