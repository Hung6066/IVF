using IVF.Application.Common.Interfaces;
using IVF.Application.Features.Cycles.Commands;
using MediatR;

namespace IVF.API.Endpoints;

public static class CyclePhaseEndpoints
{
    public static void MapCyclePhaseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cycles").WithTags("Cycle Phase Data").RequireAuthorization("DoctorOrAdmin");

        // Treatment Indication
        group.MapGet("/{cycleId:guid}/indication", async (Guid cycleId, ICyclePhaseDataRepository phaseRepo) =>
        {
            var indication = await phaseRepo.GetIndicationByCycleIdAsync(cycleId);
            return indication != null ? Results.Ok(TreatmentIndicationDto.FromEntity(indication)) : Results.NotFound();
        });

        group.MapPut("/{cycleId:guid}/indication", async (Guid cycleId, UpdateTreatmentIndicationRequest req, IMediator m) =>
        {
            var cmd = new UpdateTreatmentIndicationCommand(
                cycleId, req.LastMenstruation, req.TreatmentType, req.Regimen, req.FreezeAll, req.Sis,
                req.WifeDiagnosis, req.WifeDiagnosis2, req.HusbandDiagnosis, req.HusbandDiagnosis2,
                req.UltrasoundDoctorId, req.IndicationDoctorId, req.FshDoctorId, req.MidwifeId,
                req.Timelapse, req.PgtA, req.PgtSr, req.PgtM,
                req.SubType, req.ScientificResearch, req.Source, req.ProcedurePlace, req.StopReason,
                req.TreatmentMonth, req.PreviousTreatmentsAtSite, req.PreviousTreatmentsOther);
            var r = await m.Send(cmd);
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        });

        // Stimulation Data
        group.MapGet("/{cycleId:guid}/stimulation", async (Guid cycleId, ICyclePhaseDataRepository phaseRepo) =>
        {
            var data = await phaseRepo.GetStimulationByCycleIdAsync(cycleId);
            return data != null ? Results.Ok(StimulationDataDto.FromEntity(data)) : Results.NotFound();
        });

        group.MapPut("/{cycleId:guid}/stimulation", async (Guid cycleId, UpdateStimulationDataRequest req, IMediator m) =>
        {
            var cmd = new UpdateStimulationDataCommand(
                cycleId, req.LastMenstruation, req.StartDate, req.StartDay,
                req.Drug1, req.Drug1Duration, req.Drug1Posology,
                req.Drug2, req.Drug2Duration, req.Drug2Posology,
                req.Drug3, req.Drug3Duration, req.Drug3Posology,
                req.Drug4, req.Drug4Duration, req.Drug4Posology,
                req.Size12Follicle, req.Size14Follicle, req.EndometriumThickness,
                req.TriggerDrug, req.TriggerDrug2, req.HcgDate, req.HcgDate2, req.HcgTime, req.HcgTime2,
                req.LhLab, req.E2Lab, req.P4Lab,
                req.ProcedureType, req.AspirationDate, req.ProcedureDate, req.AspirationNo,
                req.TechniqueWife, req.TechniqueHusband);
            var r = await m.Send(cmd);
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        });

        // Culture Data
        group.MapGet("/{cycleId:guid}/culture", async (Guid cycleId, ICyclePhaseDataRepository phaseRepo) =>
        {
            var data = await phaseRepo.GetCultureByCycleIdAsync(cycleId);
            return data != null ? Results.Ok(CultureDataDto.FromEntity(data)) : Results.NotFound();
        });

        group.MapPut("/{cycleId:guid}/culture", async (Guid cycleId, UpdateCultureDataRequest req, IMediator m) =>
        {
            var cmd = new UpdateCultureDataCommand(cycleId, req.TotalFreezedEmbryo, req.TotalThawedEmbryo, req.TotalTransferedEmbryo, req.RemainFreezedEmbryo);
            var r = await m.Send(cmd);
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        });

        // Transfer Data
        group.MapGet("/{cycleId:guid}/transfer", async (Guid cycleId, ICyclePhaseDataRepository phaseRepo) =>
        {
            var data = await phaseRepo.GetTransferByCycleIdAsync(cycleId);
            return data != null ? Results.Ok(TransferDataDto.FromEntity(data)) : Results.NotFound();
        });

        group.MapPut("/{cycleId:guid}/transfer", async (Guid cycleId, UpdateTransferDataRequest req, IMediator m) =>
        {
            var cmd = new UpdateTransferDataCommand(cycleId, req.TransferDate, req.ThawingDate, req.DayOfTransfered, req.LabNote);
            var r = await m.Send(cmd);
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        });

        // Luteal Phase Data
        group.MapGet("/{cycleId:guid}/luteal-phase", async (Guid cycleId, ICyclePhaseDataRepository phaseRepo) =>
        {
            var data = await phaseRepo.GetLutealPhaseByCycleIdAsync(cycleId);
            return data != null ? Results.Ok(LutealPhaseDataDto.FromEntity(data)) : Results.NotFound();
        });

        group.MapPut("/{cycleId:guid}/luteal-phase", async (Guid cycleId, UpdateLutealPhaseDataRequest req, IMediator m) =>
        {
            var cmd = new UpdateLutealPhaseDataCommand(cycleId, req.LutealDrug1, req.LutealDrug2, req.EndometriumDrug1, req.EndometriumDrug2);
            var r = await m.Send(cmd);
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        });

