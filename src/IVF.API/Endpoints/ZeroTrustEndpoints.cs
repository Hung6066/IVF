using MediatR;
using IVF.Application.Features.ZeroTrust.Commands;
using IVF.Application.Features.ZeroTrust.Queries;

namespace IVF.API.Endpoints;

public static class ZeroTrustEndpoints
{
    public static void MapZeroTrustEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/zerotrust").WithTags("ZeroTrust").RequireAuthorization("AdminOnly");

        group.MapGet("/policies", async (IMediator m) =>
            Results.Ok(await m.Send(new GetAllZTPoliciesQuery())));

        group.MapPut("/policies", async (UpdateZTPolicyCommand cmd, IMediator m) =>
            Results.Ok(await m.Send(cmd)));

        group.MapPost("/check", async (CheckZTAccessQuery query, IMediator m) =>
            Results.Ok(await m.Send(query)));
    }
}
