using System.Security.Cryptography;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Enums;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

public class AzureKeyVaultService : IKeyVaultService
{
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AzureKeyVaultService> _logger;
    private readonly bool _isEnabled;
    private readonly object? _secretClient;
    private readonly object? _cryptoClient;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);
    private readonly string? _vaultUrl;

    private const string CachePrefix = "kv:";
    private const int IvLength = 12; // AES-GCM nonce
    private const int TagLength = 16; // AES-GCM tag
    private const int KeyLength = 32; // 256-bit
    private const int SaltLength = 32;
    private const int Pbkdf2Iterations = 100_000;

    // Auto-unseal state (in-memory; persisted as vault secrets)
    private const string AutoUnsealWrappedKeySecretName = "auto-unseal-wrapped-master";
    private const string AutoUnsealIvSecretName = "auto-unseal-iv";
    private const string AutoUnsealKeyNameSecretName = "auto-unseal-key-name";
    private const string AutoUnsealConfiguredAtSecretName = "auto-unseal-configured-at";

    public AzureKeyVaultService(
        IConfiguration configuration,
        IMemoryCache cache,
        ILogger<AzureKeyVaultService> logger)
    {
        _configuration = configuration;
        _cache = cache;
        _logger = logger;
        _isEnabled = configuration.GetValue<bool>("AzureKeyVault:Enabled");
        _vaultUrl = configuration["AzureKeyVault:VaultUrl"];

        if (_isEnabled)
        {
            _secretClient = CreateSecretClient(configuration);
            _cryptoClient = CreateCryptoClient(configuration);

            if (_secretClient is not null)
                _logger.LogInformation("Azure Key Vault enabled — connected to {VaultUrl}", _vaultUrl);
            else
                _logger.LogWarning("Azure Key Vault enabled but SecretClient creation failed — falling back to local config");

            if (_cryptoClient is not null)
                _logger.LogInformation("Azure Key Vault CryptographyClient ready for key wrap/unwrap");
        }
        else
        {
            _logger.LogInformation("Azure Key Vault disabled — using local configuration for secrets");
        }
    }

    public async Task<string> GetSecretAsync(string secretName, CancellationToken ct = default)
    {
        var cacheKey = $"{CachePrefix}{secretName}";

        if (_cache.TryGetValue(cacheKey, out string? cached) && cached is not null)
            return cached;

        string value;

        if (_isEnabled && _secretClient is not null)
        {
            value = await GetFromAzureAsync(secretName, ct);
        }
        else
        {
            value = GetFromLocalConfig(secretName);
        }

        _cache.Set(cacheKey, value, _cacheExpiration);
        return value;
    }

    public async Task SetSecretAsync(string secretName, string secretValue, CancellationToken ct = default)
    {
        if (_isEnabled && _secretClient is not null)
        {
            await SetInAzureAsync(secretName, secretValue, ct);
        }
        else
        {
            _logger.LogWarning("SetSecret called in local mode — secret '{SecretName}' not persisted to vault", secretName);
        }

        var cacheKey = $"{CachePrefix}{secretName}";
        _cache.Set(cacheKey, secretValue, _cacheExpiration);
    }

    public async Task<string> GetSecretVersionAsync(string secretName, string version, CancellationToken ct = default)
    {
        if (_isEnabled && _secretClient is not null)
        {
            return await GetVersionFromAzureAsync(secretName, version, ct);
        }

        // Local config doesn't support versioning — return current value
        return GetFromLocalConfig(secretName);
    }

    public async Task DeleteSecretAsync(string secretName, CancellationToken ct = default)
    {
        if (_isEnabled && _secretClient is not null)
        {
            await DeleteFromAzureAsync(secretName, ct);
        }
        else
        {
            _logger.LogWarning("DeleteSecret called in local mode — secret '{SecretName}' not deleted from vault", secretName);
        }

        var cacheKey = $"{CachePrefix}{secretName}";
        _cache.Remove(cacheKey);
    }

    public async Task<IEnumerable<string>> ListSecretsAsync(CancellationToken ct = default)
    {
        if (_isEnabled && _secretClient is not null)
        {
            return await ListFromAzureAsync(ct);
        }

        // List keys under Secrets section in local config
        var section = _configuration.GetSection("Secrets");
        return section.GetChildren().Select(c => c.Key).ToList();
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        if (!_isEnabled || _secretClient is null)
            return true; // Local config is always "healthy"

        try
        {
            await CheckAzureHealthAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Key Vault health check failed");
            return false;
        }
    }

    // ─── Local Config Fallback ───

    private string GetFromLocalConfig(string secretName)
    {
        // Try Secrets:xxx first, then environment variable
        var value = _configuration[$"Secrets:{secretName}"];
        if (!string.IsNullOrEmpty(value))
            return value;

        var envValue = Environment.GetEnvironmentVariable($"SECRETS_{secretName.Replace("-", "_").ToUpperInvariant()}");
        return envValue ?? string.Empty;
    }

    // ─── Azure SDK Methods (dynamic invocation to avoid hard dependency) ───

    private static object? CreateAzureCredential(IConfiguration configuration)
    {
        var identityAssembly = System.Reflection.Assembly.Load("Azure.Identity");

        var tenantId = configuration["AzureKeyVault:TenantId"];
        var clientId = configuration["AzureKeyVault:ClientId"];
        var clientSecret = configuration["AzureKeyVault:ClientSecret"];

        // Use ClientSecretCredential when explicit credentials are configured
        if (!string.IsNullOrWhiteSpace(tenantId) &&
            !string.IsNullOrWhiteSpace(clientId) &&
            !string.IsNullOrWhiteSpace(clientSecret))
        {
            var credType = identityAssembly.GetType("Azure.Identity.ClientSecretCredential")!;
            return Activator.CreateInstance(credType, tenantId, clientId, clientSecret);
        }

        // Fallback to DefaultAzureCredential (managed identity, az cli, etc.)
        var defaultCredType = identityAssembly.GetType("Azure.Identity.DefaultAzureCredential")!;
        return Activator.CreateInstance(defaultCredType);
    }

    private static object? CreateSecretClient(IConfiguration configuration)
    {
        try
        {
            var vaultUrl = configuration["AzureKeyVault:VaultUrl"];
            if (string.IsNullOrWhiteSpace(vaultUrl))
                return null;

            var secretsAssembly = System.Reflection.Assembly.Load("Azure.Security.KeyVault.Secrets");
            var clientType = secretsAssembly.GetType("Azure.Security.KeyVault.Secrets.SecretClient")!;

            var credential = CreateAzureCredential(configuration);
            var client = Activator.CreateInstance(clientType, new Uri(vaultUrl), credential);

            return client;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task<string> GetFromAzureAsync(string secretName, CancellationToken ct)
    {
        try
        {
            var clientType = _secretClient!.GetType();
            var method = clientType.GetMethod("GetSecretAsync",
                [typeof(string), typeof(string), typeof(CancellationToken)]);

            var task = (Task)method!.Invoke(_secretClient, [secretName, null, ct])!;
            await task;

            var resultProperty = task.GetType().GetProperty("Result");
            var response = resultProperty!.GetValue(task);
            var valueProperty = response!.GetType().GetProperty("Value");
            var secret = valueProperty!.GetValue(response);
            var secretValueProp = secret!.GetType().GetProperty("Value");

            return secretValueProp!.GetValue(secret)?.ToString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get secret '{SecretName}' from Azure Key Vault — falling back to local config", secretName);
            return GetFromLocalConfig(secretName);
        }
    }

    private async Task SetInAzureAsync(string secretName, string secretValue, CancellationToken ct)
    {
        try
        {
            var clientType = _secretClient!.GetType();
            var secretsAssembly = clientType.Assembly;
            var keyVaultSecretType = secretsAssembly.GetType("Azure.Security.KeyVault.Secrets.KeyVaultSecret")!;
            var secret = Activator.CreateInstance(keyVaultSecretType, secretName, secretValue);

            var method = clientType.GetMethod("SetSecretAsync",
                [keyVaultSecretType, typeof(CancellationToken)]);

            var task = (Task)method!.Invoke(_secretClient, [secret, ct])!;
            await task;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set secret '{SecretName}' in Azure Key Vault", secretName);
            throw;
        }
    }

    private async Task<string> GetVersionFromAzureAsync(string secretName, string version, CancellationToken ct)
    {
        try
        {
            var clientType = _secretClient!.GetType();
            var method = clientType.GetMethod("GetSecretAsync",
                [typeof(string), typeof(string), typeof(CancellationToken)]);

            var task = (Task)method!.Invoke(_secretClient, [secretName, version, ct])!;
            await task;

            var resultProperty = task.GetType().GetProperty("Result");
            var response = resultProperty!.GetValue(task);
            var valueProperty = response!.GetType().GetProperty("Value");
            var secret = valueProperty!.GetValue(response);
            var secretValueProp = secret!.GetType().GetProperty("Value");

            return secretValueProp!.GetValue(secret)?.ToString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get secret version '{SecretName}' v{Version} from Azure Key Vault", secretName, version);
            return GetFromLocalConfig(secretName);
        }
    }

    private async Task DeleteFromAzureAsync(string secretName, CancellationToken ct)
    {
        try
        {
            var clientType = _secretClient!.GetType();
            var method = clientType.GetMethod("StartDeleteSecretAsync",
                [typeof(string), typeof(CancellationToken)]);

            var task = (Task)method!.Invoke(_secretClient, [secretName, ct])!;
            await task;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete secret '{SecretName}' from Azure Key Vault", secretName);
            throw;
        }
    }

    private async Task<IEnumerable<string>> ListFromAzureAsync(CancellationToken ct)
    {
        try
        {
            var clientType = _secretClient!.GetType();
            var method = clientType.GetMethod("GetPropertiesOfSecretsAsync",
                [typeof(CancellationToken)]);

            var asyncPageable = method!.Invoke(_secretClient, [ct])!;
            var names = new List<string>();

            // Use GetAsyncEnumerator pattern
            var getEnumeratorMethod = asyncPageable.GetType().GetMethod("GetAsyncEnumerator",
                [typeof(CancellationToken)]);
            var enumerator = getEnumeratorMethod!.Invoke(asyncPageable, [ct])!;

            var moveNextMethod = enumerator.GetType().GetMethod("MoveNextAsync");
            var currentProp = enumerator.GetType().GetProperty("Current");

            while (true)
            {
                var moveNextTask = (ValueTask<bool>)moveNextMethod!.Invoke(enumerator, [])!;
                if (!await moveNextTask)
                    break;

                var current = currentProp!.GetValue(enumerator);
                var nameProp = current!.GetType().GetProperty("Name");
                var name = nameProp!.GetValue(current)?.ToString();
                if (name is not null)
                    names.Add(name);
            }

            return names;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list secrets from Azure Key Vault");
            var section = _configuration.GetSection("Secrets");
            return section.GetChildren().Select(c => c.Key).ToList();
        }
    }

    private async Task CheckAzureHealthAsync(CancellationToken ct)
    {
        var clientType = _secretClient!.GetType();
        var method = clientType.GetMethod("GetPropertiesOfSecretsAsync",
            [typeof(CancellationToken)]);

        var asyncPageable = method!.Invoke(_secretClient, [ct])!;
        var getEnumeratorMethod = asyncPageable.GetType().GetMethod("GetAsyncEnumerator",
            [typeof(CancellationToken)]);
        var enumerator = getEnumeratorMethod!.Invoke(asyncPageable, [ct])!;

        var moveNextMethod = enumerator.GetType().GetMethod("MoveNextAsync");
        // Just attempt one iteration to verify connectivity
        var moveNextTask = (ValueTask<bool>)moveNextMethod!.Invoke(enumerator, [])!;
        await moveNextTask;
    }

    // ─── Key Wrap / Unwrap (KEK → DEK envelope encryption) ───

    public async Task<WrappedKeyResult> WrapKeyAsync(byte[] keyToWrap, string keyName, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(keyToWrap);
        if (keyToWrap.Length == 0) throw new ArgumentException("Key to wrap cannot be empty", nameof(keyToWrap));

        if (_isEnabled && _cryptoClient is not null)
        {
            return await WrapKeyWithAzureAsync(keyToWrap, keyName, ct);
        }

        // Local AES-GCM envelope wrap using KEK derived from config
        return WrapKeyLocal(keyToWrap, keyName);
    }

    public async Task<byte[]> UnwrapKeyAsync(string wrappedKeyBase64, string ivBase64, string keyName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wrappedKeyBase64);

        if (_isEnabled && _cryptoClient is not null)
        {
            return await UnwrapKeyWithAzureAsync(wrappedKeyBase64, keyName, ct);
        }

        return UnwrapKeyLocal(wrappedKeyBase64, ivBase64, keyName);
    }

    public async Task<EncryptedPayload> EncryptAsync(byte[] plaintext, KeyPurpose purpose, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var dek = await GetOrCreateDekAsync(purpose, ct);
        var iv = RandomNumberGenerator.GetBytes(IvLength);

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagLength];

        using var aes = new AesGcm(dek, TagLength);
        aes.Encrypt(iv, plaintext, ciphertext, tag);

        // Combine ciphertext + tag
        var combined = new byte[ciphertext.Length + tag.Length];
        Buffer.BlockCopy(ciphertext, 0, combined, 0, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, combined, ciphertext.Length, tag.Length);

        return new EncryptedPayload(
            Convert.ToBase64String(combined),
            Convert.ToBase64String(iv),
            purpose,
            "AES-256-GCM");
    }

    public async Task<byte[]> DecryptAsync(string ciphertextBase64, string ivBase64, KeyPurpose purpose, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ciphertextBase64);
        ArgumentException.ThrowIfNullOrWhiteSpace(ivBase64);

        // Decrypt must use the SAME DEK from encrypt — never auto-create
        var dekName = $"dek-{purpose.ToString().ToLowerInvariant()}";
        var dekBase64 = await GetSecretAsync(dekName, ct);
        if (string.IsNullOrEmpty(dekBase64))
            throw new InvalidOperationException($"DEK for purpose '{purpose}' not found. Cannot decrypt without the original key.");

        var dek = Convert.FromBase64String(dekBase64);
        var iv = Convert.FromBase64String(ivBase64);
        var combined = Convert.FromBase64String(ciphertextBase64);

        if (combined.Length < TagLength)
            throw new ArgumentException("Ciphertext is too short to contain a valid AES-GCM tag");

        var ciphertext = combined.AsSpan(0, combined.Length - TagLength).ToArray();
        var tag = combined.AsSpan(combined.Length - TagLength).ToArray();
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(dek, TagLength);
        aes.Decrypt(iv, ciphertext, tag, plaintext);

        return plaintext;
    }

    /// <summary>Get DEK from vault, auto-create if it doesn't exist yet</summary>
    private async Task<byte[]> GetOrCreateDekAsync(KeyPurpose purpose, CancellationToken ct)
    {
        var dekName = $"dek-{purpose.ToString().ToLowerInvariant()}";
        var dekBase64 = await GetSecretAsync(dekName, ct);

        if (string.IsNullOrEmpty(dekBase64))
        {
            _logger.LogInformation("DEK '{DekName}' not found — auto-generating", dekName);
            var newDek = RandomNumberGenerator.GetBytes(KeyLength);
            dekBase64 = Convert.ToBase64String(newDek);
            await SetSecretAsync(dekName, dekBase64, ct);
        }

        return Convert.FromBase64String(dekBase64);
    }

    // ─── Auto-Unseal ───

    public async Task<AutoUnsealStatus> GetAutoUnsealStatusAsync(CancellationToken ct = default)
    {
        try
        {
            var wrappedKey = await GetSecretAsync(AutoUnsealWrappedKeySecretName, ct);
            if (string.IsNullOrEmpty(wrappedKey))
                return new AutoUnsealStatus(false, null, null, null, null);

            var keyName = await GetSecretAsync(AutoUnsealKeyNameSecretName, ct);
            var configuredAtStr = await GetSecretAsync(AutoUnsealConfiguredAtSecretName, ct);
            var configuredAt = DateTime.TryParse(configuredAtStr, out var dt) ? dt : (DateTime?)null;

            return new AutoUnsealStatus(
                true,
                _vaultUrl,
                keyName,
                "RSA-OAEP-256",
                configuredAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve auto-unseal status");
            return new AutoUnsealStatus(false, null, null, null, null);
        }
    }

    public async Task<bool> ConfigureAutoUnsealAsync(string masterPassword, string azureKeyName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(masterPassword);
        ArgumentException.ThrowIfNullOrWhiteSpace(azureKeyName);

        var masterBytes = System.Text.Encoding.UTF8.GetBytes(masterPassword);

        if (_isEnabled && _cryptoClient is not null)
        {
            // Wrap master password with Azure RSA key
            var wrapResult = await WrapKeyWithAzureAsync(masterBytes, azureKeyName, ct);
            await SetSecretAsync(AutoUnsealWrappedKeySecretName, wrapResult.WrappedKeyBase64, ct);
            await SetSecretAsync(AutoUnsealIvSecretName, wrapResult.IvBase64, ct);
        }
        else
        {
            // Local mode: wrap with local KEK
            var wrapResult = WrapKeyLocal(masterBytes, azureKeyName);
            await SetSecretAsync(AutoUnsealWrappedKeySecretName, wrapResult.WrappedKeyBase64, ct);
            await SetSecretAsync(AutoUnsealIvSecretName, wrapResult.IvBase64, ct);
        }

        await SetSecretAsync(AutoUnsealKeyNameSecretName, azureKeyName, ct);
        await SetSecretAsync(AutoUnsealConfiguredAtSecretName, DateTime.UtcNow.ToString("O"), ct);

        _logger.LogInformation("Auto-unseal configured with key {KeyName}", azureKeyName);
        return true;
    }

    public async Task<bool> AutoUnsealAsync(CancellationToken ct = default)
    {
        try
        {
            var wrappedKeyBase64 = await GetSecretAsync(AutoUnsealWrappedKeySecretName, ct);
            var ivBase64 = await GetSecretAsync(AutoUnsealIvSecretName, ct);
            var keyName = await GetSecretAsync(AutoUnsealKeyNameSecretName, ct);

            if (string.IsNullOrEmpty(wrappedKeyBase64) || string.IsNullOrEmpty(keyName))
            {
                _logger.LogWarning("Auto-unseal not configured");
                return false;
            }

            var masterBytes = await UnwrapKeyAsync(wrappedKeyBase64, ivBase64 ?? "", keyName, ct);
            var masterPassword = System.Text.Encoding.UTF8.GetString(masterBytes);

            _logger.LogInformation("Vault auto-unsealed successfully using key {KeyName}", keyName);
            // The master password can now be used to derive KEK and decrypt DEKs
            return !string.IsNullOrEmpty(masterPassword);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto-unseal failed");
            return false;
        }
    }

    // ─── Azure CryptographyClient Wrap / Unwrap ───

    private async Task<WrappedKeyResult> WrapKeyWithAzureAsync(byte[] keyToWrap, string keyName, CancellationToken ct)
    {
        try
        {
            var clientType = _cryptoClient!.GetType();
            var keysAssembly = clientType.Assembly;

            // Use WrapKeyAsync(KeyWrapAlgorithm.RsaOaep256, keyToWrap, ct)
            var algorithmType = keysAssembly.GetType("Azure.Security.KeyVault.Keys.Cryptography.KeyWrapAlgorithm");
            if (algorithmType is null)
            {
                // Try alternate namespace
                algorithmType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return []; } })
                    .FirstOrDefault(t => t.FullName == "Azure.Security.KeyVault.Keys.Cryptography.KeyWrapAlgorithm");
            }

            if (algorithmType is not null)
            {
                var rsaOaep256 = algorithmType.GetProperty("RsaOaep256")?.GetValue(null)
                    ?? algorithmType.GetField("RsaOaep256")?.GetValue(null);

                if (rsaOaep256 is not null)
                {
                    var method = clientType.GetMethod("WrapKeyAsync",
                        [algorithmType, typeof(byte[]), typeof(CancellationToken)]);

                    if (method is not null)
                    {
                        var task = (Task)method.Invoke(_cryptoClient, [rsaOaep256, keyToWrap, ct])!;
                        await task;

                        var resultProp = task.GetType().GetProperty("Result");
                        var response = resultProp!.GetValue(task);
                        var valueProp = response!.GetType().GetProperty("Value");
                        var wrapResult = valueProp!.GetValue(response);

                        var encKeyProp = wrapResult!.GetType().GetProperty("EncryptedKey");
                        var encKey = (byte[])encKeyProp!.GetValue(wrapResult)!;

                        return new WrappedKeyResult(
                            Convert.ToBase64String(encKey),
                            "", // RSA wrap doesn't use IV
                            "RSA-OAEP-256",
                            keyName,
                            1);
                    }
                }
            }

            _logger.LogWarning("Azure CryptographyClient WrapKey not available — falling back to local wrap");
            return WrapKeyLocal(keyToWrap, keyName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Key Vault WrapKey failed — falling back to local wrap");
            return WrapKeyLocal(keyToWrap, keyName);
        }
    }

    private async Task<byte[]> UnwrapKeyWithAzureAsync(string wrappedKeyBase64, string keyName, CancellationToken ct)
    {
        try
        {
            var clientType = _cryptoClient!.GetType();
            var keysAssembly = clientType.Assembly;

            var algorithmType = keysAssembly.GetType("Azure.Security.KeyVault.Keys.Cryptography.KeyWrapAlgorithm");
            if (algorithmType is null)
            {
                algorithmType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return []; } })
                    .FirstOrDefault(t => t.FullName == "Azure.Security.KeyVault.Keys.Cryptography.KeyWrapAlgorithm");
            }

            if (algorithmType is not null)
            {
                var rsaOaep256 = algorithmType.GetProperty("RsaOaep256")?.GetValue(null)
                    ?? algorithmType.GetField("RsaOaep256")?.GetValue(null);

                if (rsaOaep256 is not null)
                {
                    var wrappedKey = Convert.FromBase64String(wrappedKeyBase64);
                    var method = clientType.GetMethod("UnwrapKeyAsync",
                        [algorithmType, typeof(byte[]), typeof(CancellationToken)]);

                    if (method is not null)
                    {
                        var task = (Task)method.Invoke(_cryptoClient, [rsaOaep256, wrappedKey, ct])!;
                        await task;

                        var resultProp = task.GetType().GetProperty("Result");
                        var response = resultProp!.GetValue(task);
                        var valueProp = response!.GetType().GetProperty("Value");
                        var unwrapResult = valueProp!.GetValue(response);

                        var keyProp = unwrapResult!.GetType().GetProperty("Key");
                        return (byte[])keyProp!.GetValue(unwrapResult)!;
                    }
                }
            }

            _logger.LogWarning("Azure CryptographyClient UnwrapKey not available — falling back to local unwrap");
            return UnwrapKeyLocal(wrappedKeyBase64, "", keyName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure Key Vault UnwrapKey failed — falling back to local unwrap");
            return UnwrapKeyLocal(wrappedKeyBase64, "", keyName);
        }
    }

    // ─── Local AES-GCM Key Wrapping (KEK derived from config) ───

    private WrappedKeyResult WrapKeyLocal(byte[] keyToWrap, string keyName)
    {
        var kek = DeriveLocalKek(keyName);
        var iv = RandomNumberGenerator.GetBytes(IvLength);
        var ciphertext = new byte[keyToWrap.Length];
        var tag = new byte[TagLength];

        using var aes = new AesGcm(kek, TagLength);
        aes.Encrypt(iv, keyToWrap, ciphertext, tag);

        var combined = new byte[ciphertext.Length + tag.Length];
        Buffer.BlockCopy(ciphertext, 0, combined, 0, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, combined, ciphertext.Length, tag.Length);

        return new WrappedKeyResult(
            Convert.ToBase64String(combined),
            Convert.ToBase64String(iv),
            "AES-256-GCM",
            keyName,
            1);
    }

    private byte[] UnwrapKeyLocal(string wrappedKeyBase64, string ivBase64, string keyName)
    {
        var kek = DeriveLocalKek(keyName);
        var iv = Convert.FromBase64String(ivBase64);
        var combined = Convert.FromBase64String(wrappedKeyBase64);

        if (combined.Length < TagLength)
            throw new ArgumentException("Wrapped key data is too short");

        var ciphertext = combined.AsSpan(0, combined.Length - TagLength).ToArray();
        var tag = combined.AsSpan(combined.Length - TagLength).ToArray();
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(kek, TagLength);
        aes.Decrypt(iv, ciphertext, tag, plaintext);

        return plaintext;
    }

    private byte[] DeriveLocalKek(string keyName)
    {
        // Derive KEK from master secret + key name via PBKDF2
        var masterSecret = _configuration["AzureKeyVault:MasterSecret"]
            ?? _configuration["Secrets:MasterSecret"]
            ?? "default-dev-master-secret";

        var salt = System.Text.Encoding.UTF8.GetBytes($"ivf-kek-{keyName}");
        return Rfc2898DeriveBytes.Pbkdf2(
            System.Text.Encoding.UTF8.GetBytes(masterSecret),
            salt,
            Pbkdf2Iterations,
            HashAlgorithmName.SHA256,
            KeyLength);
    }

    // ─── Azure SDK Factory Methods ───

    private static object? CreateCryptoClient(IConfiguration configuration)
    {
        try
        {
            var vaultUrl = configuration["AzureKeyVault:VaultUrl"];
            var keyName = configuration["AzureKeyVault:KeyName"] ?? "ivf-master-key";
            if (string.IsNullOrWhiteSpace(vaultUrl))
                return null;

            var keysAssembly = System.Reflection.Assembly.Load("Azure.Security.KeyVault.Keys");
            var cryptoClientType = keysAssembly.GetType("Azure.Security.KeyVault.Keys.Cryptography.CryptographyClient");
            if (cryptoClientType is null) return null;

            var credential = CreateAzureCredential(configuration);
            var keyUri = new Uri($"{vaultUrl.TrimEnd('/')}/keys/{keyName}");
            var client = Activator.CreateInstance(cryptoClientType, keyUri, credential);

            return client;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