        // Pregnancy Data
        group.MapGet("/{cycleId:guid}/pregnancy", async (Guid cycleId, ICyclePhaseDataRepository phaseRepo) =>
        {
            var data = await phaseRepo.GetPregnancyByCycleIdAsync(cycleId);
            return data != null ? Results.Ok(PregnancyDataDto.FromEntity(data)) : Results.NotFound();
        });

        group.MapPut("/{cycleId:guid}/pregnancy", async (Guid cycleId, UpdatePregnancyDataRequest req, IMediator m) =>
        {
            var cmd = new UpdatePregnancyDataCommand(cycleId, req.BetaHcg, req.BetaHcgDate, req.IsPregnant, req.GestationalSacs, req.FetalHeartbeats, req.DueDate, req.Notes);
            var r = await m.Send(cmd);
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        });

        // Birth Data
        group.MapGet("/{cycleId:guid}/birth", async (Guid cycleId, ICyclePhaseDataRepository phaseRepo) =>
        {
            var data = await phaseRepo.GetBirthByCycleIdAsync(cycleId);
            return data != null ? Results.Ok(BirthDataDto.FromEntity(data)) : Results.NotFound();
        });

        group.MapPut("/{cycleId:guid}/birth", async (Guid cycleId, UpdateBirthDataRequest req, IMediator m) =>
        {
            var cmd = new UpdateBirthDataCommand(cycleId, req.DeliveryDate, req.GestationalWeeks, req.DeliveryMethod, req.LiveBirths, req.Stillbirths, req.BabyGenders, req.BirthWeights, req.Complications);
            var r = await m.Send(cmd);
            return r.IsSuccess ? Results.Ok(r.Value) : Results.BadRequest(r.Error);
        });

        // Adverse Events
        group.MapGet("/{cycleId:guid}/adverse-events", async (Guid cycleId, ICyclePhaseDataRepository phaseRepo) =>
        {
            var data = await phaseRepo.GetAdverseEventsByCycleIdAsync(cycleId);
            return Results.Ok(data.Select(AdverseEventDataDto.FromEntity).ToList());
        });

        group.MapPost("/{cycleId:guid}/adverse-events", async (Guid cycleId, CreateAdverseEventRequest req, IMediator m) =>
        {
            var cmd = new CreateAdverseEventCommand(cycleId, req.EventDate, req.EventType, req.Severity, req.Description, req.Treatment, req.Outcome);
            var r = await m.Send(cmd);
            return r.IsSuccess ? Results.Created($"/api/cycles/{cycleId}/adverse-events/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
        });
    }
}

// Request DTOs
public record UpdateTreatmentIndicationRequest(
    DateTime? LastMenstruation, string? TreatmentType, string? Regimen, bool FreezeAll, bool Sis,
    string? WifeDiagnosis, string? WifeDiagnosis2, string? HusbandDiagnosis, string? HusbandDiagnosis2,
    Guid? UltrasoundDoctorId, Guid? IndicationDoctorId, Guid? FshDoctorId, Guid? MidwifeId,
    bool Timelapse, bool PgtA, bool PgtSr, bool PgtM,
    string? SubType, string? ScientificResearch, string? Source, string? ProcedurePlace, string? StopReason,
    DateTime? TreatmentMonth, int PreviousTreatmentsAtSite, int PreviousTreatmentsOther);

public record UpdateStimulationDataRequest(
    DateTime? LastMenstruation, DateTime? StartDate, int? StartDay,
    string? Drug1, int Drug1Duration, string? Drug1Posology,
    string? Drug2, int Drug2Duration, string? Drug2Posology,
    string? Drug3, int Drug3Duration, string? Drug3Posology,
    string? Drug4, int Drug4Duration, string? Drug4Posology,
    int? Size12Follicle, int? Size14Follicle, decimal? EndometriumThickness,
    string? TriggerDrug, string? TriggerDrug2, DateTime? HcgDate, DateTime? HcgDate2, TimeSpan? HcgTime, TimeSpan? HcgTime2,
    decimal? LhLab, decimal? E2Lab, decimal? P4Lab,
    string? ProcedureType, DateTime? AspirationDate, DateTime? ProcedureDate, int? AspirationNo,
    string? TechniqueWife, string? TechniqueHusband);

public record UpdateCultureDataRequest(int TotalFreezedEmbryo, int TotalThawedEmbryo, int TotalTransferedEmbryo, int RemainFreezedEmbryo);

public record UpdateTransferDataRequest(DateTime? TransferDate, DateTime? ThawingDate, int DayOfTransfered, string? LabNote);

public record UpdateLutealPhaseDataRequest(string? LutealDrug1, string? LutealDrug2, string? EndometriumDrug1, string? EndometriumDrug2);

public record UpdatePregnancyDataRequest(decimal? BetaHcg, DateTime? BetaHcgDate, bool IsPregnant, int? GestationalSacs, int? FetalHeartbeats, DateTime? DueDate, string? Notes);

public record UpdateBirthDataRequest(DateTime? DeliveryDate, int GestationalWeeks, string? DeliveryMethod, int LiveBirths, int Stillbirths, string? BabyGenders, string? BirthWeights, string? Complications);

public record CreateAdverseEventRequest(DateTime? EventDate, string? EventType, string? Severity, string? Description, string? Treatment, string? Outcome);
