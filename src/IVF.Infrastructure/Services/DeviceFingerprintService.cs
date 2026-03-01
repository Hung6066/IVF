using System.Security.Cryptography;
using System.Text;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

/// <summary>
/// Device fingerprinting and trust management inspired by:
/// - Google BeyondCorp: Device inventory and trust evaluation
/// - Microsoft Endpoint Manager: Device compliance and posture assessment
/// - AWS Systems Manager: Device registration and health monitoring
///
/// Generates a cryptographic device fingerprint from browser/client signals.
/// Does NOT store PII — only a one-way hash of device characteristics.
/// </summary>
public sealed class DeviceFingerprintService : IDeviceFingerprintService
{
    private readonly IvfDbContext _context;
    private readonly ILogger<DeviceFingerprintService> _logger;

    public DeviceFingerprintService(IvfDbContext context, ILogger<DeviceFingerprintService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public string GenerateFingerprint(DeviceSignals signals)
    {
        // Combine device signals into a deterministic string
        // NOTE: IP is intentionally excluded — fingerprint should be device-specific, not location-specific
        var components = new StringBuilder();
        components.Append(NormalizeUserAgent(signals.UserAgent));
        components.Append('|');
        components.Append(signals.AcceptLanguage ?? "unknown");
        components.Append('|');
        components.Append(signals.Platform ?? "unknown");
        components.Append('|');
        components.Append(signals.Timezone ?? "unknown");
        components.Append('|');
        components.Append(signals.ScreenResolution ?? "unknown");

        // SHA-256 hash to create a fixed-length, non-reversible fingerprint
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(components.ToString()));
        return Convert.ToHexStringLower(hash);
    }

    public bool ValidateFingerprint(string existingFingerprint, DeviceSignals currentSignals)
    {
        var currentFingerprint = GenerateFingerprint(currentSignals);
        return string.Equals(existingFingerprint, currentFingerprint, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<string> RegisterDeviceAsync(Guid userId, DeviceSignals signals, CancellationToken ct = default)
    {
        var fingerprint = GenerateFingerprint(signals);

        var existing = await _context.DeviceRisks
            .FirstOrDefaultAsync(d => d.UserId == userId.ToString() && d.DeviceId == fingerprint, ct);

        if (existing is not null)
        {
            // Device already registered — update last seen
            existing.UpdateRisk(existing.RiskLevel, existing.RiskScore, existing.Factors);
            await _context.SaveChangesAsync(ct);
            return fingerprint;
        }

        // Register new device
        var deviceRisk = DeviceRisk.Create(
            userId: userId.ToString(),
            deviceId: fingerprint,
            riskLevel: RiskLevel.Medium, // New devices start at medium risk
            riskScore: 25,
            factors: "New device registration",
            isTrusted: false,
            ipAddress: signals.IpAddress,
            country: null,
            userAgent: signals.UserAgent);

        await _context.DeviceRisks.AddAsync(deviceRisk, ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("New device registered for user {UserId}: fingerprint={Fingerprint}", userId, fingerprint[..16]);
        return fingerprint;
    }

    public async Task<DeviceTrustResult> CheckDeviceTrustAsync(Guid userId, string fingerprint, CancellationToken ct = default)
    {
        var device = await _context.DeviceRisks
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.UserId == userId.ToString() && d.DeviceId == fingerprint, ct);

        if (device is null)
        {
            return new DeviceTrustResult(
                IsKnown: false,
                IsTrusted: false,
                TrustLevel: DeviceTrustLevel.Unknown,
                FirstSeen: null,
                LastSeen: null,
                AccessCount: 0);
        }

        var trustLevel = device.IsTrusted
            ? DeviceTrustLevel.Trusted
            : device.RiskLevel switch
            {
                RiskLevel.Low => DeviceTrustLevel.PartiallyTrusted,
                RiskLevel.Medium => DeviceTrustLevel.PartiallyTrusted,
                _ => DeviceTrustLevel.Untrusted
            };

        return new DeviceTrustResult(
            IsKnown: true,
            IsTrusted: device.IsTrusted,
            TrustLevel: trustLevel,
            FirstSeen: device.CreatedAt,
            LastSeen: device.UpdatedAt ?? device.CreatedAt,
            AccessCount: 0 // Could track access count if needed
        );
    }

    // ─── Private Helpers ───

    /// <summary>
    /// Normalizes User-Agent to reduce fingerprint volatility from version updates.
    /// Extracts browser family + OS family, ignoring minor version numbers.
    /// </summary>
    private static string NormalizeUserAgent(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
            return "unknown";

        // Extract browser family (Chrome, Firefox, Safari, Edge)
        var browser = "other";
        if (userAgent.Contains("Chrome", StringComparison.OrdinalIgnoreCase) &&
            !userAgent.Contains("Edge", StringComparison.OrdinalIgnoreCase))
            browser = "chrome";
        else if (userAgent.Contains("Firefox", StringComparison.OrdinalIgnoreCase))
            browser = "firefox";
        else if (userAgent.Contains("Safari", StringComparison.OrdinalIgnoreCase) &&
                 !userAgent.Contains("Chrome", StringComparison.OrdinalIgnoreCase))
            browser = "safari";
        else if (userAgent.Contains("Edg", StringComparison.OrdinalIgnoreCase))
            browser = "edge";

        // Extract OS family
        var os = "other";
        if (userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase))
            os = "windows";
        else if (userAgent.Contains("Mac", StringComparison.OrdinalIgnoreCase))
            os = "macos";
        else if (userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase))
            os = "linux";
        else if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase))
            os = "android";
        else if (userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase) ||
                 userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase))
            os = "ios";

        return $"{browser}/{os}";
    }
}
