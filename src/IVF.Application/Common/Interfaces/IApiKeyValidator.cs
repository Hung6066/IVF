namespace IVF.Application.Common.Interfaces;

/// <summary>
/// Centralized API key validation service.
/// Validates against DB-stored keys (BCrypt hash) with fallback to config-based keys.
/// </summary>
public interface IApiKeyValidator
{
    /// <summary>
    /// Validate an API key. Returns key info if valid, null if invalid/expired/inactive.
    /// </summary>
    Task<ApiKeyValidationResult?> ValidateAsync(string rawKey, CancellationToken ct = default);
}

/// <summary>
/// Result of a successful API key validation.
/// </summary>
public record ApiKeyValidationResult(
    Guid? KeyId,
    string KeyName,
    string ServiceName,
    string? KeyPrefix,
    string? Environment,
    int Version,
    string Source // "database" or "config"
);
