using System.Security.Cryptography;
using System.Text;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;

namespace IVF.API.Services;

/// <summary>
/// Background service that manages webhook authentication tokens with automatic rotation.
/// - Creates a vault token with 'webhook-alerts' policy on startup if none exists
/// - Rotates the token periodically (default: every 24 hours)
/// - Stores the current active token as a vault secret for external systems to retrieve
/// - Revokes old tokens on rotation
/// </summary>
public sealed class WebhookKeyRotationService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<WebhookKeyRotationService> logger) : BackgroundService
{
    private const string WebhookPolicy = "webhook-alerts";
    private const string WebhookTokenDisplayName = "webhook-alert-token";
    private const string WebhookSecretPath = "webhooks/alert-token";

    private TimeSpan RotationInterval => TimeSpan.FromHours(
        configuration.GetValue("Webhook:RotationIntervalHours", 24));

    private int TokenTtlSeconds => (int)TimeSpan.FromHours(
        configuration.GetValue("Webhook:TokenTtlHours", 48)).TotalSeconds; // TTL > rotation interval

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for app startup
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EnsureWebhookTokenAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Webhook key rotation failed");
            }

            try
            {
                await Task.Delay(RotationInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            // Rotate after delay
            try
            {
                await RotateWebhookTokenAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Webhook key rotation failed");
            }
        }
    }

    /// <summary>
    /// Ensures a webhook vault token and secret exist. Creates them if missing.
    /// </summary>
    private async Task EnsureWebhookTokenAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IVaultRepository>();
        var secretService = scope.ServiceProvider.GetRequiredService<IVaultSecretService>();

        // Check if a webhook secret already exists with a valid token
        var existingSecret = await secretService.GetSecretAsync(WebhookSecretPath);
        if (existingSecret is not null)
        {
            // Verify the token it references is still valid
            var tokenValue = existingSecret.Value; // decrypted raw token
            if (!string.IsNullOrEmpty(tokenValue))
            {
                var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tokenValue)));
                var existingToken = await repo.GetTokenByHashAsync(tokenHash);
                if (existingToken is not null && existingToken.IsValid)
                {
                    logger.LogInformation("Webhook token is active (accessor: {Accessor}, expires: {ExpiresAt})",
                        existingToken.Accessor, existingToken.ExpiresAt);
                    return;
                }
            }
        }

        // No valid token exists — create a new one
        await CreateNewWebhookTokenAsync(repo, secretService, ct);
    }

    /// <summary>
    /// Rotates the webhook token: creates a new one, updates the secret, revokes the old one.
    /// </summary>
    private async Task RotateWebhookTokenAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IVaultRepository>();
        var secretService = scope.ServiceProvider.GetRequiredService<IVaultSecretService>();

        logger.LogInformation("Rotating webhook authentication token...");

        // Find and revoke old webhooktokens
        var allTokens = await repo.GetTokensAsync(includeRevoked: false);
        var oldWebhookTokens = allTokens
            .Where(t => t.DisplayName == WebhookTokenDisplayName && t.IsValid)
            .ToList();

        // Create new token first (so there's no gap)
        var newRawToken = await CreateNewWebhookTokenAsync(repo, secretService, ct);

        // Revoke old tokens
        foreach (var old in oldWebhookTokens)
        {
            await repo.RevokeTokenAsync(old.Id);
            logger.LogInformation("Revoked old webhook token {Accessor}", old.Accessor);
        }

        // Log audit
        await repo.AddAuditLogAsync(VaultAuditLog.Create(
            "webhook-token.rotate", "Token", null,
            details: $"{{\"revokedCount\":{oldWebhookTokens.Count}}}"));

        logger.LogInformation("Webhook token rotated successfully. Revoked {Count} old token(s)", oldWebhookTokens.Count);
    }

    /// <summary>
    /// Creates a new webhook vault token and stores the raw value as a vault secret.
    /// </summary>
    private async Task<string> CreateNewWebhookTokenAsync(
        IVaultRepository repo,
        IVaultSecretService secretService,
        CancellationToken ct)
    {
        // Generate cryptographically secure random token
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

        var token = VaultToken.Create(
            tokenHash,
            WebhookTokenDisplayName,
            [WebhookPolicy],
            "service",
            TokenTtlSeconds);

        await repo.AddTokenAsync(token);

        // Store the raw token as a vault secret so external systems (Grafana, Prometheus) can retrieve it
        await secretService.PutSecretAsync(
            WebhookSecretPath,
            rawToken,
            metadata: $"{{\"accessor\":\"{token.Accessor}\",\"expiresAt\":\"{token.ExpiresAt:O}\"}}",
            ct: ct);

        await repo.AddAuditLogAsync(VaultAuditLog.Create(
            "webhook-token.create", "Token", token.Id.ToString(),
            details: $"{{\"accessor\":\"{token.Accessor}\",\"ttl\":{TokenTtlSeconds}}}"));

        logger.LogInformation("Created webhook token {Accessor} (TTL: {TtlHours}h, expires: {ExpiresAt})",
            token.Accessor, TokenTtlSeconds / 3600, token.ExpiresAt);

        return rawToken;
    }
}
