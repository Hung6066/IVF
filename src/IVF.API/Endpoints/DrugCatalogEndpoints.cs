using IVF.Application.Features.DrugCatalog.Commands;
using IVF.Application.Features.DrugCatalog.Queries;
using IVF.Domain.Entities;
using MediatR;

namespace IVF.API.Endpoints;

public static class DrugCatalogEndpoints
{
    public static void MapDrugCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/drug-catalog").WithTags("DrugCatalog").RequireAuthorization();

        group.MapGet("/", async (string? q, DrugCategory? category, bool? isActive, int page = 1, int pageSize = 20, IMediator mediator = null!) =>
        {
            var result = await mediator.Send(new SearchDrugsQuery(q, category, isActive, page, pageSize));
            return Results.Ok(result);
        });

        group.MapGet("/active", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new GetActiveDrugsQuery());
            return Results.Ok(result);
        });

        group.MapGet("/category/{category}", async (DrugCategory category, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetDrugsByCategoryQuery(category));
            return Results.Ok(result);
        });

        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetDrugByIdQuery(id));
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapPost("/", async (CreateDrugCatalogCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            if (!result.IsSuccess) return Results.BadRequest(result.Error);
            return Results.Created($"/api/drug-catalog/{result.Value!.Id}", result.Value);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateDrugCatalogRequest request, IMediator mediator) =>
        {
            var result = await mediator.Send(new UpdateDrugCatalogCommand(id, request.Name, request.GenericName,
                request.Category, request.Unit, request.ActiveIngredient, request.DefaultDosage, request.Notes));
            if (!result.IsSuccess) return Results.BadRequest(result.Error);
            return Results.Ok(result.Value);
        });

        group.MapPut("/{id:guid}/toggle-active", async (Guid id, ToggleActiveRequest request, IMediator mediator) =>
        {
            var result = await mediator.Send(new ToggleDrugActiveCommand(id, request.Activate));
            if (!result.IsSuccess) return Results.BadRequest(result.Error);
            return Results.Ok(result.Value);
        });
    }

    private sealed record UpdateDrugCatalogRequest(string Name, string GenericName, DrugCategory Category, string Unit,
        string? ActiveIngredient, string? DefaultDosage, string? Notes);

    private sealed record ToggleActiveRequest(bool Activate);
}
