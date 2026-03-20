using IVF.Application.Features.PrescriptionTemplates.Commands;
using IVF.Application.Features.PrescriptionTemplates.Queries;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.API.Endpoints;

public static class PrescriptionTemplateEndpoints
{
    public static void MapPrescriptionTemplateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/prescription-templates").WithTags("PrescriptionTemplates").RequireAuthorization();

        group.MapGet("/", async (string? q, PrescriptionCycleType? cycleType, bool? isActive, int page = 1, int pageSize = 20, IMediator mediator = null!) =>
        {
            var result = await mediator.Send(new SearchPrescriptionTemplatesQuery(q, cycleType, isActive, page, pageSize));
            return Results.Ok(result);
        });

        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetPrescriptionTemplateByIdQuery(id));
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapGet("/doctor/{doctorId:guid}", async (Guid doctorId, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetTemplatesByDoctorQuery(doctorId));
            return Results.Ok(result);
        });

        group.MapGet("/cycle-type/{cycleType}", async (PrescriptionCycleType cycleType, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetTemplatesByCycleTypeQuery(cycleType));
            return Results.Ok(result);
        });

        group.MapPost("/", async (CreatePrescriptionTemplateCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            if (!result.IsSuccess) return Results.BadRequest(result.Error);
            return Results.Created($"/api/prescription-templates/{result.Value!.Id}", result.Value);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateTemplateRequest request, IMediator mediator) =>
        {
            var result = await mediator.Send(new UpdatePrescriptionTemplateCommand(id, request.Name, request.CycleType, request.Description, request.Items));
            if (!result.IsSuccess) return Results.BadRequest(result.Error);
            return Results.Ok(result.Value);
        });

        group.MapPut("/{id:guid}/toggle-active", async (Guid id, ToggleTemplateRequest request, IMediator mediator) =>
        {
            var result = await mediator.Send(new ToggleTemplateActiveCommand(id, request.Activate));
            if (!result.IsSuccess) return Results.BadRequest(result.Error);
            return Results.Ok(result.Value);
        });
    }

    private sealed record UpdateTemplateRequest(string Name, PrescriptionCycleType CycleType, string? Description, List<TemplateItemInput> Items);
    private sealed record ToggleTemplateRequest(bool Activate);
}
