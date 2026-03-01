using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

/// <summary>
/// Validates API keys against the ApiKeyManagement DB table (BCrypt hash),
/// with fallback to DesktopClients:ApiKeys in configuration for backward compatibility.
/// </summary>
public class ApiKeyValidator : IApiKeyValidator
{
    private readonly IApiKeyManagementRepository _repo;
    private readonly IVaultRepository _vaultRepo;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiKeyValidator> _logger;

    public ApiKeyValidator(
        IApiKeyManagementRepository repo,
        IVaultRepository vaultRepo,
        IConfiguration configuration,
        ILogger<ApiKeyValidator> logger)
    {
        _repo = repo;
        _vaultRepo = vaultRepo;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ApiKeyValidationResult?> ValidateAsync(string rawKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawKey))
            return null;

        // 1. Try DB-backed validation using prefix lookup + BCrypt verify
        var result = await ValidateFromDatabaseAsync(rawKey, ct);
        if (result is not null)
        {
            _logger.LogDebug("API key validated from database: {KeyName}/{ServiceName}", result.KeyName, result.ServiceName);
            return result;
        }

        // 2. Fallback to config-based keys (backward compatibility)
        result = ValidateFromConfig(rawKey);
        if (result is not null)
        {
            _logger.LogDebug("API key validated from config (legacy): {KeyName}", result.KeyName);
            return result;
        }

        _logger.LogDebug("API key validation failed");
        return null;
    }

    private async Task<ApiKeyValidationResult?> ValidateFromDatabaseAsync(string rawKey, CancellationToken ct)
    {
        // Extract prefix if the key follows the format "PREFIX-..."
        var prefix = ExtractPrefix(rawKey);

        // Get active keys — filter by prefix if available for efficiency
        List<ApiKeyManagement> candidates;
        if (!string.IsNullOrEmpty(prefix))
        {
            // Use service-based lookup; all services with active keys
            candidates = await GetActiveKeysByPrefixAsync(prefix, ct);
        }
        else
        {
            // No prefix pattern recognized; check all active keys (less efficient)
            candidates = await GetAllActiveKeysAsync(ct);
        }

        foreach (var key in candidates)
        {
            if (key.ExpiresAt.HasValue && key.ExpiresAt.Value < DateTime.UtcNow)
                continue;

            try
            {
                if (BCrypt.Net.BCrypt.Verify(rawKey, key.KeyHash))
                {
                    await LogAuditAsync("api_key.validated", key.Id.ToString(), key.KeyName);
                    return new ApiKeyValidationResult(
                        key.Id,
                        key.KeyName,
                        key.ServiceName,
                        key.KeyPrefix,
                        key.Environment,
                        key.Version,
                        "database");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "BCrypt verification failed for key {KeyName}", key.KeyName);
            }
        }

        return null;
    }

    private ApiKeyValidationResult? ValidateFromConfig(string rawKey)
    {
        var validKeys = _configuration.GetSection("DesktopClients:ApiKeys").Get<string[]>();
        if (validKeys is null || validKeys.Length == 0)
            return null;

        // Use constant-time comparison for each config key
        foreach (var configKey in validKeys)
        {
            if (CryptographicEquals(rawKey, configKey))
            {
                return new ApiKeyValidationResult(
                    null,
                    "desktop-client",
                    "DesktopClient",
                    null,
                    null,
                    1,
                    "config");
            }
        }

        return null;
    }

    private static string? ExtractPrefix(string rawKey)
    {
        // Keys typically follow "PREFIX-rest-of-key" format
        var dashIndex = rawKey.IndexOf('-');
        if (dashIndex > 0 && dashIndex < rawKey.Length - 1)
            return rawKey[..dashIndex];
        return null;
    }

    private async Task<List<ApiKeyManagement>> GetActiveKeysByPrefixAsync(string prefix, CancellationToken ct)
    {
        // Get all services, then filter by prefix — repository doesn't have prefix-based lookup
        var allServices = await _repo.GetActiveKeysAsync("DesktopClient", ct);
        var otherKeys = await _repo.GetActiveKeysAsync("desktop-client", ct);
        var combined = new List<ApiKeyManagement>(allServices);
        combined.AddRange(otherKeys);

        // Also try generic active keys for any service
        return combined.Where(k => string.IsNullOrEmpty(k.KeyPrefix) || k.KeyPrefix == prefix).ToList();
    }

    private async Task<List<ApiKeyManagement>> GetAllActiveKeysAsync(CancellationToken ct)
    {
        // Get common service names
        var desktop = await _repo.GetActiveKeysAsync("DesktopClient", ct);
        var desktopAlt = await _repo.GetActiveKeysAsync("desktop-client", ct);
        var combined = new List<ApiKeyManagement>(desktop);
        combined.AddRange(desktopAlt);
        return combined;
    }

    private async Task LogAuditAsync(string action, string resourceId, string keyName)
    {
        try
        {
            var log = VaultAuditLog.Create(
                action: action,
                resourceType: "ApiKey",
                resourceId: resourceId,
                details: System.Text.Json.JsonSerializer.Serialize(new { keyName }));
            await _vaultRepo.AddAuditLogAsync(log);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write API key audit log for {KeyName}", keyName);
        }
    }

    /// <summary>
    /// Constant-time string comparison to prevent timing attacks.
    /// </summary>
    private static bool CryptographicEquals(string a, string b)
    {
        if (a.Length != b.Length)
            return false;

        var result = 0;
        for (var i = 0; i < a.Length; i++)
            result |= a[i] ^ b[i];
        return result == 0;
    }
}
