using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

namespace IVF.API.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        // Liveness probe — simple check that the process is running
        app.MapGet("/health/live", () => Results.Ok(new
        {
            status = "Healthy",
            timestamp = DateTime.UtcNow
        }))
        .WithTags("Health")
        .AllowAnonymous()
        .WithName("Liveness")
        .Produces(200);

        // Readiness probe — check if app is ready to serve traffic (DB, Redis, etc.)
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = _ => true,
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                var response = new
                {
                    status = report.Status.ToString(),
                    timestamp = DateTime.UtcNow,
                    checks = report.Entries.Select(e => new
                    {
                        name = e.Key,
                        status = e.Value.Status.ToString(),
                        duration = e.Value.Duration.TotalMilliseconds,
                        description = e.Value.Description,
                        exception = e.Value.Exception?.Message
                    })
                };
                await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                }));
            }
        })
        .WithTags("Health")
        .AllowAnonymous()
        .WithName("Readiness");

        // Startup probe — used during initial deployment to detect if app started successfully
        app.MapGet("/health/startup", () => Results.Ok(new
        {
            status = "Started",
            timestamp = DateTime.UtcNow
        }))
        .WithTags("Health")
        .AllowAnonymous()
        .WithName("Startup")
        .Produces(200);
    }
}
