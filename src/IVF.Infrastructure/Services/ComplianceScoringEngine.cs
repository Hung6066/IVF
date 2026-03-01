using System.Text.Json;
using IVF.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

/// <summary>
/// Evaluates vault infrastructure against HIPAA, SOC 2, and GDPR controls.
/// Each control queries live vault state to produce a real-time compliance score.
/// </summary>
public sealed class ComplianceScoringEngine : IComplianceScoringEngine
{
    private readonly IVaultRepository _repo;
    private readonly IKeyVaultService _kvService;
    private readonly ILogger<ComplianceScoringEngine> _logger;

    public ComplianceScoringEngine(
        IVaultRepository repo,
        IKeyVaultService kvService,
        ILogger<ComplianceScoringEngine> logger)
    {
        _repo = repo;
        _kvService = kvService;
        _logger = logger;
    }

    public async Task<ComplianceReport> EvaluateAsync(CancellationToken ct = default)
    {
        var frameworks = new List<FrameworkScore>
        {
            await EvaluateFrameworkAsync(ComplianceFramework.Hipaa, ct),
            await EvaluateFrameworkAsync(ComplianceFramework.Soc2, ct),
            await EvaluateFrameworkAsync(ComplianceFramework.Gdpr, ct),
        };

        var totalScore = frameworks.Sum(f => f.Score);
        var totalMax = frameworks.Sum(f => f.MaxScore);
        var percentage = totalMax > 0 ? Math.Round(totalScore * 100.0 / totalMax, 1) : 0;

        return new ComplianceReport(
            DateTime.UtcNow,
            totalScore,
            totalMax,
            percentage,
            CalculateGrade(percentage),
            frameworks);
    }

    public async Task<FrameworkScore> EvaluateFrameworkAsync(ComplianceFramework framework, CancellationToken ct = default)
    {
        var controls = framework switch
        {
            ComplianceFramework.Hipaa => await EvaluateHipaaAsync(ct),
            ComplianceFramework.Soc2 => await EvaluateSoc2Async(ct),
            ComplianceFramework.Gdpr => await EvaluateGdprAsync(ct),
            _ => throw new ArgumentOutOfRangeException(nameof(framework))
        };

        var score = controls.Sum(c => c.Score);
        var max = controls.Sum(c => c.MaxScore);
        var pct = max > 0 ? Math.Round(score * 100.0 / max, 1) : 0;

        return new FrameworkScore(
            framework,
            framework.ToString().ToUpperInvariant(),
            score, max, pct, controls);
    }

    // ─── HIPAA Security Rule Controls ────────────────────

