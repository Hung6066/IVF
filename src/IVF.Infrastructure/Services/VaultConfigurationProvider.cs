using IVF.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

/// <summary>
/// Configuration provider that injects vault secrets into the IConfiguration pipeline.
/// Secrets stored under "config/" prefix in vault are mapped to configuration keys.
/// Example: vault path "config/ConnectionStrings/Redis" → IConfiguration["ConnectionStrings:Redis"]
/// </summary>
public class VaultConfigurationProvider : ConfigurationProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VaultConfigurationProvider> _logger;
    private Timer? _reloadTimer;
    private readonly TimeSpan _reloadInterval;

    public VaultConfigurationProvider(
        IServiceScopeFactory scopeFactory,
        ILogger<VaultConfigurationProvider> logger,
        TimeSpan? reloadInterval = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _reloadInterval = reloadInterval ?? TimeSpan.FromMinutes(5);
    }

    public override void Load()
    {
        LoadAsync().GetAwaiter().GetResult();

        _reloadTimer ??= new Timer(
            _ => ReloadAsync(),
            null,
            _reloadInterval,
            _reloadInterval);
    }

    private async Task LoadAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var secretService = scope.ServiceProvider.GetRequiredService<IVaultSecretService>();

            var entries = await secretService.ListSecretsAsync("config/");
            var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            await LoadRecursiveAsync(secretService, "config/", data);

            Data = data;
            _logger.LogInformation("Vault configuration loaded: {Count} secrets", data.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load vault configuration — using existing values");
        }
    }

    private async Task LoadRecursiveAsync(
        IVaultSecretService secretService,
        string prefix,
        Dictionary<string, string?> data)
    {
        var entries = await secretService.ListSecretsAsync(prefix);

        foreach (var entry in entries)
        {
            var fullPath = prefix + entry.Name;

            if (entry.Type == "folder")
            {
                await LoadRecursiveAsync(secretService, fullPath, data);
            }
            else
            {
                var secret = await secretService.GetSecretAsync(fullPath.TrimEnd('/'));
                if (secret is not null)
                {
                    // Convert vault path to config key: "config/ConnectionStrings/Redis" → "ConnectionStrings:Redis"
                    var configKey = fullPath
                        .Replace("config/", "", StringComparison.OrdinalIgnoreCase)
                        .TrimEnd('/')
                        .Replace('/', ':');
                    data[configKey] = secret.Value;
                }
            }
        }
    }

    private async void ReloadAsync()
    {
        try
        {
            await LoadAsync();
            OnReload();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to reload vault configuration");
        }
    }

    public void Dispose()
    {
        _reloadTimer?.Dispose();
    }
}

/// <summary>
/// Configuration source that adds vault secrets to the configuration pipeline.
/// </summary>
public class VaultConfigurationSource : IConfigurationSource
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly TimeSpan? _reloadInterval;

    public VaultConfigurationSource(
        IServiceScopeFactory scopeFactory,
        ILoggerFactory loggerFactory,
        TimeSpan? reloadInterval = null)
    {
        _scopeFactory = scopeFactory;
        _loggerFactory = loggerFactory;
        _reloadInterval = reloadInterval;
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new VaultConfigurationProvider(
            _scopeFactory,
            _loggerFactory.CreateLogger<VaultConfigurationProvider>(),
            _reloadInterval);
    }
}

/// <summary>
/// Extension methods for adding vault configuration to the application.
/// </summary>
public static class VaultConfigurationExtensions
{
    /// <summary>
    /// Adds vault secrets (under "config/" prefix) to the configuration pipeline.
    /// Call after building the service provider but before app.Run().
    /// </summary>
    public static IConfigurationBuilder AddVaultSecrets(
        this IConfigurationBuilder builder,
        IServiceProvider serviceProvider,
        TimeSpan? reloadInterval = null)
    {
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        builder.Add(new VaultConfigurationSource(scopeFactory, loggerFactory, reloadInterval));
        return builder;
    }
}
