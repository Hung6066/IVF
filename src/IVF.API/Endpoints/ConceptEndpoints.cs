using IVF.Application.Features.Forms.Commands;
using IVF.Application.Features.Forms.DTOs;
using IVF.Application.Features.Forms.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IVF.API.Endpoints;

public static class ConceptEndpoints
{
    public static void MapConceptEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/concepts")
            .WithTags("Concepts")
            .WithOpenApi();

        // Create concept
        group.MapPost("/", async (
            [FromBody] CreateConceptRequest request,
            IMediator mediator) =>
        {
            var command = new CreateConceptCommand(
                request.Code,
                request.Display,
                request.Description,
                request.System ?? "LOCAL",
                request.ConceptType
            );

            var concept = await mediator.Send(command);
            return Results.Created($"/api/concepts/{concept.Id}", concept);
        })
        .WithName("CreateConcept")
        .WithSummary("Create a new medical concept");

        // Update concept
        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateConceptRequest request,
            IMediator mediator) =>
        {
            var command = new UpdateConceptCommand(
                id,
                request.Display,
                request.Description
            );

            var concept = await mediator.Send(command);
            return Results.Ok(concept);
        })
        .WithName("UpdateConcept")
        .WithSummary("Update an existing concept");

        // Get concept by ID
        group.MapGet("/{id:guid}", async (
            Guid id,
            IMediator mediator) =>
        {
            var query = new GetConceptByIdQuery(id);
            var concept = await mediator.Send(query);
            return concept is not null ? Results.Ok(concept) : Results.NotFound();
        })
        .WithName("GetConceptById")
        .WithSummary("Get concept by ID with mappings");

        // Search concepts
        group.MapGet("/search", async (
            IMediator mediator,
            [FromQuery] string? q = null,
            [FromQuery] int? conceptType = null,
            [FromQuery] int pageSize = 20,
            [FromQuery] int pageNumber = 1) =>
        {
            var query = new SearchConceptsQuery(
                q ?? "",
                conceptType,
                pageSize,
                pageNumber
            );

            var result = await mediator.Send(query);
            return Results.Ok(result);
        })
        .WithName("SearchConcepts")
        .WithSummary("Full-text search concepts using PostgreSQL TsVector");

        // Get concepts by type
        group.MapGet("/by-type/{conceptType:int}", async (
            int conceptType,
            IMediator mediator,
            [FromQuery] int pageSize = 50,
            [FromQuery] int pageNumber = 1) =>
        {
            var query = new GetConceptsByTypeQuery(conceptType, pageSize, pageNumber);
            var concepts = await mediator.Send(query);
            return Results.Ok(concepts);
        })
        .WithName("GetConceptsByType")
        .WithSummary("Get all concepts of a specific type");

        // Add concept mapping (SNOMED CT, HL7, LOINC, etc.)
        group.MapPost("/{id:guid}/mappings", async (
            Guid id,
            [FromBody] AddConceptMappingRequest request,
            IMediator mediator) =>
        {
            var command = new AddConceptMappingCommand(
                id,
                request.TargetSystem,
                request.TargetCode,
                request.TargetDisplay,
                request.Relationship
            );

            var mapping = await mediator.Send(command);
            return Results.Created($"/api/concepts/{id}/mappings/{mapping.Id}", mapping);
        })
        .WithName("AddConceptMapping")
        .WithSummary("Add external terminology mapping (SNOMED CT, HL7, LOINC)");

        // Link form field to concept
        group.MapPost("/link/field", async (
            [FromBody] LinkFieldToConceptRequest request,
            IMediator mediator) =>
        {
            var command = new LinkFieldToConceptCommand(
                request.FieldId,
                request.ConceptId
            );

            var field = await mediator.Send(command);
            return Results.Ok(field);
        })
        .WithName("LinkFieldToConcept")
        .WithSummary("Link a form field to a concept");

        // Link form field option to concept
        group.MapPost("/link/option", async (
            [FromBody] LinkOptionToConceptRequest request,
            IMediator mediator) =>
        {
            var command = new LinkOptionToConceptCommand(
                request.OptionId,
                request.ConceptId
            );

            var result = await mediator.Send(command);
            return Results.Ok(result);
        })
        .WithName("LinkOptionToConcept")
        .WithSummary("Link a form field option to a concept");
        // Seed concepts (development only)
        group.MapPost("/seed", async (IServiceProvider services) =>
        {
            await IVF.Infrastructure.Seeding.ConceptSeeder.SeedConceptsAsync(services);
            return Results.Ok(new { Message = "Concepts seeded successfully" });
        })
        .WithName("SeedConcepts")
        .WithSummary("Seed medical concepts (development only)");
    }
}

// Request DTOs
public record CreateConceptRequest(
    string Code,
    string Display,
    string? Description,
    string? System,
    int ConceptType
);

public record UpdateConceptRequest(
    string Display,
    string? Description
);

public record AddConceptMappingRequest(
    string TargetSystem,
    string TargetCode,
    string TargetDisplay,
    string? Relationship
);

public record LinkFieldToConceptRequest(
    Guid FieldId,
    Guid ConceptId
);

public record LinkOptionToConceptRequest(
    Guid OptionId,
    Guid ConceptId
);
