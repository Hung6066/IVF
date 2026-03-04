using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Tracks security awareness training completion for compliance.
/// Required by: SOC 2 CC1.4, ISO 27001 A.6.3, HIPAA §164.308(a)(5), HITRUST 02.e.
/// </summary>
public class ComplianceTraining : BaseEntity
{
    public Guid UserId { get; private set; }
    public string TrainingType { get; private set; } = string.Empty; // security_awareness, hipaa_privacy, gdpr_basics, phishing_simulation, incident_response, data_handling
    public string TrainingName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTime AssignedAt { get; private set; }
    public DateTime DueDate { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public bool IsCompleted { get; private set; }
    public int? ScorePercent { get; private set; } // Quiz/test score (0-100)
    public bool IsPassed { get; private set; }
    public int PassThreshold { get; private set; } = 80; // Minimum score to pass
    public string? CertificateId { get; private set; } // External LMS certificate reference
    public DateTime? ExpiresAt { get; private set; } // Annual renewal
    public string? CompletionEvidence { get; private set; } // JSON: { source, timestamp, verifier }
    public Guid? AssignedBy { get; private set; }

    private ComplianceTraining() { }

    public static ComplianceTraining Assign(
        Guid userId,
        string trainingType,
        string trainingName,
        DateTime dueDate,
        Guid? assignedBy = null,
        string? description = null,
        int passThreshold = 80,
        DateTime? expiresAt = null)
    {
        return new ComplianceTraining
        {
            UserId = userId,
            TrainingType = trainingType,
            TrainingName = trainingName,
            Description = description,
            AssignedAt = DateTime.UtcNow,
            DueDate = dueDate,
            PassThreshold = passThreshold,
            AssignedBy = assignedBy,
            ExpiresAt = expiresAt
        };
    }

    public void Complete(int scorePercent, string? certificateId = null, string? evidence = null)
    {
        CompletedAt = DateTime.UtcNow;
        IsCompleted = true;
        ScorePercent = scorePercent;
        IsPassed = scorePercent >= PassThreshold;
        CertificateId = certificateId;
        CompletionEvidence = evidence;
        SetUpdated();
    }

    public void SetExpiry(DateTime expiresAt)
    {
        ExpiresAt = expiresAt;
        SetUpdated();
    }

    public bool IsOverdue() => !IsCompleted && DateTime.UtcNow > DueDate;

    public bool IsExpired() => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;

    public bool NeedsRenewal() => IsCompleted && IsExpired();
}

/// <summary>
/// Standard training types for healthcare compliance.
/// </summary>
public static class TrainingTypes
{
    public const string SecurityAwareness = "security_awareness";
    public const string HipaaPrivacy = "hipaa_privacy";
    public const string GdprBasics = "gdpr_basics";
    public const string PhishingSimulation = "phishing_simulation";
    public const string IncidentResponse = "incident_response";
    public const string DataHandling = "data_handling";
    public const string PasswordSecurity = "password_security";
    public const string SocialEngineering = "social_engineering";
    public const string MedicalRecordPrivacy = "medical_record_privacy";
    public const string BiometricDataHandling = "biometric_data_handling";
}
