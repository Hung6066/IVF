using IVF.Application.Features.Procedures.Commands;
using IVF.Application.Features.Procedures.Queries;
using MediatR;

namespace IVF.API.Endpoints;

public static class ProcedureEndpoints
{
    public static void MapProcedureEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/procedures").WithTags("Procedures").RequireAuthorization();

        // Get by ID
        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetProcedureByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        });

        // Get by patient
        group.MapGet("/patient/{patientId:guid}", async (Guid patientId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetProceduresByPatientQuery(patientId));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        // Get by cycle
        group.MapGet("/cycle/{cycleId:guid}", async (Guid cycleId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetProceduresByCycleQuery(cycleId));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        // Get by date
        group.MapGet("/date/{date}", async (DateTime date, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetProceduresByDateQuery(date));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        // Search
        group.MapGet("/", async (IMediator mediator, string? q, string? procedureType, string? status, int page = 1, int pageSize = 20) =>
        {
            var result = await mediator.Send(new SearchProceduresQuery(q, procedureType, status, page, pageSize));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        // Create
        group.MapPost("/", async (CreateProcedureCommand cmd, IMediator mediator) =>
        {
            var result = await mediator.Send(cmd);
            return result.IsSuccess
                ? Results.Created($"/api/procedures/{result.Value!.Id}", result.Value)
                : Results.BadRequest(result.Error);
        });

        // Start procedure
        group.MapPut("/{id:guid}/start", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new StartProcedureCommand(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        });

        // Complete procedure
        group.MapPut("/{id:guid}/complete", async (Guid id, CompleteProcedureRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new CompleteProcedureCommand(id, req.IntraOpFindings, req.PostOpNotes, req.Complications, req.DurationMinutes));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        });

        // Cancel procedure
        group.MapPut("/{id:guid}/cancel", async (Guid id, CancelProcedureRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new CancelProcedureCommand(id, req.Reason));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        });

        // Postpone procedure
        group.MapPut("/{id:guid}/postpone", async (Guid id, PostponeProcedureRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new PostponeProcedureCommand(id, req.NewScheduledAt, req.Reason));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        });
    }
}

// Request DTOs
public record CompleteProcedureRequest(string? IntraOpFindings, string? PostOpNotes, string? Complications, int? DurationMinutes);
public record CancelProcedureRequest(string? Reason);
public record PostponeProcedureRequest(DateTime NewScheduledAt, string? Reason);
