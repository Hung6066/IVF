using System.Text.Json;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

/// <summary>
/// Multi-provider auto-unseal with priority-based failover.
/// Tries each configured provider in priority order until one succeeds.
/// Stores provider configs in vault settings for persistence.
/// </summary>
public sealed class MultiProviderUnsealService : IMultiProviderUnsealService
{
    private readonly IKeyVaultService _kvService;
    private readonly IVaultRepository _repo;
    private readonly ISecurityEventPublisher _events;
    private readonly ILogger<MultiProviderUnsealService> _logger;

    private const string ProvidersSettingKey = "unseal-providers";

    public MultiProviderUnsealService(
        IKeyVaultService kvService,
        IVaultRepository repo,
        ISecurityEventPublisher events,
        ILogger<MultiProviderUnsealService> logger)
    {
        _kvService = kvService;
        _repo = repo;
        _events = events;
        _logger = logger;
    }

    public async Task<UnsealResult> AutoUnsealAsync(CancellationToken ct = default)
    {
        var providers = await GetProvidersAsync(ct);
        if (providers.Count == 0)
        {
            // Fall back to default single-provider unseal
            var defaultResult = await _kvService.AutoUnsealAsync(ct);
            return new UnsealResult(defaultResult, "default-azure", defaultResult ? null : "Default auto-unseal failed", 1);
        }

        var ordered = providers.OrderBy(p => p.Priority).ToList();
        var attempts = 0;

        foreach (var provider in ordered)
        {
            attempts++;
            try
            {
                var success = provider.ProviderType switch
                {
                    "Azure" => await _kvService.AutoUnsealAsync(ct),
                    "Local" => await TryLocalUnsealAsync(provider, ct),
                    _ => false
                };

                if (success)
                {
                    provider.LastUsedAt = DateTime.UtcNow;
                    await SaveProvidersAsync(providers, ct);

                    _logger.LogInformation("Auto-unseal succeeded via provider {ProviderId} (type={Type})",
                        provider.ProviderId, provider.ProviderType);

                    return new UnsealResult(true, provider.ProviderId, null, attempts);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Auto-unseal failed for provider {ProviderId}", provider.ProviderId);
            }
        }

        await _events.PublishAsync(new VaultSecurityEvent
        {
            EventType = "vault.unseal.all_failed",
            Severity = Domain.Enums.SecuritySeverity.Critical,
            Source = "MultiProviderUnsealService",
            Action = "vault.unseal",
            ResourceType = "Vault",
            ResourceId = "master",
            Outcome = "failure",
            Reason = $"All {attempts} unseal providers failed"
        }, ct);

        return new UnsealResult(false, "", $"All {attempts} providers failed", attempts);
    }

    public async Task<bool> ConfigureProviderAsync(UnsealProviderConfig config, string masterPassword, CancellationToken ct = default)
    {
        var providers = await GetProvidersAsync(ct);

        // Remove existing with same ID
        providers.RemoveAll(p => p.ProviderId == config.ProviderId);

        var stored = new StoredProvider
        {
            ProviderId = config.ProviderId,
            ProviderType = config.ProviderType,
            Priority = config.Priority,
            KeyIdentifier = config.KeyIdentifier,
            Settings = config.Settings ?? new(),
            ConfiguredAt = DateTime.UtcNow,
        };

        // For Azure, configure via existing KV service
        if (config.ProviderType == "Azure")
        {
            var success = await _kvService.ConfigureAutoUnsealAsync(masterPassword, config.KeyIdentifier, ct);
            if (!success) return false;
        }

        providers.Add(stored);
        await SaveProvidersAsync(providers, ct);

        await _repo.AddAuditLogAsync(VaultAuditLog.Create(
            "unseal.provider.configured", "UnsealProvider", config.ProviderId,
            details: JsonSerializer.Serialize(new { config.ProviderType, config.Priority })));

        return true;
    }

    public async Task<List<UnsealProviderStatus>> GetProviderStatusAsync(CancellationToken ct = default)
    {
        var providers = await GetProvidersAsync(ct);
        var statuses = new List<UnsealProviderStatus>();

        foreach (var p in providers.OrderBy(p => p.Priority))
        {
            var available = p.ProviderType switch
            {
                "Azure" => await _kvService.IsHealthyAsync(ct),
                "Local" => true,
                _ => false
            };

            statuses.Add(new UnsealProviderStatus(
                p.ProviderId, p.ProviderType, p.Priority,
                available, p.LastUsedAt, available ? null : "Provider unavailable"));
        }

        return statuses;
    }

    private Task<bool> TryLocalUnsealAsync(StoredProvider provider, CancellationToken ct)
    {
        // Local unseal uses the existing auto-unseal mechanism
        // which stores wrapped key in vault settings
        return _kvService.AutoUnsealAsync(ct);
    }

    private async Task<List<StoredProvider>> GetProvidersAsync(CancellationToken ct)
    {
        var setting = await _repo.GetSettingAsync(ProvidersSettingKey, ct);
        if (setting is null) return new List<StoredProvider>();

        return JsonSerializer.Deserialize<List<StoredProvider>>(setting.ValueJson) ?? new List<StoredProvider>();
    }

    private async Task SaveProvidersAsync(List<StoredProvider> providers, CancellationToken ct)
    {
        await _repo.SaveSettingAsync(ProvidersSettingKey,
            JsonSerializer.Serialize(providers), ct);
    }

    private sealed class StoredProvider
    {
        public string ProviderId { get; set; } = "";
        public string ProviderType { get; set; } = "";
        public int Priority { get; set; }
        public string KeyIdentifier { get; set; } = "";
        public Dictionary<string, string> Settings { get; set; } = new();
        public DateTime ConfiguredAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
    }
}
