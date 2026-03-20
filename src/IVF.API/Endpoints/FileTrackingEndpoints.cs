using IVF.Application.Features.FileTracking.Commands;
using IVF.Application.Features.FileTracking.Queries;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.API.Endpoints;

public static class FileTrackingEndpoints
{
    public static void MapFileTrackingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/file-tracking").WithTags("FileTracking").RequireAuthorization();

        group.MapGet("/", async (string? q, FileStatus? status, string? location, int page = 1, int pageSize = 20, IMediator mediator = null!) =>
        {
            var result = await mediator.Send(new SearchFileTrackingQuery(q, status, location, page, pageSize));
            return Results.Ok(result);
        });

        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetFileTrackingByIdQuery(id));
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapGet("/patient/{patientId:guid}", async (Guid patientId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetFilesByPatientQuery(patientId));
            return Results.Ok(result);
        });

        group.MapGet("/location/{location}", async (string location, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetFilesByLocationQuery(location));
            return Results.Ok(result);
        });

        group.MapPost("/", async (CreateFileTrackingCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            if (!result.IsSuccess) return Results.BadRequest(result.Error);
            return Results.Created($"/api/file-tracking/{result.Value!.Id}", result.Value);
        });

        group.MapPut("/{id:guid}/transfer", async (Guid id, TransferFileRequest request, IMediator mediator) =>
        {
            var result = await mediator.Send(new TransferFileCommand(id, request.ToLocation, request.TransferredByUserId, request.Reason));
            if (!result.IsSuccess) return Results.BadRequest(result.Error);
            return Results.Ok(result.Value);
        });

        group.MapPut("/{id:guid}/received", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new MarkFileReceivedCommand(id));
            if (!result.IsSuccess) return Results.BadRequest(result.Error);
            return Results.Ok(result.Value);
        });

        group.MapPut("/{id:guid}/lost", async (Guid id, MarkLostRequest request, IMediator mediator) =>
        {
            var result = await mediator.Send(new MarkFileLostCommand(id, request.Reason));
            if (!result.IsSuccess) return Results.BadRequest(result.Error);
            return Results.Ok(result.Value);
        });
    }

    private sealed record TransferFileRequest(string ToLocation, Guid TransferredByUserId, string? Reason);
    private sealed record MarkLostRequest(string? Reason);
}
