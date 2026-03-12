using IVF.Application.Features.DnsManagement.Commands;
using IVF.Application.Features.DnsManagement.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IVF.API.Endpoints;

public static class DnsManagementEndpoints
{
    public static void MapDnsManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/dns-records")
            .WithTags("DNS Management")
            .RequireAuthorization();

        // GET /api/admin/dns-records — List all DNS records for tenant
        group.MapGet("/", GetDnsRecords)
            .WithName("GetDnsRecords")
            .WithOpenApi()
            .Produces<List<object>>(StatusCodes.Status200OK);

        // POST /api/admin/dns-records — Create new DNS record
        group.MapPost("/", CreateDnsRecord)
            .WithName("CreateDnsRecord")
            .WithOpenApi()
            .Accepts<CreateDnsRecordRequest>("application/json")
            .Produces<object>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        // DELETE /api/admin/dns-records/{id} — Delete DNS record
        group.MapDelete("/{id}", DeleteDnsRecord)
            .WithName("DeleteDnsRecord")
            .WithOpenApi()
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);
    }

    // Handler: Get DNS Records
    private static async Task<IResult> GetDnsRecords(IMediator mediator)
    {
        var records = await mediator.Send(new GetTenantDnsRecordsQuery());
        return Results.Ok(records);
    }

    // Handler: Create DNS Record
    private static async Task<IResult> CreateDnsRecord(
        [FromBody] CreateDnsRecordRequest request,
        IMediator mediator)
    {
        var command = new CreateDnsRecordCommand(
            request.RecordType,
            request.Name,
            request.Content,
            request.TtlSeconds);

        var result = await mediator.Send(command);

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return Results.Created($"/api/admin/dns-records/{result.Value?.Id}", result.Value);
    }

    // Handler: Delete DNS Record
    private static async Task<IResult> DeleteDnsRecord(
        Guid id,
        IMediator mediator)
    {
        var result = await mediator.Send(new DeleteDnsRecordCommand(id));

        if (!result.IsSuccess)
            return Results.BadRequest(new { error = result.Error });

        return Results.NoContent();
    }
}

// ─── Request DTOs ───
public record CreateDnsRecordRequest(
    IVF.Domain.Enums.DnsRecordType RecordType,
    string Name,
    string Content,
    int TtlSeconds);
