using IVF.API.Contracts;
using IVF.Application.Features.Couples.Commands;
using IVF.Application.Features.Couples.Queries;
using MediatR;

namespace IVF.API.Endpoints;

public static class CoupleEndpoints
{
    public static void MapCoupleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/couples").WithTags("Couples").RequireAuthorization();

        group.MapGet("/", async (IMediator m) =>
            Results.Ok(await m.Send(new GetAllCouplesQuery())));

        group.MapGet("/{id:guid}", async (Guid id, IMediator m) =>
        {
            var r = await m.Send(new GetCoupleByIdQuery(id));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapPost("/", async (CreateCoupleCommand cmd, IMediator m) =>
        {
            var r = await m.Send(cmd);
            return r.IsSuccess ? Results.Created($"/api/couples/{r.Value!.Id}", r.Value) : Results.BadRequest(r.Error);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateCoupleRequest req, IMediator m) =>
        {
            var r = await m.Send(new UpdateCoupleCommand(id, req.MarriageDate, req.InfertilityYears));
            return r.IsSuccess ? Results.Ok(r.Value) : Results.NotFound(r.Error);
        });

        group.MapPost("/{id:guid}/donor", async (Guid id, SetDonorRequest req, IMediator m) =>
        {
            var r = await m.Send(new SetSpermDonorCommand(id, req.DonorId));
            return r.IsSuccess ? Results.Ok() : Results.BadRequest(r.Error);
        });
    }
}
