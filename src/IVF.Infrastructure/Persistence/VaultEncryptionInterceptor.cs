using System.Text;
using System.Text.Json;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Persistence;

/// <summary>
/// EF Core interceptor that automatically encrypts sensitive fields on save
/// based on EncryptionConfig entries in the database.
/// Fields are stored as JSON: {"c":"base64","iv":"base64"} when encrypted.
/// Uses IServiceProvider to lazily resolve IVaultRepository, breaking the
/// circular dependency: IvfDbContext → VaultEncryptionInterceptor → IVaultRepository → IvfDbContext.
/// </summary>
public class VaultEncryptionInterceptor : SaveChangesInterceptor
{
    private readonly IKeyVaultService _kvService;
    private readonly ILogger<VaultEncryptionInterceptor> _logger;

    private List<EncryptionConfig>? _configsCache;

    public VaultEncryptionInterceptor(
        IKeyVaultService kvService,
        ILogger<VaultEncryptionInterceptor> logger)
    {
        _kvService = kvService;
        _logger = logger;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return await base.SavingChangesAsync(eventData, result, cancellationToken);

        try
        {
            // Query EncryptionConfigs directly from the context to avoid circular dependency
            // (IvfDbContext → VaultEncryptionInterceptor → IVaultRepository → IvfDbContext)
            if (eventData.Context is IvfDbContext db)
                _configsCache ??= await db.EncryptionConfigs.ToListAsync(cancellationToken);
            else
                return await base.SavingChangesAsync(eventData, result, cancellationToken);

            var enabledConfigs = _configsCache.Where(c => c.IsEnabled).ToList();
            if (enabledConfigs.Count == 0)
                return await base.SavingChangesAsync(eventData, result, cancellationToken);

            foreach (var entry in eventData.Context.ChangeTracker.Entries()
                .Where(e => e.State is EntityState.Added or EntityState.Modified))
            {
                var tableName = entry.Metadata.GetTableName();
                if (tableName is null) continue;

                var config = enabledConfigs.FirstOrDefault(c =>
                    string.Equals(c.TableName, tableName, StringComparison.OrdinalIgnoreCase));
                if (config is null) continue;

                var purpose = Enum.TryParse<KeyPurpose>(config.DekPurpose, true, out var kp)
                    ? kp : KeyPurpose.Data;

                foreach (var fieldName in config.EncryptedFields)
                {
                    var prop = entry.Properties.FirstOrDefault(p =>
                        string.Equals(p.Metadata.GetColumnName(), fieldName, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(p.Metadata.Name, fieldName, StringComparison.OrdinalIgnoreCase));

                    if (prop?.CurrentValue is not string value || string.IsNullOrEmpty(value))
                        continue;

                    // Skip already-encrypted values (JSON with "c" and "iv" keys)
                    if (IsAlreadyEncrypted(value))
                        continue;

                    var encrypted = await _kvService.EncryptAsync(
                        Encoding.UTF8.GetBytes(value), purpose, cancellationToken);

                    prop.CurrentValue = JsonSerializer.Serialize(new EncryptedFieldValue
                    {
                        C = encrypted.CiphertextBase64,
                        Iv = encrypted.IvBase64
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Vault encryption interceptor failed — data saved unencrypted");
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static bool IsAlreadyEncrypted(string value)
    {
        if (!value.StartsWith('{')) return false;
        try
        {
            var doc = JsonDocument.Parse(value);
            return doc.RootElement.TryGetProperty("c", out _)
                && doc.RootElement.TryGetProperty("iv", out _);
        }
        catch
        {
            return false;
        }
    }

    private sealed class EncryptedFieldValue
    {
        public string C { get; init; } = "";
        public string Iv { get; init; } = "";
    }
}
