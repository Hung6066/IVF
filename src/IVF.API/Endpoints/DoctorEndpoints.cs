using IVF.Application.Features.Users.Queries;
using MediatR;

namespace IVF.API.Endpoints;

public static class DoctorEndpoints
{
    public static void MapDoctorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/doctors").WithTags("Doctors").RequireAuthorization();

        group.MapGet("/", async (IMediator m, string? q, int page = 1, int pageSize = 20) =>
        {
            var result = await m.Send(new SearchDoctorsQuery(q, page, pageSize));
            return Results.Ok(result);
        });
    }
}
