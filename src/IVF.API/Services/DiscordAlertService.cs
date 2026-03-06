using System.Collections.Concurrent;
using System.Text.Json;

namespace IVF.API.Services;

/// <summary>
/// Sends infrastructure alerts to a Discord channel via webhook.
/// Rate-limited: max 1 alert per source per 5 minutes to avoid flooding.
/// </summary>
public sealed class DiscordAlertService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<DiscordAlertService> logger)
{
    private readonly ConcurrentDictionary<string, DateTime> _lastAlertTimes = new();
    private static readonly TimeSpan CooldownPeriod = TimeSpan.FromMinutes(5);

    private string? WebhookUrl => configuration["Discord:WebhookUrl"];
    private bool Enabled => configuration.GetValue("Discord:Enabled", false);
    private string MinLevel => configuration.GetValue("Discord:MinLevel", "warning") ?? "warning";

    /// <summary>
    /// Send alert to Discord if enabled and not rate-limited.
    /// </summary>
    public async Task SendAlertAsync(string source, string message, string level, CancellationToken ct = default)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(WebhookUrl))
            return;

        // Level filter: skip if below minimum
        if (MinLevel == "critical" && level != "critical")
            return;

        // Rate limiting per source
        var now = DateTime.UtcNow;
        if (_lastAlertTimes.TryGetValue(source, out var lastTime) && now - lastTime < CooldownPeriod)
            return;

        _lastAlertTimes[source] = now;

        try
        {
            var color = level == "critical" ? 0xDC2626 : 0xF59E0B; // red : yellow
            var emoji = level == "critical" ? "🔴" : "⚠️";
            var levelLabel = level == "critical" ? "NGHIÊM TRỌNG" : "CẢNH BÁO";

            var payload = new
            {
                embeds = new[]
                {
                    new
                    {
                        title = $"{emoji} {levelLabel}: {source}",
                        description = message,
                        color,
                        timestamp = now.ToString("o"),
                        footer = new { text = "IVF Infrastructure Monitor" },
                        fields = new[]
                        {
                            new { name = "Nguồn", value = source, inline = true },
                            new { name = "Mức độ", value = levelLabel, inline = true },
                            new { name = "Hostname", value = Environment.MachineName, inline = true }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(WebhookUrl, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Discord webhook returned {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send Discord alert for {Source}", source);
        }
    }

    /// <summary>
    /// Send multiple alerts in batch.
    /// </summary>
    public async Task SendAlertsAsync(IReadOnlyList<AlertDto> alerts, CancellationToken ct = default)
    {
        foreach (var alert in alerts)
        {
            await SendAlertAsync(alert.Source, alert.Message, alert.Level, ct);
        }
    }
}