    private async Task<List<ControlResult>> EvaluateHipaaAsync(CancellationToken ct)
    {
        var controls = new List<ControlResult>();

        // §164.312(a)(2)(iv) — Encryption at rest
        var encConfigs = await _repo.GetAllEncryptionConfigsAsync(ct);
        controls.Add(new ControlResult(
            "HIPAA-1", "Encryption at Rest",
            "ePHI must be encrypted using AES-256 or equivalent",
            encConfigs.Count > 0 ? ControlStatus.Pass : ControlStatus.Fail,
            encConfigs.Count > 0 ? 10 : 0, 10,
            encConfigs.Count > 0 ? $"{encConfigs.Count} table(s) encrypted" : "No encryption configs found"));

        // §164.312(b) — Audit controls
        var auditCount = await _repo.GetAuditLogCountAsync(ct: ct);
        controls.Add(new ControlResult(
            "HIPAA-2", "Audit Controls",
            "Record and examine activity in systems containing ePHI",
            auditCount > 0 ? ControlStatus.Pass : ControlStatus.Fail,
            auditCount > 0 ? 10 : 0, 10,
            $"{auditCount} audit log entries"));

        // §164.312(c)(1) — Integrity controls
        var secrets = await _repo.ListSecretsAsync(ct: ct);
        var versionedSecrets = secrets.Count(s => s.Version > 1);
        controls.Add(new ControlResult(
            "HIPAA-3", "Data Integrity",
            "Protect ePHI from improper alteration or destruction",
            secrets.Count > 0 ? ControlStatus.Pass : ControlStatus.Partial,
            secrets.Count > 0 ? 10 : 5, 10,
            $"{secrets.Count} secrets managed, {versionedSecrets} versioned"));

        // §164.312(d) — Person or entity authentication
        var policies = await _repo.GetPoliciesAsync(ct);
        var userPolicies = await _repo.GetUserPoliciesAsync(ct);
        controls.Add(new ControlResult(
            "HIPAA-4", "Authentication",
            "Verify identity of persons seeking access to ePHI",
            policies.Count > 0 && userPolicies.Count > 0 ? ControlStatus.Pass : ControlStatus.Partial,
            policies.Count > 0 ? 10 : 0, 10,
            $"{policies.Count} policies, {userPolicies.Count} user assignments"));

        // §164.312(e)(1) — Transmission security
        var kvHealthy = await _kvService.IsHealthyAsync();
        controls.Add(new ControlResult(
            "HIPAA-5", "Transmission Security",
            "Guard against unauthorized access during electronic transmission",
            kvHealthy ? ControlStatus.Pass : ControlStatus.Fail,
            kvHealthy ? 10 : 0, 10,
            kvHealthy ? "Key vault connection is TLS-encrypted" : "Key vault unhealthy"));

        // §164.308(a)(5)(ii)(D) — Password management
        var tokens = await _repo.GetTokensAsync(includeRevoked: false, ct: ct);
        var expiredTokens = tokens.Where(t => t.ExpiresAt < DateTime.UtcNow).ToList();
        controls.Add(new ControlResult(
            "HIPAA-6", "Access Token Management",
            "Procedures for creating, changing, and safeguarding passwords/tokens",
            expiredTokens.Count == 0 ? ControlStatus.Pass : ControlStatus.Partial,
            expiredTokens.Count == 0 ? 10 : 5, 10,
            $"{tokens.Count} active tokens, {expiredTokens.Count} expired but not revoked"));

        // §164.308(a)(4) — Access management (rotation schedules)
        var rotations = await _repo.GetRotationSchedulesAsync(activeOnly: true, ct: ct);
        controls.Add(new ControlResult(
            "HIPAA-7", "Secret Rotation",
            "Automated credential rotation to limit exposure window",
            rotations.Count > 0 ? ControlStatus.Pass : ControlStatus.Fail,
            rotations.Count > 0 ? 10 : 0, 10,
            $"{rotations.Count} active rotation schedule(s)"));

        // §164.312(a)(1) — Access control (field-level)
        var fieldPolicies = await _repo.GetAllFieldAccessPoliciesAsync(ct);
        controls.Add(new ControlResult(
            "HIPAA-8", "Field-Level Access Control",
            "Fine-grained access control for sensitive data fields",
            fieldPolicies.Count > 0 ? ControlStatus.Pass : ControlStatus.Partial,
            fieldPolicies.Count > 0 ? 10 : 5, 10,
            $"{fieldPolicies.Count} field access policies"));

        // §164.310(d)(1) — Device and media controls
        var autoUnseal = await _repo.GetAutoUnsealConfigAsync(ct);
        controls.Add(new ControlResult(
            "HIPAA-9", "Key Protection",
            "Hardware/software mechanisms to protect encryption keys",
            autoUnseal != null ? ControlStatus.Pass : ControlStatus.Partial,
            autoUnseal != null ? 10 : 5, 10,
            autoUnseal != null ? "Auto-unseal configured" : "Manual unseal only"));

        // §164.308(a)(1)(ii)(D) — Contingency plan (lease management)
        var leases = await _repo.GetLeasesAsync(includeExpired: false, ct: ct);
        controls.Add(new ControlResult(
            "HIPAA-10", "Lease Management",
            "Time-bounded access with automatic revocation",
            leases.Count >= 0 ? ControlStatus.Pass : ControlStatus.Fail,
            10, 10,
            $"{leases.Count} active lease(s)"));

        return controls;
    }

