using IVF.API.Contracts;
using IVF.Application.Features.Patients.Commands;
using IVF.Application.Features.Patients.Queries;
using IVF.Domain.Enums;
using MediatR;

namespace IVF.API.Endpoints;

public static class PatientEndpoints
{
    public static void MapPatientEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/patients").WithTags("Patients").RequireAuthorization();

        // ==================== CORE CRUD ====================
        group.MapGet("/", async (IMediator m, string? q, string? gender, int page = 1, int pageSize = 20) =>
            Results.Ok(await m.Send(new SearchPatientsQuery(q, gender, page, pageSize))));

        group.MapGet("/{id:guid}", async (Guid id, IMediator m) =>
        {
            var r = await m.Send(new GetPatientByIdQuery(id));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapPost("/", async (CreatePatientCommand cmd, IMediator m) =>
        {
            var r = await m.Send(cmd);
            return r.IsSuccess ? Results.Created($"/api/patients/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdatePatientRequest req, IMediator m) =>
        {
            var r = await m.Send(new UpdatePatientCommand(id, req.FullName, req.Phone, req.Address));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IMediator m) =>
        {
            var r = await m.Send(new DeletePatientCommand(id));
            return r.IsSuccess ? Results.NoContent() : Results.NotFound(r.Error);
        });

        // ==================== ADVANCED SEARCH ====================
        group.MapGet("/search/advanced", async (IMediator m,
            string? q, string? gender, PatientType? patientType, PatientStatus? status,
            PatientPriority? priority, RiskLevel? riskLevel, string? bloodType,
            DateTime? dobFrom, DateTime? dobTo, DateTime? createdFrom, DateTime? createdTo,
            string? sortBy, bool sortDesc = true, int page = 1, int pageSize = 20) =>
        {
            var result = await m.Send(new AdvancedSearchPatientsQuery(
                q, gender, patientType, status, priority, riskLevel, bloodType,
                dobFrom, dobTo, createdFrom, createdTo, sortBy, sortDesc, page, pageSize));
            return Results.Ok(result);
        });

        // ==================== DEMOGRAPHICS & EMERGENCY ====================
        group.MapPut("/{id:guid}/demographics", async (Guid id, UpdatePatientDemographicsCommand cmd, IMediator m) =>
        {
            if (cmd.Id != id) return Results.BadRequest("ID mismatch");
            var r = await m.Send(cmd);
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapPut("/{id:guid}/emergency-contact", async (Guid id, UpdateEmergencyContactRequest req, IMediator m) =>
        {
            var r = await m.Send(new UpdateEmergencyContactCommand(id, req.Name, req.Phone, req.Relation));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapPut("/{id:guid}/medical-notes", async (Guid id, UpdateMedicalNotesRequest req, IMediator m) =>
        {
            var r = await m.Send(new UpdatePatientMedicalNotesCommand(id, req.MedicalNotes));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        // ==================== CONSENT & COMPLIANCE ====================
        group.MapPut("/{id:guid}/consent", async (Guid id, UpdateConsentRequest req, IMediator m) =>
        {
            var r = await m.Send(new UpdatePatientConsentCommand(id, req.ConsentDataProcessing, req.ConsentResearch, req.ConsentMarketing));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapPost("/{id:guid}/anonymize", async (Guid id, IMediator m) =>
        {
            var r = await m.Send(new AnonymizePatientCommand(id));
            return r.IsSuccess ? Results.Ok() : Results.NotFound(r.Error);
        });

        // ==================== RISK & STATUS ====================
        group.MapPut("/{id:guid}/risk", async (Guid id, SetRiskRequest req, IMediator m) =>
        {
            var r = await m.Send(new SetPatientRiskCommand(id, req.RiskLevel, req.RiskNotes));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapPut("/{id:guid}/status", async (Guid id, ChangeStatusRequest req, IMediator m) =>
        {
            var r = await m.Send(new ChangePatientStatusCommand(id, req.Status));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapPost("/{id:guid}/record-visit", async (Guid id, IMediator m) =>
        {
            var r = await m.Send(new RecordPatientVisitCommand(id));
            return r.IsSuccess ? Results.Ok() : Results.NotFound(r.Error);
        });

        // ==================== ANALYTICS & REPORTING ====================
        group.MapGet("/analytics", async (IMediator m) =>
        {
            var r = await m.Send(new GetPatientAnalyticsQuery());
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        });

        // ==================== AUDIT TRAIL ====================
        group.MapGet("/{id:guid}/audit-trail", async (Guid id, IMediator m, int page = 1, int pageSize = 50) =>
        {
            var r = await m.Send(new GetPatientAuditTrailQuery(id, page, pageSize));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        // ==================== FOLLOW-UP & DATA RETENTION ====================
        group.MapGet("/follow-up", async (IMediator m, int days = 90) =>
        {
            var r = await m.Send(new GetPatientsFollowUpQuery(days));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        });

        group.MapGet("/data-retention/expired", async (IMediator m) =>
        {
            var r = await m.Send(new GetExpiredDataRetentionQuery());
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        });
    }
}
