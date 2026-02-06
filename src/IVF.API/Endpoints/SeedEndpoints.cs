using IVF.Application.Features.Seed.Commands;
using MediatR;

namespace IVF.API.Endpoints;

public static class SeedEndpoints
{
    public static void MapSeedEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/seed").WithTags("Seed");

        group.MapPost("/flow", async (IMediator m) =>
        {
            await m.Send(new SeedFlowDataCommand());
            return Results.Ok("Flow data seeded successfully");
        });
    }
}
