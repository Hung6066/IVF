using IVF.Application.Features.Stimulation.Commands;
using IVF.Application.Features.Stimulation.Queries;
using MediatR;

namespace IVF.API.Endpoints;

public static class StimulationEndpoints
{
    public static void MapStimulationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/stimulation").WithTags("Stimulation").RequireAuthorization();

        // Get tracker for a cycle
        group.MapGet("/cycle/{cycleId:guid}", async (Guid cycleId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetStimulationTrackerQuery(cycleId));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        });

        // Get follicle chart data
        group.MapGet("/cycle/{cycleId:guid}/chart", async (Guid cycleId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetFollicleChartQuery(cycleId));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        });

        // Get medication schedule
        group.MapGet("/cycle/{cycleId:guid}/medications", async (Guid cycleId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetMedicationScheduleQuery(cycleId));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        });

        // Record follicle scan
        group.MapPost("/cycle/{cycleId:guid}/scan", async (Guid cycleId, RecordFollicleScanRequest req, IMediator mediator) =>
        {
            var cmd = new RecordFollicleScanCommand(
                cycleId, req.ScanDate, req.CycleDay,
                req.Size12Follicle, req.Size14Follicle, req.TotalFollicles,
                req.EndometriumThickness, req.EndometriumPattern,
                req.E2, req.Lh, req.P4, req.Notes, req.ScanType ?? "Stimulation");
            var result = await mediator.Send(cmd);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        // Record trigger shot
        group.MapPost("/cycle/{cycleId:guid}/trigger", async (Guid cycleId, RecordTriggerShotRequest req, IMediator mediator) =>
        {
            var cmd = new RecordTriggerShotCommand(
                cycleId, req.TriggerDrug, req.TriggerDate, req.TriggerTime,
                req.TriggerDrug2, req.TriggerDate2, req.TriggerTime2,
                req.LhLab, req.E2Lab, req.P4Lab);
            var result = await mediator.Send(cmd);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        // Evaluate follicle readiness
        group.MapPost("/cycle/{cycleId:guid}/evaluate", async (Guid cycleId, EvaluateReadinessRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new EvaluateFollicleReadinessCommand(cycleId, req.Decision, req.Reason));
            return result.IsSuccess ? Results.Ok(new { decision = result.Value }) : Results.BadRequest(result.Error);
        });
    }

    public record RecordFollicleScanRequest(
        DateTime ScanDate, int CycleDay,
        int? Size12Follicle, int? Size14Follicle, int? TotalFollicles,
        decimal? EndometriumThickness, string? EndometriumPattern,
        decimal? E2, decimal? Lh, decimal? P4, string? Notes, string? ScanType);

    public record RecordTriggerShotRequest(
        string TriggerDrug, DateTime TriggerDate, TimeSpan TriggerTime,
        string? TriggerDrug2, DateTime? TriggerDate2, TimeSpan? TriggerTime2,
        decimal? LhLab, decimal? E2Lab, decimal? P4Lab);

    public record EvaluateReadinessRequest(string Decision, string? Reason);
}
