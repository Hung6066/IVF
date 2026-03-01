using IVF.Domain.Entities;

namespace IVF.Application.Common.Interfaces;

/// <summary>
/// Validates vault tokens for service-to-service and external API authentication.
/// Tokens are looked up by SHA-256 hash — plaintext is never stored.
/// </summary>
public interface IVaultTokenValidator
{
    /// <summary>
    /// Validate a raw token string. Returns token info if valid, null if invalid/expired/revoked.
    /// Automatically increments usage count.
    /// </summary>
    Task<VaultTokenValidationResult?> ValidateTokenAsync(string rawToken, CancellationToken ct = default);

    /// <summary>
    /// Check if a token has a specific capability for a given path.
    /// Evaluates token policies against vault policy definitions.
    /// </summary>
    Task<bool> HasCapabilityAsync(string rawToken, string path, string capability, CancellationToken ct = default);
}

public record VaultTokenValidationResult(
    Guid TokenId,
    string Accessor,
    string? DisplayName,
    string[] Policies,
    string TokenType,
    DateTime? ExpiresAt,
    int UsesCount);