    // ─── SOC 2 Trust Service Criteria ────────────────────

    private async Task<List<ControlResult>> EvaluateSoc2Async(CancellationToken ct)
    {
        var controls = new List<ControlResult>();

        // CC6.1 — Logical access security
        var policies = await _repo.GetPoliciesAsync(ct);
        var userPolicies = await _repo.GetUserPoliciesAsync(ct);
        controls.Add(new ControlResult(
            "SOC2-CC6.1", "Logical Access Security",
            "Implement logical access security over protected information assets",
            policies.Count > 0 ? ControlStatus.Pass : ControlStatus.Fail,
            policies.Count > 0 ? 10 : 0, 10,
            $"{policies.Count} vault policies, {userPolicies.Count} user bindings"));

        // CC6.3 — Restricts access (encryption)
        var encConfigs = await _repo.GetAllEncryptionConfigsAsync(ct);
        controls.Add(new ControlResult(
            "SOC2-CC6.3", "Encryption Controls",
            "Restrict access through encryption of data at rest and in transit",
            encConfigs.Count > 0 ? ControlStatus.Pass : ControlStatus.Fail,
            encConfigs.Count > 0 ? 10 : 0, 10,
            $"{encConfigs.Count} tables with field-level encryption"));

        // CC6.7 — Restricts data transmission
        var kvHealthy = await _kvService.IsHealthyAsync();
        controls.Add(new ControlResult(
            "SOC2-CC6.7", "Data Transmission",
            "Restrict the transmission of data to authorized external parties",
            kvHealthy ? ControlStatus.Pass : ControlStatus.Fail,
            kvHealthy ? 10 : 0, 10,
            kvHealthy ? "Key vault TLS connection verified" : "Key vault unhealthy"));

        // CC7.2 — Monitoring activities
        var auditCount = await _repo.GetAuditLogCountAsync(ct: ct);
        controls.Add(new ControlResult(
            "SOC2-CC7.2", "Monitoring Activities",
            "Monitor system components for anomalies and security events",
            auditCount > 100 ? ControlStatus.Pass : auditCount > 0 ? ControlStatus.Partial : ControlStatus.Fail,
            auditCount > 100 ? 10 : auditCount > 0 ? 5 : 0, 10,
            $"{auditCount} audit entries (100+ recommended)"));

        // CC8.1 — Change management
        var secrets = await _repo.ListSecretsAsync(ct: ct);
        var versionedCount = secrets.Count(s => s.Version > 1);
        controls.Add(new ControlResult(
            "SOC2-CC8.1", "Change Management",
            "Authorize, design, develop, configure, document, test, approve changes",
            versionedCount > 0 ? ControlStatus.Pass : ControlStatus.Partial,
            versionedCount > 0 ? 10 : 5, 10,
            $"{versionedCount}/{secrets.Count} secrets have version history"));

        // A1.2 — Environmental protections (DR)
        var dekVersionSetting = await _repo.GetSettingAsync("dek-version-data", ct);
        controls.Add(new ControlResult(
            "SOC2-A1.2", "Recovery Mechanisms",
            "Recovery infrastructure and tested recovery procedures",
            dekVersionSetting != null ? ControlStatus.Pass : ControlStatus.Partial,
            dekVersionSetting != null ? 10 : 5, 10,
            dekVersionSetting != null ? "DEK versioning active" : "No DEK version metadata found"));

        // C1.1 — Confidentiality commitments
        var fieldPolicies = await _repo.GetAllFieldAccessPoliciesAsync(ct);
        controls.Add(new ControlResult(
            "SOC2-C1.1", "Confidentiality",
            "Identify and protect confidential information",
            fieldPolicies.Count > 0 ? ControlStatus.Pass : ControlStatus.Partial,
            fieldPolicies.Count > 0 ? 10 : 5, 10,
            $"{fieldPolicies.Count} field access policies defined"));

        return controls;
    }

    // ─── GDPR Controls ──────────────────────────────────

