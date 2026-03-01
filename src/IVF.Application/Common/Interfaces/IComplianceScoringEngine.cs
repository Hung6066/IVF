using IVF.Domain.Enums;

namespace IVF.Application.Common.Interfaces;

/// <summary>
/// Enterprise compliance scoring engine — evaluates vault state against
/// HIPAA, SOC 2, and GDPR control frameworks in real time.
/// </summary>
public interface IComplianceScoringEngine
{
    Task<ComplianceReport> EvaluateAsync(CancellationToken ct = default);
    Task<FrameworkScore> EvaluateFrameworkAsync(ComplianceFramework framework, CancellationToken ct = default);
}

public enum ComplianceFramework
{
    Hipaa,
    Soc2,
    Gdpr
}

public enum ControlStatus
{
    Pass,
    Fail,
    Partial,
    NotApplicable
}

public sealed record ComplianceReport(
    DateTime EvaluatedAt,
    int OverallScore,
    int MaxScore,
    double Percentage,
    string Grade,
    List<FrameworkScore> Frameworks);

public sealed record FrameworkScore(
    ComplianceFramework Framework,
    string Name,
    int Score,
    int MaxScore,
    double Percentage,
    List<ControlResult> Controls);

public sealed record ControlResult(
    string ControlId,
    string Name,
    string Description,
    ControlStatus Status,
    int Score,
    int MaxScore,
    string? Finding);
