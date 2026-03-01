using System.Text;
using System.Text.Json;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

public interface IVaultDecryptionService
{
    Task<string> DecryptFieldAsync(string encryptedValue, string dekPurpose = "data", CancellationToken ct = default);
    bool IsEncrypted(string? value);
}

public class VaultDecryptionService : IVaultDecryptionService
{
    private readonly IKeyVaultService _kvService;
    private readonly ILogger<VaultDecryptionService> _logger;

    public VaultDecryptionService(IKeyVaultService kvService, ILogger<VaultDecryptionService> logger)
    {
        _kvService = kvService;
        _logger = logger;
    }

    public async Task<string> DecryptFieldAsync(string encryptedValue, string dekPurpose = "data", CancellationToken ct = default)
    {
        if (!IsEncrypted(encryptedValue))
            return encryptedValue;

        try
        {
            var parsed = JsonSerializer.Deserialize<EncryptedFieldValue>(encryptedValue);
            if (parsed is null || string.IsNullOrEmpty(parsed.C) || string.IsNullOrEmpty(parsed.Iv))
                return encryptedValue;

            var purpose = Enum.TryParse<KeyPurpose>(dekPurpose, true, out var kp)
                ? kp : KeyPurpose.Data;

            var plainBytes = await _kvService.DecryptAsync(parsed.C, parsed.Iv, purpose, ct);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decrypt field value");
            return encryptedValue;
        }
    }

    public bool IsEncrypted(string? value)
    {
        if (string.IsNullOrEmpty(value) || !value.StartsWith('{'))
            return false;
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
