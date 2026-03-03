using System.Security.Cryptography;
using System.Text;
using IVF.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

/// <summary>
/// PII masking for structured logs — masks email, phone, IP, username patterns.
/// Applied to Serilog log output; does NOT affect raw DB records (needed for forensics).
/// Inspired by AWS Macie PII detection + OWASP logging guidelines.
/// </summary>
public static class PiiMasker
{
    /// <summary>
    /// Masks PII patterns in a string for safe logging.
    /// </summary>
    public static string Mask(string? input)
    {
        if (string.IsNullOrEmpty(input)) return input ?? string.Empty;

        var result = input;

        // Mask email: user@domain.com → u***@domain.com
        result = System.Text.RegularExpressions.Regex.Replace(result,
            @"([a-zA-Z0-9._%+-])([a-zA-Z0-9._%+-]+)@([a-zA-Z0-9.-]+\.[a-zA-Z]{2,})",
            m => $"{m.Groups[1].Value}***@{m.Groups[3].Value}",
            System.Text.RegularExpressions.RegexOptions.None,
            TimeSpan.FromSeconds(1));

        // Mask phone: +84123456789 → +84***6789
        result = System.Text.RegularExpressions.Regex.Replace(result,
            @"(\+?\d{2,3})\d{3,6}(\d{4})",
            "$1***$2",
            System.Text.RegularExpressions.RegexOptions.None,
            TimeSpan.FromSeconds(1));

        // Mask IPv4: 192.168.1.100 → 192.168.x.x
        result = System.Text.RegularExpressions.Regex.Replace(result,
            @"(\d{1,3}\.\d{1,3})\.\d{1,3}\.\d{1,3}",
            "$1.x.x",
            System.Text.RegularExpressions.RegexOptions.None,
            TimeSpan.FromSeconds(1));

        return result;
    }
}

/// <summary>
/// Breached password checker using Have I Been Pwned API with k-anonymity.
/// Only the first 5 characters of SHA-1 hash are sent — full password never leaves the server.
/// </summary>
public sealed class BreachedPasswordService(
    IHttpClientFactory httpClientFactory,
    Microsoft.Extensions.Logging.ILogger<BreachedPasswordService> logger) : IBreachedPasswordService
{
    private const string HibpApiUrl = "https://api.pwnedpasswords.com/range/";

    public async Task<BreachedPasswordResult> CheckAsync(string password, CancellationToken ct = default)
    {
        try
        {
            // SHA-1 hash of the password
            var sha1Bytes = SHA1.HashData(Encoding.UTF8.GetBytes(password));
            var sha1Hex = Convert.ToHexStringLower(sha1Bytes);
            var prefix = sha1Hex[..5];
            var suffix = sha1Hex[5..].ToUpperInvariant();

            using var client = httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("User-Agent", "IVF-Security-Check");
            var response = await client.GetStringAsync($"{HibpApiUrl}{prefix}", ct);

            // Response format: "SUFFIX:COUNT\r\n" per line
            foreach (var line in response.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Trim().Split(':');
                if (parts.Length == 2 && parts[0].Equals(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    var count = int.TryParse(parts[1], out var c) ? c : 0;
                    logger.LogWarning("Password found in {Count} data breaches", count);
                    return new BreachedPasswordResult(true, count);
                }
            }

            return new BreachedPasswordResult(false, 0);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check password against breach database");
            // Fail open — don't block login if HIBP is unavailable
            return new BreachedPasswordResult(false, 0);
        }
    }
}
