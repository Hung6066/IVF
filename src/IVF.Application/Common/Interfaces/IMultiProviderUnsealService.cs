namespace IVF.Application.Common.Interfaces;

/// <summary>
/// Multi-provider auto-unseal — supports primary + fallback KMS providers
/// for vault master key unwrapping. Follows HashiCorp Vault seal/unseal
/// pattern with configurable provider priority.
/// </summary>
public interface IMultiProviderUnsealService
{
    /// <summary>
    /// Attempt auto-unseal using configured providers in priority order.
    /// Falls back to secondary if primary fails.
    /// </summary>
    Task<UnsealResult> AutoUnsealAsync(CancellationToken ct = default);

    /// <summary>
    /// Configure a new unseal provider. Wraps the master password with the provider's key.
    /// </summary>
    Task<bool> ConfigureProviderAsync(UnsealProviderConfig config, string masterPassword, CancellationToken ct = default);

    /// <summary>Get status of all configured unseal providers.</summary>
    Task<List<UnsealProviderStatus>> GetProviderStatusAsync(CancellationToken ct = default);
}

public sealed record UnsealProviderConfig(
    string ProviderId,
    string ProviderType, // "Azure", "Local", "Shamir"
    int Priority,        // Lower = tried first
    string KeyIdentifier,
    Dictionary<string, string>? Settings = null);

public sealed record UnsealProviderStatus(
    string ProviderId,
    string ProviderType,
    int Priority,
    bool Available,
    DateTime? LastUsedAt,
    string? Error);

public sealed record UnsealResult(
    bool Success,
    string ProviderId,
    string? Error,
    int AttemptsTotal);
