using IVF.Application.Features.MedicationAdmin.Commands;
using IVF.Application.Features.MedicationAdmin.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IVF.API.Endpoints;

public static class MedicationAdminEndpoints
{
    public static void MapMedicationAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/medication-admin").WithTags("MedicationAdministration").RequireAuthorization();

        group.MapGet("/{id:guid}", async (Guid id, IMediator m) =>
        {
            var result = await m.Send(new GetMedicationAdminByIdQuery(id));
            return result != null ? Results.Ok(result) : Results.NotFound();
        });

        group.MapGet("/cycle/{cycleId:guid}", async (Guid cycleId, IMediator m) =>
            Results.Ok(await m.Send(new GetMedicationsByCycleQuery(cycleId))));

        group.MapGet("/cycle/{cycleId:guid}/trigger-shots", async (Guid cycleId, IMediator m) =>
            Results.Ok(await m.Send(new GetTriggerShotsByCycleQuery(cycleId))));

        group.MapGet("/", async (IMediator m, string? q, Guid? cycleId, DateTime? from, DateTime? to, int page = 1, int pageSize = 20) =>
        {
            var r = await m.Send(new SearchMedicationAdminsQuery(q, cycleId, from, to, page, pageSize));
            return Results.Ok(new { r.Items, r.Total });
        });

        group.MapPost("/", async ([FromBody] RecordMedicationAdminCommand cmd, IMediator m) =>
        {
            var r = await m.Send(cmd);
            return r.IsSuccess ? Results.Created($"/api/medication-admin/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPut("/{id:guid}/skip", async (Guid id, [FromBody] SkipMedicationRequest req, IMediator m) =>
        {
            var r = await m.Send(new SkipMedicationCommand(id, req.Reason));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        });
    }
}

public record SkipMedicationRequest(string? Reason);
