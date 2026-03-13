using IVF.API.Services;

namespace IVF.API.Endpoints;

public static class WafEndpoints
{
    public static void MapWafEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/waf")
            .WithTags("WAF Management")
            .RequireAuthorization();

        group.MapGet("/status", GetWafStatus)
            .WithName("GetWafStatus")
            .Produces<CloudflareWafService.WafStatus>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/events", GetWafEvents)
            .WithName("GetWafEvents")
            .Produces<List<CloudflareWafService.WafEvent>>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> GetWafStatus(CloudflareWafService wafService, CancellationToken ct)
    {
        var status = await wafService.GetStatusAsync(ct);
        if (!status.Configured)
            return Results.Json(
                new { error = "Cloudflare WAF not configured. Set Cloudflare:ApiToken and Cloudflare:ZoneId." },
                statusCode: StatusCodes.Status503ServiceUnavailable);

        return Results.Ok(status);
    }

    private static async Task<IResult> GetWafEvents(
        CloudflareWafService wafService,
        int? limit,
        CancellationToken ct)
    {
        var events = await wafService.GetRecentEventsAsync(limit ?? 50, ct);
        return Results.Ok(events);
    }
}
