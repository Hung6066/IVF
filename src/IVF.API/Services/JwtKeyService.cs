using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace IVF.API.Services;

/// <summary>
/// Manages RSA key pair for asymmetric JWT signing (RS256).
/// Google/Microsoft standard: asymmetric keys prevent token forgery even if
/// the verification key is exposed. Only the private key can sign tokens.
/// </summary>
public sealed class JwtKeyService
{
    private readonly RsaSecurityKey _signingKey;
    private readonly RsaSecurityKey _validationKey;
    private readonly ILogger<JwtKeyService> _logger;

    /// <summary>
    /// Singleton instance, set after DI registration.
    /// Used by static helper methods that cannot inject services.
    /// </summary>
    public static JwtKeyService? Instance { get; private set; }

    public SigningCredentials SigningCredentials { get; }
    public SecurityKey ValidationKey => _validationKey;

    public JwtKeyService(IConfiguration config, ILogger<JwtKeyService> logger)
    {
        _logger = logger;
        Instance = this;
        var keysPath = config["JwtSettings:RsaKeysPath"]
            ?? Path.Combine(AppContext.BaseDirectory, "keys", "jwt");
        Directory.CreateDirectory(keysPath);

        var privateKeyPath = Path.Combine(keysPath, "jwt-private.pem");
        var rsa = RSA.Create(3072); // NIST recommends 3072-bit for post-2030

        if (File.Exists(privateKeyPath))
        {
            rsa.ImportFromPem(File.ReadAllText(privateKeyPath));
            _logger.LogInformation("Loaded existing RSA key pair from {Path}", keysPath);
        }
        else
        {
            var privatePem = rsa.ExportRSAPrivateKeyPem();
            File.WriteAllText(privateKeyPath, privatePem);
            // Set restrictive file permissions
            File.SetAttributes(privateKeyPath, FileAttributes.ReadOnly | FileAttributes.Hidden);
            _logger.LogInformation("Generated new 3072-bit RSA key pair at {Path}", keysPath);
        }

        _signingKey = new RsaSecurityKey(rsa) { KeyId = ComputeKeyId(rsa) };

        // Validation key uses only public parameters
        var publicRsa = RSA.Create();
        publicRsa.ImportParameters(rsa.ExportParameters(includePrivateParameters: false));
        _validationKey = new RsaSecurityKey(publicRsa) { KeyId = _signingKey.KeyId };

        SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256)
        {
            CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
        };
    }

    private static string ComputeKeyId(RSA rsa)
    {
        var pubParams = rsa.ExportParameters(false);
        var hash = SHA256.HashData(pubParams.Modulus!);
        return Convert.ToBase64String(hash[..8]);
    }
}
