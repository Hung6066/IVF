using IVF.Domain.Common;

namespace IVF.Domain.Entities;

public class AiModelVersion : BaseEntity
{
    public string AiSystemName { get; private set; } = string.Empty;
    public string ModelVersion { get; private set; } = string.Empty;
    public string? PreviousVersion { get; private set; }

    // Configuration snapshot
    public string ConfigurationJson { get; private set; } = "{}";
    public string ThresholdsJson { get; private set; } = "{}";
    public string FeatureSetJson { get; private set; } = "{}";

    // Performance metrics at deployment
    public double? Accuracy { get; private set; }
    public double? Precision { get; private set; }
    public double? Recall { get; private set; }
    public double? F1Score { get; private set; }
    public double? Fpr { get; private set; }
    public double? Fnr { get; private set; }

    // Lifecycle
    public string Status { get; private set; } = ModelVersionStatus.Draft;
    public string ChangeDescription { get; private set; } = string.Empty;
    public string? ChangeReason { get; private set; }
    public string? ApprovedBy { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public DateTime? DeployedAt { get; private set; }
    public DateTime? RetiredAt { get; private set; }
    public string? RollbackReason { get; private set; }

    // Bias test reference
    public bool BiasTestPassed { get; private set; }
    public Guid? BiasTestResultId { get; private set; }

    // Git reference
    public string? GitCommitHash { get; private set; }
    public string? GitTag { get; private set; }

    private AiModelVersion() { }

    public static AiModelVersion Create(
        string aiSystemName,
        string modelVersion,
        string changeDescription,
        string configurationJson,
        string thresholdsJson,
        string? previousVersion = null)
    {
        return new AiModelVersion
        {
            AiSystemName = aiSystemName,
            ModelVersion = modelVersion,
            PreviousVersion = previousVersion,
            ChangeDescription = changeDescription,
            ConfigurationJson = configurationJson,
            ThresholdsJson = thresholdsJson,
            Status = ModelVersionStatus.Draft
        };
    }

    public void SetPerformanceMetrics(double accuracy, double precision, double recall, double f1, double fpr, double fnr)
    {
        Accuracy = accuracy;
        Precision = precision;
        Recall = recall;
        F1Score = f1;
        Fpr = fpr;
        Fnr = fnr;
        SetUpdated();
    }

    public void SetFeatureSet(string featureSetJson)
    {
        FeatureSetJson = featureSetJson;
        SetUpdated();
    }

    public void SetGitReference(string commitHash, string? tag = null)
    {
        GitCommitHash = commitHash;
        GitTag = tag;
        SetUpdated();
    }

    public void LinkBiasTest(Guid biasTestResultId, bool passed)
    {
        BiasTestResultId = biasTestResultId;
        BiasTestPassed = passed;
        SetUpdated();
    }

    public void Submit()
    {
        if (Status != ModelVersionStatus.Draft)
            throw new InvalidOperationException("Only draft versions can be submitted for review.");
        Status = ModelVersionStatus.PendingReview;
        SetUpdated();
    }

    public void Approve(string approvedBy)
    {
        if (Status != ModelVersionStatus.PendingReview)
            throw new InvalidOperationException("Only versions pending review can be approved.");
        Status = ModelVersionStatus.Approved;
        ApprovedBy = approvedBy;
        ApprovedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void Reject(string reason)
    {
        if (Status != ModelVersionStatus.PendingReview)
            throw new InvalidOperationException("Only versions pending review can be rejected.");
        Status = ModelVersionStatus.Rejected;
        ChangeReason = reason;
        SetUpdated();
    }

    public void Deploy()
    {
        if (Status != ModelVersionStatus.Approved)
            throw new InvalidOperationException("Only approved versions can be deployed.");
        Status = ModelVersionStatus.Deployed;
        DeployedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void Rollback(string reason)
    {
        if (Status != ModelVersionStatus.Deployed)
            throw new InvalidOperationException("Only deployed versions can be rolled back.");
        Status = ModelVersionStatus.RolledBack;
        RollbackReason = reason;
        RetiredAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void Retire()
    {
        if (Status != ModelVersionStatus.Deployed)
            throw new InvalidOperationException("Only deployed versions can be retired.");
        Status = ModelVersionStatus.Retired;
        RetiredAt = DateTime.UtcNow;
        SetUpdated();
    }
}

public static class ModelVersionStatus
{
    public const string Draft = "Draft";
    public const string PendingReview = "PendingReview";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Deployed = "Deployed";
    public const string RolledBack = "RolledBack";
    public const string Retired = "Retired";
}