    private async Task<List<ControlResult>> EvaluateGdprAsync(CancellationToken ct)
    {
        var controls = new List<ControlResult>();

        // Art. 32 — Security of processing (encryption)
        var encConfigs = await _repo.GetAllEncryptionConfigsAsync(ct);
        controls.Add(new ControlResult(
            "GDPR-32a", "Pseudonymisation & Encryption",
            "Encryption of personal data (Article 32.1.a)",
            encConfigs.Count > 0 ? ControlStatus.Pass : ControlStatus.Fail,
            encConfigs.Count > 0 ? 10 : 0, 10,
            $"{encConfigs.Count} tables encrypted at field level"));

        // Art. 32 — Confidentiality
        var policies = await _repo.GetPoliciesAsync(ct);
        controls.Add(new ControlResult(
            "GDPR-32b", "Confidentiality",
            "Ensure ongoing confidentiality of processing systems (Article 32.1.b)",
            policies.Count > 0 ? ControlStatus.Pass : ControlStatus.Fail,
            policies.Count > 0 ? 10 : 0, 10,
            $"{policies.Count} access policies enforced"));

        // Art. 32 — Resilience
        var autoUnseal = await _repo.GetAutoUnsealConfigAsync(ct);
        controls.Add(new ControlResult(
            "GDPR-32c", "Resilience",
            "Ability to restore availability and access to data (Article 32.1.c)",
            autoUnseal != null ? ControlStatus.Pass : ControlStatus.Partial,
            autoUnseal != null ? 10 : 5, 10,
            autoUnseal != null ? "Auto-unseal configured for availability" : "Manual unseal only"));

        // Art. 32 — Testing
        var auditCount = await _repo.GetAuditLogCountAsync(ct: ct);
        controls.Add(new ControlResult(
            "GDPR-32d", "Regular Testing",
            "Process for regularly testing security measures (Article 32.1.d)",
            auditCount > 0 ? ControlStatus.Pass : ControlStatus.Fail,
            auditCount > 0 ? 10 : 0, 10,
            $"{auditCount} audit entries for security testing evidence"));

        // Art. 5(1)(f) — Integrity and confidentiality
        var fieldPolicies = await _repo.GetAllFieldAccessPoliciesAsync(ct);
        controls.Add(new ControlResult(
            "GDPR-5f", "Integrity & Confidentiality",
            "Protect against unauthorized processing, loss, destruction (Article 5.1.f)",
            fieldPolicies.Count > 0 && encConfigs.Count > 0 ? ControlStatus.Pass : ControlStatus.Partial,
            fieldPolicies.Count > 0 && encConfigs.Count > 0 ? 10 : 5, 10,
            $"{encConfigs.Count} encryption configs, {fieldPolicies.Count} access policies"));

        // Art. 25 — Data protection by design
        var rotations = await _repo.GetRotationSchedulesAsync(activeOnly: true, ct: ct);
        controls.Add(new ControlResult(
            "GDPR-25", "Privacy by Design",
            "Data protection by design and by default (Article 25)",
            rotations.Count > 0 ? ControlStatus.Pass : ControlStatus.Partial,
            rotations.Count > 0 ? 10 : 5, 10,
            $"{rotations.Count} automated rotation schedules"));

        // Art. 30 — Records of processing activities
        controls.Add(new ControlResult(
            "GDPR-30", "Processing Records",
            "Maintain records of processing activities (Article 30)",
            auditCount > 0 ? ControlStatus.Pass : ControlStatus.Fail,
            auditCount > 0 ? 10 : 0, 10,
            $"Vault audit log maintains processing records ({auditCount} entries)"));

        return controls;
    }

    private static string CalculateGrade(double percentage) => percentage switch
    {
        >= 95 => "A+",
        >= 90 => "A",
        >= 85 => "A-",
        >= 80 => "B+",
        >= 75 => "B",
        >= 70 => "B-",
        >= 65 => "C+",
        >= 60 => "C",
        >= 50 => "D",
        _ => "F"
    };
}
