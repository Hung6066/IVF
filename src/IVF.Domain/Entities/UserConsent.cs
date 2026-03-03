using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Data privacy consent tracking — GDPR/HIPAA compliance.
/// Inspired by Google's consent framework and Facebook's privacy center.
/// Tracks user consent for data processing, marketing, analytics, etc.
/// </summary>
public class UserConsent : BaseEntity
{
    public Guid UserId { get; private set; }
    public string ConsentType { get; private set; } = string.Empty; // data_processing, marketing, analytics, research, third_party
    public bool IsGranted { get; private set; }
    public string? ConsentVersion { get; private set; } // Version of the policy accepted
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTime ConsentedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? RevokedReason { get; private set; }
    public DateTime? ExpiresAt { get; private set; }

    private UserConsent() { }

    public static UserConsent Grant(
        Guid userId,
        string consentType,
        string? consentVersion = null,
        string? ipAddress = null,
        string? userAgent = null,
        DateTime? expiresAt = null)
    {
        return new UserConsent
        {
            UserId = userId,
            ConsentType = consentType,
            IsGranted = true,
            ConsentVersion = consentVersion,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            ConsentedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt
        };
    }

    public void Revoke(string? reason = null)
    {
        IsGranted = false;
        RevokedAt = DateTime.UtcNow;
        RevokedReason = reason;
        SetUpdated();
    }

    public bool IsValid() => IsGranted && !IsDeleted && (ExpiresAt == null || ExpiresAt > DateTime.UtcNow);
}

/// <summary>
/// Standard consent types for healthcare compliance.
/// </summary>
public static class ConsentTypes
{
    public const string DataProcessing = "data_processing";
    public const string MedicalRecords = "medical_records";
    public const string Marketing = "marketing";
    public const string Analytics = "analytics";
    public const string Research = "research";
    public const string ThirdPartySharing = "third_party";
    public const string BiometricData = "biometric_data";
    public const string CookieConsent = "cookies";
}
