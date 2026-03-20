using IVF.Application.Features.EggDonorRecipients.Commands;
using IVF.Application.Features.EggDonorRecipients.Queries;
using MediatR;

namespace IVF.API.Endpoints;

public static class EggDonorRecipientEndpoints
{
    public static void MapEggDonorRecipientEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/egg-donor-recipients").WithTags("EggDonorRecipients").RequireAuthorization();

        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetEggDonorRecipientByIdQuery(id));
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapGet("/donor/{eggDonorId:guid}", async (Guid eggDonorId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetRecipientsByDonorQuery(eggDonorId));
            return Results.Ok(result);
        });

        group.MapGet("/couple/{coupleId:guid}", async (Guid coupleId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetMatchesByRecipientCoupleQuery(coupleId));
            return Results.Ok(result);
        });

        group.MapPost("/", async (MatchDonorWithRecipientCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            if (!result.IsSuccess) return Results.BadRequest(result.Error);
            return Results.Created($"/api/egg-donor-recipients/{result.Value!.Id}", result.Value);
        });

        group.MapPut("/{id:guid}/link-cycle", async (Guid id, LinkCycleRequest request, IMediator mediator) =>
        {
            var result = await mediator.Send(new LinkMatchToCycleCommand(id, request.CycleId));
            if (!result.IsSuccess) return Results.BadRequest(result.Error);
            return Results.Ok(result.Value);
        });

        group.MapPut("/{id:guid}/complete", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new CompleteMatchCommand(id));
            if (!result.IsSuccess) return Results.BadRequest(result.Error);
            return Results.Ok(result.Value);
        });

        group.MapPut("/{id:guid}/cancel", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new CancelMatchCommand(id));
            if (!result.IsSuccess) return Results.BadRequest(result.Error);
            return Results.Ok(result.Value);
        });
    }

    private sealed record LinkCycleRequest(Guid CycleId);
}
