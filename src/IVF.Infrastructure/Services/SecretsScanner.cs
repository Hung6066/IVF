using System.Text.RegularExpressions;
using IVF.Application.Common.Interfaces;

namespace IVF.Infrastructure.Services;

/// <summary>
/// Secrets scanner — detects leaked credentials in log entries and security event data.
/// Scans for JWT tokens, API keys, passwords in URLs, connection strings, private keys.
/// Inspired by GitHub Secret Scanning + AWS Macie.
/// </summary>
public sealed partial class SecretsScanner : ISecretsScanner
{
    public SecretsScanResult Scan(string? content)
    {
        if (string.IsNullOrEmpty(content))
            return new SecretsScanResult(false, []);

        var detected = new List<string>();

        // JWT token pattern (three base64url segments separated by dots)
        if (JwtPattern().IsMatch(content))
            detected.Add("jwt_token");

        // API key patterns (hex or base64, 32+ chars)
        if (ApiKeyPattern().IsMatch(content))
            detected.Add("api_key");

        // Connection string patterns
        if (ConnectionStringPattern().IsMatch(content))
            detected.Add("connection_string");

        // Password in URL
        if (PasswordInUrlPattern().IsMatch(content))
            detected.Add("password_in_url");

        // Private key markers
        if (PrivateKeyPattern().IsMatch(content))
            detected.Add("private_key");

        // Bearer token in plain text
        if (BearerTokenPattern().IsMatch(content))
            detected.Add("bearer_token");

        return new SecretsScanResult(detected.Count > 0, detected);
    }

    [GeneratedRegex(@"eyJ[A-Za-z0-9_-]{10,}\.eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex JwtPattern();

    [GeneratedRegex(@"(?:api[_-]?key|apikey|x-api-key)\s*[=:]\s*[""']?[A-Za-z0-9+/=_-]{32,}[""']?", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ApiKeyPattern();

    [GeneratedRegex(@"(?:Server|Host|Data Source)\s*=.*?(?:Password|Pwd)\s*=\s*[^;]+", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ConnectionStringPattern();

    [GeneratedRegex(@"://[^:]+:[^@]+@", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PasswordInUrlPattern();

    [GeneratedRegex(@"-----BEGIN (?:RSA |EC |DSA )?PRIVATE KEY-----", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PrivateKeyPattern();

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9_-]{20,}", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex BearerTokenPattern();
}
