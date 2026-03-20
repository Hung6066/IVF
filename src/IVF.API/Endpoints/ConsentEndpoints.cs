using IVF.Application.Features.Consent.Commands;
using IVF.Application.Features.Consent.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IVF.API.Endpoints;

public static class ConsentEndpoints
{
    public static void MapConsentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/consent-forms").WithTags("Consents").RequireAuthorization();

        // Queries
        group.MapGet("/{id:guid}", async (Guid id, IMediator m) =>
        {
            var result = await m.Send(new GetConsentByIdQuery(id));
            return result != null ? Results.Ok(result) : Results.NotFound();
        });

        group.MapGet("/patient/{patientId:guid}", async (Guid patientId, IMediator m) =>
            Results.Ok(await m.Send(new GetConsentsByPatientQuery(patientId))));

        group.MapGet("/cycle/{cycleId:guid}", async (Guid cycleId, IMediator m) =>
            Results.Ok(await m.Send(new GetConsentsByCycleQuery(cycleId))));

        group.MapGet("/patient/{patientId:guid}/pending", async (Guid patientId, IMediator m) =>
            Results.Ok(await m.Send(new GetPendingConsentsQuery(patientId))));

        group.MapGet("/", async (IMediator m, string? q, string? status, string? type, int page = 1, int pageSize = 20) =>
        {
            var r = await m.Send(new SearchConsentsQuery(q, status, type, page, pageSize));
            return Results.Ok(new { r.Items, r.Total });
        });

        group.MapGet("/check", async (IMediator m, Guid patientId, string consentType, Guid? cycleId) =>
            Results.Ok(new { IsValid = await m.Send(new CheckValidConsentQuery(patientId, consentType, cycleId)) }));

        // Commands
        group.MapPost("/", async ([FromBody] CreateConsentFormCommand cmd, IMediator m) =>
        {
            var r = await m.Send(cmd);
            return r.IsSuccess ? Results.Created($"/api/consent-forms/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPut("/{id:guid}/sign", async (Guid id, [FromBody] SignConsentRequest req, IMediator m) =>
        {
            var r = await m.Send(new SignConsentCommand(id, req.PatientId, req.PatientSignature, req.WitnessUserId, req.WitnessSignature, req.DoctorUserId, req.DoctorSignature));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPut("/{id:guid}/revoke", async (Guid id, [FromBody] RevokeConsentRequest req, IMediator m) =>
        {
            var r = await m.Send(new RevokeConsentCommand(id, req.Reason));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPut("/{id:guid}/scan", async (Guid id, [FromBody] UploadScanRequest req, IMediator m) =>
        {
            var r = await m.Send(new UploadConsentScanCommand(id, req.DocumentUrl));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        });
    }
}

// Request DTOs
public record SignConsentRequest(Guid PatientId, string? PatientSignature, Guid? WitnessUserId, string? WitnessSignature, Guid? DoctorUserId, string? DoctorSignature);
public record RevokeConsentRequest(string Reason);
public record UploadScanRequest(string DocumentUrl);
