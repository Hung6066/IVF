using IVF.Domain.Common;

namespace IVF.Domain.Entities;

/// <summary>
/// Tracks AI bias and fairness test results per NIST AI RMF MEASURE 3 and ISO 42001 Annex A.8.
/// Records FPR/FNR disparities across protected groups for each AI/ML system.
/// </summary>
public class AiBiasTestResult : BaseEntity
{
    public string AiSystemName { get; private set; } = null!; // ThreatDetection, BehavioralAnalytics, BiometricMatcher, BotDetection, ContextualAuth
    public string TestType { get; private set; } = null!; // fairness, accuracy, robustness, explainability
    public string ProtectedAttribute { get; private set; } = null!; // geographic_region, device_type, time_zone, user_role, access_pattern
    public string ProtectedGroupValue { get; private set; } = null!; // e.g., "Asia", "mobile", "UTC+7", "Nurse"
    public int SampleSize { get; private set; }
    public int TruePositives { get; private set; }
    public int FalsePositives { get; private set; }
    public int TrueNegatives { get; private set; }
    public int FalseNegatives { get; private set; }
    public decimal FalsePositiveRate { get; private set; } // FP / (FP + TN)
    public decimal FalseNegativeRate { get; private set; } // FN / (FN + TP)
    public decimal Accuracy { get; private set; } // (TP + TN) / Total
    public decimal Precision { get; private set; } // TP / (TP + FP)
    public decimal Recall { get; private set; } // TP / (TP + FN)
    public decimal F1Score { get; private set; }
    public decimal BaselineFpr { get; private set; } // Population-wide FPR
    public decimal BaselineFnr { get; private set; }
    public decimal DisparityRatioFpr { get; private set; } // Group FPR / Baseline FPR (1.0 = no disparity)
    public decimal DisparityRatioFnr { get; private set; }
    public bool PassesFairnessThreshold { get; private set; } // Disparity ratios within 0.8–1.25 (four-fifths rule)
    public decimal FairnessThreshold { get; private set; } = 0.25m; // Max acceptable disparity (25%)
    public string? FeatureImportance { get; private set; } // JSON: SHAP-like feature importance values
    public string? Explanation { get; private set; } // Human-readable explanation of results
    public string? RemediationAction { get; private set; }
    public DateTime TestRunAt { get; private set; }
    public string? TestRunBy { get; private set; }
    public string TestPeriodStart { get; private set; } = null!; // Data window start
    public string TestPeriodEnd { get; private set; } = null!; // Data window end

    private AiBiasTestResult() { }

    public static AiBiasTestResult Create(
        string aiSystemName,
        string testType,
        string protectedAttribute,
        string protectedGroupValue,
        int sampleSize,
        int truePositives,
        int falsePositives,
        int trueNegatives,
        int falseNegatives,
        decimal baselineFpr,
        decimal baselineFnr,
        string testPeriodStart,
        string testPeriodEnd,
        string? testRunBy = null,
        decimal fairnessThreshold = 0.25m)
    {
        var total = truePositives + falsePositives + trueNegatives + falseNegatives;
        var fpr = (falsePositives + trueNegatives) > 0
            ? (decimal)falsePositives / (falsePositives + trueNegatives) : 0;
        var fnr = (falseNegatives + truePositives) > 0
            ? (decimal)falseNegatives / (falseNegatives + truePositives) : 0;
        var precision = (truePositives + falsePositives) > 0
            ? (decimal)truePositives / (truePositives + falsePositives) : 0;
        var recall = (truePositives + falseNegatives) > 0
            ? (decimal)truePositives / (truePositives + falseNegatives) : 0;
        var accuracy = total > 0
            ? (decimal)(truePositives + trueNegatives) / total : 0;
        var f1 = (precision + recall) > 0
            ? 2 * precision * recall / (precision + recall) : 0;

        var disparityFpr = baselineFpr > 0 ? fpr / baselineFpr : 1;
        var disparityFnr = baselineFnr > 0 ? fnr / baselineFnr : 1;

        var passes = Math.Abs(disparityFpr - 1) <= fairnessThreshold
                  && Math.Abs(disparityFnr - 1) <= fairnessThreshold;

        return new AiBiasTestResult
        {
            AiSystemName = aiSystemName,
            TestType = testType,
            ProtectedAttribute = protectedAttribute,
            ProtectedGroupValue = protectedGroupValue,
            SampleSize = sampleSize,
            TruePositives = truePositives,
            FalsePositives = falsePositives,
            TrueNegatives = trueNegatives,
            FalseNegatives = falseNegatives,
            FalsePositiveRate = Math.Round(fpr, 6),
            FalseNegativeRate = Math.Round(fnr, 6),
            Accuracy = Math.Round(accuracy, 6),
            Precision = Math.Round(precision, 6),
            Recall = Math.Round(recall, 6),
            F1Score = Math.Round(f1, 6),
            BaselineFpr = baselineFpr,
            BaselineFnr = baselineFnr,
            DisparityRatioFpr = Math.Round(disparityFpr, 4),
            DisparityRatioFnr = Math.Round(disparityFnr, 4),
            PassesFairnessThreshold = passes,
            FairnessThreshold = fairnessThreshold,
            TestRunAt = DateTime.UtcNow,
            TestRunBy = testRunBy,
            TestPeriodStart = testPeriodStart,
            TestPeriodEnd = testPeriodEnd
        };
    }

    public void SetExplanation(string explanation, string? featureImportanceJson = null)
    {
        Explanation = explanation;
        FeatureImportance = featureImportanceJson;
        SetUpdated();
    }

    public void SetRemediation(string remediation)
    {
        RemediationAction = remediation;
        SetUpdated();
    }
}

public static class AiSystemNames
{
    public const string ThreatDetection = "ThreatDetection";
    public const string BehavioralAnalytics = "BehavioralAnalytics";
    public const string BiometricMatcher = "BiometricMatcher";
    public const string BotDetection = "BotDetection";
    public const string ContextualAuth = "ContextualAuth";
}

public static class BiasTestTypes
{
    public const string Fairness = "fairness";
    public const string Accuracy = "accuracy";
    public const string Robustness = "robustness";
    public const string Explainability = "explainability";
}

public static class ProtectedAttributes
{
    public const string GeographicRegion = "geographic_region";
    public const string DeviceType = "device_type";
    public const string TimeZone = "time_zone";
    public const string UserRole = "user_role";
    public const string AccessPattern = "access_pattern";
    public const string NetworkType = "network_type";
}
