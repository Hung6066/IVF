using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Record of Processing Activities (ROPA) per GDPR Article 30.
/// Documents all personal data processing activities for regulatory compliance.
/// </summary>
public class ProcessingActivity : BaseEntity
{
    public string ActivityName { get; private set; } = null!;
    public string Purpose { get; private set; } = null!;
    public string LegalBasis { get; private set; } = null!; // Consent, Contract, LegalObligation, VitalInterest, PublicTask, LegitimateInterest
    public string DataCategories { get; private set; } = null!; // JSON array: ["PHI", "PII", "biometric", "genetic"]
    public string DataSubjectCategories { get; private set; } = null!; // JSON array: ["patients", "staff", "visitors"]
    public string? ProcessingDescription { get; private set; }
    public string? Recipients { get; private set; } // JSON array of third-party recipients
    public string? ThirdCountryTransfers { get; private set; } // JSON: countries + safeguards
    public string RetentionPeriod { get; private set; } = null!; // e.g., "7 years", "Until consent withdrawal"
    public string? SecurityMeasures { get; private set; } // JSON array of controls
    public bool RequiresDpia { get; private set; }
    public bool DpiaCompleted { get; private set; }
    public DateTime? DpiaCompletedAt { get; private set; }
    public string? DpiaReference { get; private set; }
    public string DataControllerName { get; private set; } = null!;
    public string? DataControllerContact { get; private set; }
    public string? DpoName { get; private set; }
    public string? DpoContact { get; private set; }
    public string? JointControllerDetails { get; private set; }
    public string? ProcessorName { get; private set; }
    public string? ProcessorContract { get; private set; }
    public bool IsAutomatedDecisionMaking { get; private set; }
    public string? AutomatedDecisionDetails { get; private set; }
    public string Status { get; private set; } = null!; // Draft, Active, UnderReview, Archived
    public DateTime? LastReviewedAt { get; private set; }
    public DateTime? NextReviewDueAt { get; private set; }

    private ProcessingActivity() { }

    public static ProcessingActivity Create(
        string activityName,
        string purpose,
        string legalBasis,
        string dataCategories,
        string dataSubjectCategories,
        string retentionPeriod,
        string dataControllerName,
        bool requiresDpia = false,
        bool isAutomatedDecisionMaking = false)
    {
        return new ProcessingActivity
        {
            ActivityName = activityName,
            Purpose = purpose,
            LegalBasis = legalBasis,
            DataCategories = dataCategories,
            DataSubjectCategories = dataSubjectCategories,
            RetentionPeriod = retentionPeriod,
            DataControllerName = dataControllerName,
            RequiresDpia = requiresDpia,
            IsAutomatedDecisionMaking = isAutomatedDecisionMaking,
            Status = ProcessingActivityStatus.Draft
        };
    }

    public void Activate()
    {
        Status = ProcessingActivityStatus.Active;
        SetUpdated();
    }

    public void SubmitForReview()
    {
        Status = ProcessingActivityStatus.UnderReview;
        SetUpdated();
    }

    public void MarkReviewed(DateTime? nextReviewDue = null)
    {
        LastReviewedAt = DateTime.UtcNow;
        NextReviewDueAt = nextReviewDue ?? DateTime.UtcNow.AddYears(1);
        Status = ProcessingActivityStatus.Active;
        SetUpdated();
    }

    public void Archive()
    {
        Status = ProcessingActivityStatus.Archived;
        SetUpdated();
    }

    public void CompleteDpia(string reference)
    {
        DpiaCompleted = true;
        DpiaCompletedAt = DateTime.UtcNow;
        DpiaReference = reference;
        SetUpdated();
    }

    public void SetSecurityMeasures(string securityMeasuresJson)
    {
        SecurityMeasures = securityMeasuresJson;
        SetUpdated();
    }

    public void SetRecipients(string recipientsJson)
    {
        Recipients = recipientsJson;
        SetUpdated();
    }

    public void SetTransferDetails(string transferJson)
    {
        ThirdCountryTransfers = transferJson;
        SetUpdated();
    }

    public bool IsOverdueForReview() =>
        NextReviewDueAt.HasValue && NextReviewDueAt.Value < DateTime.UtcNow;
}

public static class ProcessingActivityStatus
{
    public const string Draft = "Draft";
    public const string Active = "Active";
    public const string UnderReview = "UnderReview";
    public const string Archived = "Archived";
}

public static class LegalBasis
{
    public const string Consent = "Consent";
    public const string Contract = "Contract";
    public const string LegalObligation = "LegalObligation";
    public const string VitalInterest = "VitalInterest";
    public const string PublicTask = "PublicTask";
    public const string LegitimateInterest = "LegitimateInterest";
}
