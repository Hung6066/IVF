using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

/// <summary>
/// Risk-based step-up authentication — inspired by Google Advanced Protection.
/// Evaluates threat assessment and device trust to decide if additional
/// authentication factors are needed beyond the user's normal auth level.
/// </summary>
public sealed class StepUpAuthService(
    ILogger<StepUpAuthService> logger) : IStepUpAuthService
{
    public Task<StepUpDecision> EvaluateStepUpAsync(
        Guid userId,
        ThreatAssessment assessment,
        DeviceTrustResult deviceTrust,
        CancellationToken ct = default)
    {
        // Risk-based step-up decision matrix:
        // Critical risk (70+) → Block
        // High risk (50-69) → Require MFA + device verification
        // Medium risk (25-49) → Require MFA
        // Low risk + untrusted device → Require MFA
        // Low risk + trusted device → Allow

        if (assessment.RiskLevel == RiskLevel.Critical || assessment.RiskScore >= 70)
        {
            logger.LogWarning("Step-up: blocking user {UserId} due to critical risk score {Score}",
                userId, assessment.RiskScore);

            return Task.FromResult(new StepUpDecision(
                RequiresStepUp: true,
                RequiredAction: "block",
                Reason: "Critical security risk detected",
                RiskScore: assessment.RiskScore));
        }

        if (assessment.RiskLevel == RiskLevel.High || assessment.RiskScore >= 50)
        {
            logger.LogInformation("Step-up: requiring MFA + device verify for user {UserId}, risk {Score}",
                userId, assessment.RiskScore);

            return Task.FromResult(new StepUpDecision(
                RequiresStepUp: true,
                RequiredAction: "mfa_and_device_verify",
                Reason: "High security risk — additional verification required",
                RiskScore: assessment.RiskScore));
        }

        if (assessment.RiskLevel == RiskLevel.Medium || assessment.RiskScore >= 25)
        {
            logger.LogInformation("Step-up: requiring MFA for user {UserId}, risk {Score}",
                userId, assessment.RiskScore);

            return Task.FromResult(new StepUpDecision(
                RequiresStepUp: true,
                RequiredAction: "mfa",
                Reason: "Elevated security risk — MFA required",
                RiskScore: assessment.RiskScore));
        }

        // Low risk but untrusted device
        if (!deviceTrust.IsTrusted && deviceTrust.TrustLevel < DeviceTrustLevel.PartiallyTrusted)
        {
            logger.LogInformation("Step-up: requiring MFA for user {UserId} on untrusted device",
                userId);

            return Task.FromResult(new StepUpDecision(
                RequiresStepUp: true,
                RequiredAction: "mfa",
                Reason: "Login from unrecognized device",
                RiskScore: assessment.RiskScore));
        }

        return Task.FromResult(new StepUpDecision(
            RequiresStepUp: false,
            RequiredAction: null,
            Reason: null,
            RiskScore: assessment.RiskScore));
    }
}
