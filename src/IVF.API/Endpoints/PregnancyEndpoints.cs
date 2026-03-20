using IVF.Application.Features.Pregnancy.Commands;
using IVF.Application.Features.Pregnancy.Queries;
using MediatR;

namespace IVF.API.Endpoints;

public static class PregnancyEndpoints
{
    public static void MapPregnancyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/pregnancy").WithTags("Pregnancy").RequireAuthorization();

        // Get pregnancy data for a cycle
        group.MapGet("/cycle/{cycleId:guid}", async (Guid cycleId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetPregnancyByCycleQuery(cycleId));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        });

        // Get Beta HCG results
        group.MapGet("/cycle/{cycleId:guid}/beta-hcg", async (Guid cycleId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetBetaHcgResultsQuery(cycleId));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        });

        // Get follow-up plan
        group.MapGet("/cycle/{cycleId:guid}/follow-up", async (Guid cycleId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetPregnancyFollowUpQuery(cycleId));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        });

        // Record Beta HCG
        group.MapPost("/cycle/{cycleId:guid}/beta-hcg", async (Guid cycleId, RecordBetaHcgRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new RecordBetaHcgCommand(cycleId, req.BetaHcg, req.TestDate, req.Notes));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        // Notify result
        group.MapPost("/cycle/{cycleId:guid}/notify", async (Guid cycleId, NotifyResultRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new NotifyBetaHcgResultCommand(cycleId, req.Channel, req.Message));
            return result.IsSuccess ? Results.Ok(new { message = result.Value }) : Results.BadRequest(result.Error);
        });

        // Record 7-week prenatal exam
        group.MapPost("/cycle/{cycleId:guid}/prenatal-exam", async (Guid cycleId, RecordPrenatalExamRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new RecordPrenatalExamCommand(
                cycleId, req.ExamDate, req.GestationalSacs, req.FetalHeartbeats,
                req.DueDate, req.UltrasoundFindings, req.Notes));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        // Discharge (close IVF cycle)
        group.MapPost("/cycle/{cycleId:guid}/discharge", async (Guid cycleId, DischargeRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new DischargeCycleCommand(cycleId, req.OutcomeNote, req.DischargeDate));
            return result.IsSuccess
                ? Results.Ok(new { message = result.Value })
                : Results.BadRequest(result.Error);
        });
    }

    public record RecordBetaHcgRequest(decimal BetaHcg, DateTime TestDate, string? Notes);
    public record NotifyResultRequest(string Channel, string? Message);
    public record RecordPrenatalExamRequest(DateTime ExamDate, int? GestationalSacs, int? FetalHeartbeats,
        DateTime? DueDate, string? UltrasoundFindings, string? Notes);
    public record DischargeRequest(string OutcomeNote, DateTime DischargeDate);
}
