using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IVF.API.Services;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;

namespace IVF.API.Endpoints;

public static class AlertWebhookEndpoints
{
    public static void MapAlertWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/webhooks/alerts")
            .WithTags("Alert Webhooks")
            .AllowAnonymous(); // Auth handled via vault token validation in handler

        // ─── Grafana webhook receiver ────────────────────────
        group.MapPost("/grafana", HandleGrafanaWebhook)
            .WithName("GrafanaAlertWebhook")
            .WithDescription("Receives alert notifications from Grafana unified alerting");

        // ─── Prometheus Alertmanager webhook receiver ────────
        group.MapPost("/prometheus", HandlePrometheusWebhook)
            .WithName("PrometheusAlertWebhook")
            .WithDescription("Receives alert notifications from Prometheus Alertmanager");

        // ─── Generic alert receiver ─────────────────────────
        group.MapPost("/", HandleGenericWebhook)
            .WithName("GenericAlertWebhook")
            .WithDescription("Generic alert webhook for custom integrations");

        // ─── Webhook health check ───────────────────────────
        group.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
            .WithName("AlertWebhookHealth");

        // ─── Manual token rotation (requires JWT auth) ──────
        app.MapPost("/api/webhooks/alerts/rotate", HandleManualRotation)
            .WithTags("Alert Webhooks")
            .RequireAuthorization()
            .WithName("ManualWebhookTokenRotation")
            .WithDescription("Manually triggers webhook token rotation (admin only)");
    }

    /// <summary>
    /// Validates X-Webhook-Token header against vault tokens with 'webhook-alerts' policy.
    /// </summary>
    private static async Task<bool> ValidateWebhookToken(
        HttpContext ctx,
        IVaultTokenValidator validator,
        ILogger logger)
    {
        var token = ctx.Request.Headers["X-Webhook-Token"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token))
        {
            logger.LogWarning("Alert webhook called without X-Webhook-Token header from {IP}",
                ctx.Connection.RemoteIpAddress);
            return false;
        }

        var result = await validator.ValidateTokenAsync(token);
        if (result is null)
        {
            logger.LogWarning("Alert webhook called with invalid token from {IP}",
                ctx.Connection.RemoteIpAddress);
            return false;
        }

        if (!result.Policies.Contains("webhook-alerts"))
        {
            logger.LogWarning("Alert webhook token {Accessor} missing 'webhook-alerts' policy from {IP}",
                result.Accessor, ctx.Connection.RemoteIpAddress);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Handles Grafana unified alerting webhook format.
    /// Grafana sends: { "status": "firing|resolved", "alerts": [{ "status", "labels", "annotations", ... }] }
    /// </summary>
    private static async Task<IResult> HandleGrafanaWebhook(
        HttpContext ctx,
        JsonElement body,
        IVaultTokenValidator validator,
        DiscordAlertService discord,
        ILogger<DiscordAlertService> logger)
    {
        if (!await ValidateWebhookToken(ctx, validator, logger))
            return Results.Unauthorized();

        try
        {
            var status = body.TryGetProperty("status", out var s) ? s.GetString() : "unknown";
            var alerts = body.TryGetProperty("alerts", out var a) ? a : default;

            if (alerts.ValueKind != JsonValueKind.Array)
                return Results.BadRequest(new { error = "Missing 'alerts' array" });

            var processed = 0;
            foreach (var alert in alerts.EnumerateArray())
            {
                var alertStatus = alert.TryGetProperty("status", out var ast) ? ast.GetString() : status;
                var labels = alert.TryGetProperty("labels", out var lbl) ? lbl : default;
                var annotations = alert.TryGetProperty("annotations", out var ann) ? ann : default;

                var alertName = labels.ValueKind == JsonValueKind.Object &&
                                labels.TryGetProperty("alertname", out var an)
                    ? an.GetString() ?? "Unknown"
                    : "Unknown";

                var severity = labels.ValueKind == JsonValueKind.Object &&
                               labels.TryGetProperty("severity", out var sev)
                    ? sev.GetString() ?? "warning"
                    : "warning";

                var summary = annotations.ValueKind == JsonValueKind.Object &&
                              annotations.TryGetProperty("summary", out var sum)
                    ? sum.GetString() ?? ""
                    : "";

                var description = annotations.ValueKind == JsonValueKind.Object &&
                                  annotations.TryGetProperty("description", out var desc)
                    ? desc.GetString() ?? ""
                    : "";

                var message = !string.IsNullOrEmpty(summary) ? summary : description;
                if (string.IsNullOrEmpty(message))
                    message = $"Alert '{alertName}' is {alertStatus}";

                // Map resolved status to info level
                var level = alertStatus == "resolved" ? "warning"
                    : severity == "critical" ? "critical"
                    : "warning";

                var source = $"Grafana: {alertName}";
                if (alertStatus == "resolved")
                    message = $"✅ ĐÃ KHẮC PHỤC: {message}";

                await discord.SendAlertAsync(source, message, level);
                processed++;
            }

            logger.LogInformation("Processed {Count} Grafana alerts (status: {Status})", processed, status);
            return Results.Ok(new { success = true, processed, status });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process Grafana webhook");
            return Results.Json(new { error = "Failed to process webhook payload" }, statusCode: 500);
        }
    }

    /// <summary>
    /// Handles Prometheus Alertmanager webhook format.
    /// Alertmanager sends: { "status": "firing|resolved", "alerts": [{ "status", "labels", "annotations", ... }] }
    /// </summary>
    private static async Task<IResult> HandlePrometheusWebhook(
        HttpContext ctx,
        JsonElement body,
        IVaultTokenValidator validator,
        DiscordAlertService discord,
        ILogger<DiscordAlertService> logger)
    {
        if (!await ValidateWebhookToken(ctx, validator, logger))
            return Results.Unauthorized();

        try
        {
            var status = body.TryGetProperty("status", out var s) ? s.GetString() : "unknown";
            var alerts = body.TryGetProperty("alerts", out var a) ? a : default;

            if (alerts.ValueKind != JsonValueKind.Array)
                return Results.BadRequest(new { error = "Missing 'alerts' array" });

            var processed = 0;
            foreach (var alert in alerts.EnumerateArray())
            {
                var alertStatus = alert.TryGetProperty("status", out var ast) ? ast.GetString() : status;
                var labels = alert.TryGetProperty("labels", out var lbl) ? lbl : default;
                var annotations = alert.TryGetProperty("annotations", out var ann) ? ann : default;

                var alertName = labels.ValueKind == JsonValueKind.Object &&
                                labels.TryGetProperty("alertname", out var an)
                    ? an.GetString() ?? "Unknown"
                    : "Unknown";

                var severity = labels.ValueKind == JsonValueKind.Object &&
                               labels.TryGetProperty("severity", out var sev)
                    ? sev.GetString() ?? "warning"
                    : "warning";

                var summary = annotations.ValueKind == JsonValueKind.Object &&
                              annotations.TryGetProperty("summary", out var sum)
                    ? sum.GetString() ?? ""
                    : "";

                var description = annotations.ValueKind == JsonValueKind.Object &&
                                  annotations.TryGetProperty("description", out var desc)
                    ? desc.GetString() ?? ""
                    : "";

                var message = !string.IsNullOrEmpty(summary) ? summary : description;
                if (string.IsNullOrEmpty(message))
                    message = $"Alert '{alertName}' is {alertStatus}";

                var level = alertStatus == "resolved" ? "warning"
                    : severity == "critical" ? "critical"
                    : "warning";

                var source = $"Prometheus: {alertName}";
                if (alertStatus == "resolved")
                    message = $"✅ ĐÃ KHẮC PHỤC: {message}";

                await discord.SendAlertAsync(source, message, level);
                processed++;
            }

            logger.LogInformation("Processed {Count} Prometheus alerts (status: {Status})", processed, status);
            return Results.Ok(new { success = true, processed, status });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process Prometheus webhook");
            return Results.Json(new { error = "Failed to process webhook payload" }, statusCode: 500);
        }
    }

    /// <summary>
    /// Generic webhook for custom integrations.
    /// Accepts: { "source": "...", "message": "...", "level": "warning|critical" }
    /// Or array: [{ "source", "message", "level" }, ...]
    /// </summary>
    private static async Task<IResult> HandleGenericWebhook(
        HttpContext ctx,
        JsonElement body,
        IVaultTokenValidator validator,
        DiscordAlertService discord,
        ILogger<DiscordAlertService> logger)
    {
        if (!await ValidateWebhookToken(ctx, validator, logger))
            return Results.Unauthorized();

        try
        {
            var processed = 0;

            if (body.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in body.EnumerateArray())
                {
                    await ProcessGenericAlert(item, discord);
                    processed++;
                }
            }
            else
            {
                await ProcessGenericAlert(body, discord);
                processed = 1;
            }

            logger.LogInformation("Processed {Count} generic alert(s)", processed);
            return Results.Ok(new { success = true, processed });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process generic webhook");
            return Results.Json(new { error = "Failed to process webhook payload" }, statusCode: 500);
        }
    }

    private static async Task ProcessGenericAlert(JsonElement item, DiscordAlertService discord)
    {
        var source = item.TryGetProperty("source", out var src) ? src.GetString() ?? "External" : "External";
        var message = item.TryGetProperty("message", out var msg) ? msg.GetString() ?? "" : "";
        var level = item.TryGetProperty("level", out var lvl) ? lvl.GetString() ?? "warning" : "warning";

        if (!string.IsNullOrEmpty(message))
            await discord.SendAlertAsync(source, message, level);
    }

    /// <summary>
    /// Manually triggers webhook token rotation (requires JWT authentication).
    /// </summary>
    private static async Task<IResult> HandleManualRotation(
        IVaultRepository repo,
        IVaultSecretService secretService,
        ILogger<WebhookKeyRotationService> logger)
    {
        try
        {
            var allTokens = await repo.GetTokensAsync(includeRevoked: false);
            var oldWebhookTokens = allTokens
                .Where(t => t.DisplayName == "webhook-alert-token" && t.IsValid)
                .ToList();

            // Generate new token
            var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

            var token = VaultToken.Create(
                tokenHash,
                "webhook-alert-token",
                ["webhook-alerts"],
                "service",
                172800); // 48h TTL

            await repo.AddTokenAsync(token);

            await secretService.PutSecretAsync(
                "webhooks/alert-token",
                rawToken,
                metadata: $"{{\"accessor\":\"{token.Accessor}\",\"expiresAt\":\"{token.ExpiresAt:O}\",\"rotatedBy\":\"manual\"}}");

            // Revoke old tokens
            foreach (var old in oldWebhookTokens)
            {
                await repo.RevokeTokenAsync(old.Id);
            }

            await repo.AddAuditLogAsync(VaultAuditLog.Create(
                "webhook-token.rotate", "Token", token.Id.ToString(),
                details: $"{{\"revokedCount\":{oldWebhookTokens.Count},\"trigger\":\"manual\"}}"));

            logger.LogInformation("Manual webhook token rotation completed. Revoked {Count} old token(s)", oldWebhookTokens.Count);

            return Results.Ok(new
            {
                success = true,
                message = $"Token đã được xoay thành công. Revoked {oldWebhookTokens.Count} token cũ."
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Manual webhook token rotation failed");
            return Results.Json(new { error = "Failed to rotate webhook token" }, statusCode: 500);
        }
    }
}
