using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace IVF.API.Extensions;

/// <summary>
/// OpenTelemetry configuration for metrics, tracing, and Prometheus export.
/// Provides enterprise-grade observability for the IVF platform.
/// </summary>
public static class OpenTelemetryExtensions
{
    public static IServiceCollection AddIvfOpenTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        var serviceName = configuration["OpenTelemetry:ServiceName"] ?? "ivf-api";
        var serviceVersion = typeof(OpenTelemetryExtensions).Assembly.GetName().Version?.ToString() ?? "1.0.0";

        var otelBuilder = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production",
                    ["host.name"] = Environment.MachineName
                }));

        // Metrics
        otelBuilder.WithMetrics(metrics =>
        {
            metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("IVF.API") // Custom meters
                .AddPrometheusExporter();

            // OTLP exporter for Grafana/Tempo if configured
            var otlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"];
            if (!string.IsNullOrEmpty(otlpEndpoint))
            {
                metrics.AddOtlpExporter(opts => opts.Endpoint = new Uri(otlpEndpoint));
            }
        });

        // Tracing
        otelBuilder.WithTracing(tracing =>
        {
            tracing
                .AddAspNetCoreInstrumentation(opts =>
                {
                    // Filter out health check and metrics endpoints
                    opts.Filter = context =>
                        !context.Request.Path.StartsWithSegments("/health") &&
                        !context.Request.Path.StartsWithSegments("/metrics");
                })
                .AddHttpClientInstrumentation()
                .AddSource("IVF.API"); // Custom activity sources

            // OTLP exporter for distributed tracing
            var otlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"];
            if (!string.IsNullOrEmpty(otlpEndpoint))
            {
                tracing.AddOtlpExporter(opts => opts.Endpoint = new Uri(otlpEndpoint));
            }
        });

        return services;
    }
}
