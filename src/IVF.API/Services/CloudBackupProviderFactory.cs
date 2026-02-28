using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.API.Services;

/// <summary>
/// Creates cloud backup providers on demand based on DB-persisted configuration.
/// Caches the current provider and invalidates when config changes.
/// </summary>
public sealed class CloudBackupProviderFactory(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILoggerFactory loggerFactory)
{
    private ICloudBackupProvider? _cachedProvider;
    private Guid _cachedConfigId;
    private readonly object _lock = new();

    /// <summary>
    /// Get or create a cloud backup provider based on the current DB configuration.
    /// </summary>
    public async Task<ICloudBackupProvider> GetProviderAsync(CancellationToken ct = default)
    {
        var config = await GetConfigAsync(ct);

        lock (_lock)
        {
            if (_cachedProvider != null && _cachedConfigId == config.Id)
                return _cachedProvider;
        }

        var provider = CreateProvider(config);

        lock (_lock)
        {
            // Dispose old provider if it's disposable
            if (_cachedProvider is IDisposable disposable)
                disposable.Dispose();

            _cachedProvider = provider;
            _cachedConfigId = config.Id;
        }

        return provider;
    }

    /// <summary>
    /// Create a provider from explicit config (for test connection without saving).
    /// </summary>
    public ICloudBackupProvider CreateFromConfig(CloudBackupConfig config)
    {
        return CreateProvider(config);
    }

    /// <summary>
    /// Invalidate the cached provider so the next call re-creates it from DB.
    /// </summary>
    public void InvalidateCache()
    {
        lock (_lock)
        {
            if (_cachedProvider is IDisposable disposable)
                disposable.Dispose();
            _cachedProvider = null;
        }
    }

    /// <summary>
    /// Get the current cloud config from DB, creating a default row if none exists.
    /// </summary>
    public async Task<CloudBackupConfig> GetConfigAsync(CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        var config = await db.CloudBackupConfigs.OrderBy(c => c.CreatedAt).FirstOrDefaultAsync(ct);
        if (config != null)
            return config;

        // Seed default config from appsettings
        config = CloudBackupConfig.CreateDefault();
        SeedFromAppSettings(config);
        db.CloudBackupConfigs.Add(config);
        await db.SaveChangesAsync(ct);
        return config;
    }

    /// <summary>
    /// Update cloud config in DB and invalidate cached provider.
    /// </summary>
    public async Task<CloudBackupConfig> UpdateConfigAsync(
        string? provider = null,
        bool? compressionEnabled = null,
        string? s3Region = null,
        string? s3BucketName = null,
        string? s3AccessKey = null,
        string? s3SecretKey = null,
        string? s3ServiceUrl = null,
        bool? s3ForcePathStyle = null,
        string? azureConnectionString = null,
        string? azureContainerName = null,
        string? gcsProjectId = null,
        string? gcsBucketName = null,
        string? gcsCredentialsPath = null,
        CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IvfDbContext>();

        var config = await db.CloudBackupConfigs.OrderBy(c => c.CreatedAt).FirstOrDefaultAsync(ct);
        if (config == null)
        {
            config = CloudBackupConfig.CreateDefault();
            db.CloudBackupConfigs.Add(config);
        }

        config.Update(
            provider, compressionEnabled,
            s3Region, s3BucketName, s3AccessKey, s3SecretKey, s3ServiceUrl, s3ForcePathStyle,
            azureConnectionString, azureContainerName,
            gcsProjectId, gcsBucketName, gcsCredentialsPath);

        await db.SaveChangesAsync(ct);
        InvalidateCache();
        return config;
    }

