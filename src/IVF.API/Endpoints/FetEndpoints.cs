using IVF.Application.Features.FET.Commands;
using IVF.Application.Features.FET.Queries;
using MediatR;

namespace IVF.API.Endpoints;

public static class FetEndpoints
{
    public static void MapFetEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/fet").WithTags("FET").RequireAuthorization();

        // Get by ID
        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetFetProtocolByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        });

        // Get by cycle
        group.MapGet("/cycle/{cycleId:guid}", async (Guid cycleId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetFetProtocolByCycleQuery(cycleId));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        });

        // Search
        group.MapGet("/", async (IMediator mediator, string? q, string? status, int page = 1, int pageSize = 20) =>
        {
            var result = await mediator.Send(new SearchFetProtocolsQuery(q, status, page, pageSize));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
        });

        // Create
        group.MapPost("/", async (CreateFetProtocolCommand cmd, IMediator mediator) =>
        {
            var result = await mediator.Send(cmd);
            return result.IsSuccess
                ? Results.Created($"/api/fet/{result.Value!.Id}", result.Value)
                : Results.BadRequest(result.Error);
        });

        // Update hormone therapy
        group.MapPut("/{id:guid}/hormones", async (Guid id, UpdateHormoneTherapyRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new UpdateHormoneTherapyCommand(
                id, req.EstrogenDrug, req.EstrogenDose, req.EstrogenStartDate,
                req.ProgesteroneDrug, req.ProgesteroneDose, req.ProgesteroneStartDate));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        });

        // Record endometrium check
        group.MapPut("/{id:guid}/endometrium", async (Guid id, RecordEndometriumCheckRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new RecordEndometriumCheckCommand(id, req.Thickness, req.Pattern, req.CheckDate));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        });

        // Record thawing
        group.MapPut("/{id:guid}/thawing", async (Guid id, RecordThawingRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new RecordThawingCommand(
                id, req.EmbryosToThaw, req.EmbryosSurvived, req.ThawDate, req.EmbryoGrade, req.EmbryoAge));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        });

        // Schedule transfer
        group.MapPut("/{id:guid}/schedule-transfer", async (Guid id, ScheduleTransferRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new ScheduleTransferCommand(id, req.TransferDate));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        });

        // Mark transferred
        group.MapPost("/{id:guid}/transferred", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new MarkFetTransferredCommand(id));
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error);
        });

        // Cancel
        group.MapPost("/{id:guid}/cancel", async (Guid id, CancelFetRequest? req, IMediator mediator) =>
        {
            var result = await mediator.Send(new CancelFetProtocolCommand(id, req?.Reason));
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
        });

        // ==================== ENDOMETRIUM SCANS ====================
        var scanGroup = app.MapGroup("/api/endometrium-scans").WithTags("FET").RequireAuthorization();

        scanGroup.MapGet("/cycle/{cycleId:guid}", async (Guid cycleId, IMediator mediator) =>
            Results.Ok(await mediator.Send(new GetEndometriumScansByCycleQuery(cycleId))));

        scanGroup.MapPost("/", async (CreateEndometriumScanCommand cmd, IMediator mediator) =>
        {
            var r = await mediator.Send(cmd);
            return r.IsSuccess ? Results.Created($"/api/endometrium-scans/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
        });
    }

    public record UpdateHormoneTherapyRequest(string? EstrogenDrug, string? EstrogenDose, DateTime? EstrogenStartDate,
        string? ProgesteroneDrug, string? ProgesteroneDose, DateTime? ProgesteroneStartDate);
    public record RecordEndometriumCheckRequest(decimal Thickness, string? Pattern, DateTime CheckDate);
    public record RecordThawingRequest(int EmbryosToThaw, int EmbryosSurvived, DateTime ThawDate, string? EmbryoGrade, int EmbryoAge);
    public record ScheduleTransferRequest(DateTime TransferDate);
    public record CancelFetRequest(string? Reason);
}
