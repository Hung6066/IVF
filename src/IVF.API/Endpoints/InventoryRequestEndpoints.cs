using IVF.Application.Features.InventoryRequests.Commands;
using IVF.Application.Features.InventoryRequests.Queries;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.API.Endpoints;

public static class InventoryRequestEndpoints
{
    public static void MapInventoryRequestEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory-requests").WithTags("InventoryRequests").RequireAuthorization();

        group.MapGet("/", async (string? q, InventoryRequestStatus? status, InventoryRequestType? type, int page = 1, int pageSize = 20, IMediator mediator = null!) =>
        {
            var result = await mediator.Send(new SearchInventoryRequestsQuery(q, status, type, page, pageSize));
            return Results.Ok(result);
        });

        group.MapGet("/pending", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new GetInventoryRequestsByStatusQuery(InventoryRequestStatus.Pending));
            return Results.Ok(result);
        });

        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetInventoryRequestByIdQuery(id));
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapPost("/", async (CreateInventoryRequestCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            if (!result.IsSuccess) return Results.BadRequest(result.Error);
            return Results.Created($"/api/inventory-requests/{result.Value!.Id}", result.Value);
        });

        group.MapPut("/{id:guid}/approve", async (Guid id, ApproveRequest request, IMediator mediator) =>
        {
            var result = await mediator.Send(new ApproveInventoryRequestCommand(id, request.ApprovedByUserId));
            if (!result.IsSuccess) return Results.BadRequest(result.Error);
            return Results.Ok(result.Value);
        });

        group.MapPut("/{id:guid}/reject", async (Guid id, RejectRequest request, IMediator mediator) =>
        {
            var result = await mediator.Send(new RejectInventoryRequestCommand(id, request.RejectedByUserId, request.Reason));
            if (!result.IsSuccess) return Results.BadRequest(result.Error);
            return Results.Ok(result.Value);
        });

        group.MapPut("/{id:guid}/fulfill", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new FulfillInventoryRequestCommand(id));
            if (!result.IsSuccess) return Results.BadRequest(result.Error);
            return Results.Ok(result.Value);
        });
    }

    private sealed record ApproveRequest(Guid ApprovedByUserId);
    private sealed record RejectRequest(Guid RejectedByUserId, string Reason);
}
