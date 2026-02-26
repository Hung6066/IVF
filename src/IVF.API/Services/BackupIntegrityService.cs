using System.Security.Cryptography;

namespace IVF.API.Services;

/// <summary>
/// Provides SHA-256 checksum computation and verification for backup files.
/// Stores checksums as .sha256 sidecar files alongside the backup.
/// </summary>
public sealed class BackupIntegrityService(ILogger<BackupIntegrityService> logger)
{
    private const string ChecksumExtension = ".sha256";

    /// <summary>
    /// Compute SHA-256 checksum of a file and save alongside it as a .sha256 sidecar.
    /// Returns the hex-encoded checksum string.
    /// </summary>
    public async Task<string> ComputeAndStoreChecksumAsync(string filePath, CancellationToken ct = default)
    {
        var checksum = await ComputeChecksumAsync(filePath, ct);
        var checksumPath = filePath + ChecksumExtension;
        var fileName = Path.GetFileName(filePath);
        // BSD-style checksum line: SHA256 (filename) = hex
        await File.WriteAllTextAsync(checksumPath, $"SHA256 ({fileName}) = {checksum}", ct);
        logger.LogDebug("Checksum stored: {File} → {Checksum}", fileName, checksum);
        return checksum;
    }

    /// <summary>
    /// Compute SHA-256 of a file without storing. Returns hex-encoded lowercase string.
    /// </summary>
    public static async Task<string> ComputeChecksumAsync(string filePath, CancellationToken ct = default)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        var hashBytes = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexStringLower(hashBytes);
    }

    /// <summary>
    /// Verify a file against its stored .sha256 sidecar checksum.
    /// Returns (isValid, expectedChecksum, actualChecksum).
    /// </summary>
    public async Task<ChecksumResult> VerifyChecksumAsync(string filePath, CancellationToken ct = default)
    {
        var checksumPath = filePath + ChecksumExtension;
        if (!File.Exists(checksumPath))
            return new ChecksumResult(false, null, null, "Checksum file not found");

        var content = await File.ReadAllTextAsync(checksumPath, ct);
        var expected = ParseChecksumFile(content);
        if (expected == null)
            return new ChecksumResult(false, null, null, "Invalid checksum file format");

        var actual = await ComputeChecksumAsync(filePath, ct);
        var isValid = string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);

        return new ChecksumResult(isValid, expected, actual,
            isValid ? null : "Checksum mismatch — file may be corrupted");
    }

    /// <summary>
    /// Load the stored checksum for a backup file (reads sidecar), or null if not available.
    /// </summary>
    public static string? LoadStoredChecksum(string filePath)
    {
        var checksumPath = filePath + ChecksumExtension;
        if (!File.Exists(checksumPath)) return null;
        try
        {
            var content = File.ReadAllText(checksumPath);
            return ParseChecksumFile(content);
        }
        catch { return null; }
    }

    private static string? ParseChecksumFile(string content)
    {
        // Parse "SHA256 (filename) = hex" or just raw hex
        content = content.Trim();
        var eqIdx = content.LastIndexOf('=');
        if (eqIdx >= 0)
            content = content[(eqIdx + 1)..].Trim();

        // Validate it looks like a hex SHA-256 (64 chars)
        return content.Length == 64 && content.All(IsHexChar) ? content : null;

        static bool IsHexChar(char c) => c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');
    }
}

public record ChecksumResult(bool IsValid, string? ExpectedChecksum, string? ActualChecksum, string? Error);