    private ICloudBackupProvider CreateProvider(CloudBackupConfig config)
    {
        return config.Provider.ToUpperInvariant() switch
        {
            "AZURE" => new CloudProviders.AzureCloudBackupProvider(
                new AzureBlobSettings
                {
                    ConnectionString = config.AzureConnectionString,
                    ContainerName = config.AzureContainerName
                },
                loggerFactory.CreateLogger<CloudProviders.AzureCloudBackupProvider>()),

            "GCS" => new CloudProviders.GcsCloudBackupProvider(
                new GcsSettings
                {
                    ProjectId = config.GcsProjectId,
                    BucketName = config.GcsBucketName,
                    CredentialsPath = config.GcsCredentialsPath
                },
                loggerFactory.CreateLogger<CloudProviders.GcsCloudBackupProvider>()),

            _ => CreateS3Provider(config),
        };
    }

    private ICloudBackupProvider CreateS3Provider(CloudBackupConfig config)
    {
        var isMinIO = config.Provider.Equals("MinIO", StringComparison.OrdinalIgnoreCase);

        var settings = new S3Settings
        {
            Region = config.S3Region,
            BucketName = config.S3BucketName,
            AccessKey = config.S3AccessKey,
            SecretKey = config.S3SecretKey,
            ServiceUrl = config.S3ServiceUrl,
            ForcePathStyle = config.S3ForcePathStyle
        };

        // For MinIO, fall back to appsettings MinIO section if DB credentials are empty
        if (isMinIO)
        {
            if (string.IsNullOrEmpty(settings.AccessKey))
                settings.AccessKey = configuration["MinIO:AccessKey"];
            if (string.IsNullOrEmpty(settings.SecretKey))
                settings.SecretKey = configuration["MinIO:SecretKey"];
            if (string.IsNullOrEmpty(settings.ServiceUrl))
            {
                var useSsl = configuration.GetValue<bool>("MinIO:UseSSL");
                var scheme = useSsl ? "https" : "http";
                settings.ServiceUrl = $"{scheme}://{configuration["MinIO:Endpoint"] ?? "localhost:9000"}";
            }
        }

        var providerName = isMinIO ? "MinIO (S3)" : "AWS S3";
        return new CloudProviders.S3CloudBackupProvider(
            settings,
            loggerFactory.CreateLogger<CloudProviders.S3CloudBackupProvider>(),
            providerName);
    }

    private void SeedFromAppSettings(CloudBackupConfig config)
    {
        var section = configuration.GetSection(CloudBackupSettings.SectionName);
        var settings = section.Get<CloudBackupSettings>();
        if (settings == null) return;

        var s3AccessKey = settings.S3.AccessKey;
        var s3SecretKey = settings.S3.SecretKey;
        var s3ServiceUrl = settings.S3.ServiceUrl;

        // For MinIO provider, fall back to MinIO section if S3 credentials are not configured
        if (settings.Provider.Equals("MinIO", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(s3AccessKey))
                s3AccessKey = configuration["MinIO:AccessKey"];
            if (string.IsNullOrEmpty(s3SecretKey))
                s3SecretKey = configuration["MinIO:SecretKey"];
            if (string.IsNullOrEmpty(s3ServiceUrl))
            {
                var useSsl = configuration.GetValue<bool>("MinIO:UseSSL");
                var scheme = useSsl ? "https" : "http";
                s3ServiceUrl = $"{scheme}://{configuration["MinIO:Endpoint"] ?? "localhost:9000"}";
            }
        }

        config.Update(
            provider: settings.Provider,
            compressionEnabled: settings.CompressionEnabled,
            s3Region: settings.S3.Region,
            s3BucketName: settings.S3.BucketName,
            s3AccessKey: s3AccessKey,
            s3SecretKey: s3SecretKey,
            s3ServiceUrl: s3ServiceUrl,
            s3ForcePathStyle: settings.S3.ForcePathStyle,
            azureConnectionString: settings.Azure.ConnectionString,
            azureContainerName: settings.Azure.ContainerName,
            gcsProjectId: settings.Gcs.ProjectId,
            gcsBucketName: settings.Gcs.BucketName,
            gcsCredentialsPath: settings.Gcs.CredentialsPath);
    }
}
