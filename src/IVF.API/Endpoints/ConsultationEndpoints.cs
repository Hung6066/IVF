using IVF.Application.Features.Consultations.Commands;
using IVF.Application.Features.Consultations.Queries;
using IVF.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IVF.API.Endpoints;

public static class ConsultationEndpoints
{
    public static void MapConsultationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/consultations").WithTags("Consultations").RequireAuthorization();

        // Queries
        group.MapGet("/{id:guid}", async (Guid id, IMediator m) =>
        {
            var result = await m.Send(new GetConsultationByIdQuery(id));
            return result != null ? Results.Ok(result) : Results.NotFound();
        });

        group.MapGet("/patient/{patientId:guid}", async (Guid patientId, IMediator m) =>
            Results.Ok(await m.Send(new GetConsultationsByPatientQuery(patientId))));

        group.MapGet("/cycle/{cycleId:guid}", async (Guid cycleId, IMediator m) =>
            Results.Ok(await m.Send(new GetConsultationsByCycleQuery(cycleId))));

        group.MapGet("/", async (IMediator m, string? q, string? status, string? type, DateTime? from, DateTime? to, int page = 1, int pageSize = 20) =>
        {
            var r = await m.Send(new SearchConsultationsQuery(q, status, type, from, to, page, pageSize));
            return Results.Ok(new { Items = r.Items, Total = r.Total });
        });

        // Commands
        group.MapPost("/", async ([FromBody] CreateConsultationCommand cmd, IMediator m) =>
        {
            var r = await m.Send(cmd);
            return r.IsSuccess ? Results.Created($"/api/consultations/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPut("/{id:guid}/start", async (Guid id, IMediator m) =>
        {
            var r = await m.Send(new StartConsultationCommand(id));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPut("/{id:guid}/clinical-data", async (Guid id, [FromBody] RecordClinicalDataRequest req, IMediator m) =>
        {
            var r = await m.Send(new RecordClinicalDataCommand(id, req.ChiefComplaint, req.MedicalHistory, req.PastHistory,
                req.SurgicalHistory, req.FamilyHistory, req.ObstetricHistory, req.MenstrualHistory, req.PhysicalExamination));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPut("/{id:guid}/diagnosis", async (Guid id, [FromBody] RecordDiagnosisRequest req, IMediator m) =>
        {
            var r = await m.Send(new RecordDiagnosisCommand(id, req.Diagnosis, req.TreatmentPlan, req.RecommendedMethod));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPut("/{id:guid}/complete", async (Guid id, IMediator m) =>
        {
            var r = await m.Send(new CompleteConsultationCommand(id));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPut("/{id:guid}/cancel", async (Guid id, IMediator m) =>
        {
            var r = await m.Send(new CancelConsultationCommand(id));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        });
    }
}

// Request DTOs
public record RecordClinicalDataRequest(
    string? ChiefComplaint, string? MedicalHistory, string? PastHistory,
    string? SurgicalHistory, string? FamilyHistory, string? ObstetricHistory,
    string? MenstrualHistory, string? PhysicalExamination);

public record RecordDiagnosisRequest(string? Diagnosis, string? TreatmentPlan, TreatmentMethod? RecommendedMethod);
