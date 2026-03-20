using IVF.Application.Features.CycleFees.Commands;
using IVF.Application.Features.CycleFees.Queries;
using MediatR;

namespace IVF.API.Endpoints;

public static class CycleFeeEndpoints
{
    public static void MapCycleFeeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cycle-fees").WithTags("CycleFees").RequireAuthorization();

        group.MapGet("/cycle/{cycleId:guid}", async (Guid cycleId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetFeesByCycleQuery(cycleId));
            return Results.Ok(result);
        });

        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetFeesByCycleQuery(Guid.Empty));
            return Results.Ok(result);
        });

        group.MapGet("/cycle/{cycleId:guid}/check", async (Guid cycleId, string feeType, IMediator mediator) =>
        {
            var result = await mediator.Send(new CheckCycleFeeExistsQuery(cycleId, feeType));
            return Results.Ok(new { Exists = result });
        });

        group.MapPost("/", async (CreateCycleFeeCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            if (!result.IsSuccess) return Results.BadRequest(result.Error);
            return Results.Created($"/api/cycle-fees/{result.Value!.Id}", result.Value);
        });

        group.MapPut("/{id:guid}/waive", async (Guid id, WaiveCycleFeeRequest request, IMediator mediator) =>
        {
            var result = await mediator.Send(new WaiveCycleFeeCommand(id, request.WaivedByUserId, request.Reason));
            if (!result.IsSuccess) return Results.BadRequest(result.Error);
            return Results.Ok(result.Value);
        });

        group.MapPut("/{id:guid}/refund", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new RefundCycleFeeCommand(id));
            if (!result.IsSuccess) return Results.BadRequest(result.Error);
            return Results.Ok(result.Value);
        });
    }

    private sealed record WaiveCycleFeeRequest(string Reason, Guid WaivedByUserId);
    private sealed record RefundCycleFeeRequest(decimal RefundAmount, string Reason);
}
