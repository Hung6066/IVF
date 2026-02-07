using IVF.API.Contracts;
using IVF.Application.Features.Patients.Commands;
using IVF.Application.Features.Patients.Queries;
using MediatR;

namespace IVF.API.Endpoints;

public static class PatientEndpoints
{
    public static void MapPatientEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/patients").WithTags("Patients").RequireAuthorization();

        group.MapGet("/", async (IMediator m, string? q, string? gender, int page = 1, int pageSize = 20) =>
            Results.Ok(await m.Send(new SearchPatientsQuery(q, gender, page, pageSize))));

        group.MapGet("/{id:guid}", async (Guid id, IMediator m) =>
        {
            var r = await m.Send(new GetPatientByIdQuery(id));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapPost("/", async (CreatePatientCommand cmd, IMediator m) =>
        {
            var r = await m.Send(cmd);
            return r.IsSuccess ? Results.Created($"/api/patients/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdatePatientRequest req, IMediator m) =>
        {
            var r = await m.Send(new UpdatePatientCommand(id, req.FullName, req.Phone, req.Address));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IMediator m) =>
        {
            var r = await m.Send(new DeletePatientCommand(id));
            return r.IsSuccess ? Results.NoContent() : Results.NotFound(r.Error);
        });
    }
}
