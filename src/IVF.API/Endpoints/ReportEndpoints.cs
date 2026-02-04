using IVF.Application.Features.Reports.Queries;
using MediatR;

namespace IVF.API.Endpoints;

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports").WithTags("Reports").RequireAuthorization();

        group.MapGet("/dashboard", async (IMediator m) =>
            Results.Ok(await m.Send(new GetDashboardStatsQuery())));

        group.MapGet("/cycles/success-rates", async (IMediator m, int? year) =>
            Results.Ok(await m.Send(new GetCycleSuccessRatesQuery(year))));

        group.MapGet("/cycles/methods", async (IMediator m, int? year) =>
            Results.Ok(await m.Send(new GetTreatmentMethodDistributionQuery(year))));

        group.MapGet("/revenue/monthly", async (IMediator m, int year) =>
            Results.Ok(await m.Send(new GetMonthlyRevenueQuery(year))));

        group.MapGet("/queue/stats", async (IMediator m, DateTime? date) =>
            Results.Ok(await m.Send(new GetQueueStatisticsQuery(date ?? DateTime.UtcNow))));
    }
}
