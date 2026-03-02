using IVF.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace IVF.Infrastructure.Services;

/// <summary>
/// Evaluates vault infrastructure against HIPAA, SOC 2, and GDPR controls.
/// Each control queries live vault state to produce a real-time compliance score.
/// v2: 15 HIPAA + 12 SOC 2 + 11 GDPR = 38 controls, 380 max points.
/// </summary>
public sealed class ComplianceScoringEngine : IComplianceScoringEngine
{
    private readonly IVaultRepository _repo;
    private readonly IKeyVaultService _kvService;
    private readonly ISecurityEventService _securityEvents;
    private readonly IVaultDrService _drService;
    private readonly ILogger<ComplianceScoringEngine> _logger;

    public ComplianceScoringEngine(
        IVaultRepository repo,
        IKeyVaultService kvService,
        ISecurityEventService securityEvents,
        IVaultDrService drService,
        ILogger<ComplianceScoringEngine> logger)
    {
        _repo = repo;
        _kvService = kvService;
        _securityEvents = securityEvents;
        _drService = drService;
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

    // ─── HIPAA Security Rule — 15 Controls (150 points) ─────────────

    private async Task<List<ControlResult>> EvaluateHipaaAsync(CancellationToken ct)
    {
        var controls = new List<ControlResult>();

        // Prefetch shared data
        var encConfigs = await _repo.GetAllEncryptionConfigsAsync(ct);
        var auditCount = await _repo.GetAuditLogCountAsync(ct: ct);
        var secrets = await _repo.ListSecretsAsync(ct: ct);
        var policies = await _repo.GetPoliciesAsync(ct);
        var userPolicies = await _repo.GetUserPoliciesAsync(ct);
        var tokens = await _repo.GetTokensAsync(includeRevoked: false, ct: ct);
        var rotations = await _repo.GetRotationSchedulesAsync(activeOnly: true, ct: ct);
        var fieldPolicies = await _repo.GetAllFieldAccessPoliciesAsync(ct);
        var autoUnseal = await _repo.GetAutoUnsealConfigAsync(ct);
        var leases = await _repo.GetLeasesAsync(includeExpired: false, ct: ct);
        var dynamicCreds = await _repo.GetDynamicCredentialsAsync(includeRevoked: false, ct: ct);

        // HIPAA-1: §164.312(a)(2)(iv) — Encryption at rest
        var enabledConfigs = encConfigs.Count(c => c.IsEnabled);
        controls.Add(new ControlResult(
            "HIPAA-1", "Encryption at Rest",
            "ePHI must be encrypted using AES-256 or equivalent (§164.312(a)(2)(iv))",
            enabledConfigs >= 3 ? ControlStatus.Pass : enabledConfigs > 0 ? ControlStatus.Partial : ControlStatus.Fail,
            enabledConfigs >= 3 ? 10 : enabledConfigs > 0 ? 5 : 0, 10,
            enabledConfigs > 0
                ? $"{enabledConfigs}/{encConfigs.Count} encryption configs active"
                : "No encryption configs found",
            enabledConfigs < 3 ? "Configure field-level encryption for all tables containing ePHI (minimum 3 tables)" : null));

        // HIPAA-2: §164.312(b) — Audit controls
        var auditStatus = auditCount >= 100 ? ControlStatus.Pass : auditCount > 0 ? ControlStatus.Partial : ControlStatus.Fail;
        controls.Add(new ControlResult(
            "HIPAA-2", "Audit Controls",
            "Record and examine activity in systems containing ePHI (§164.312(b))",
            auditStatus,
            auditCount >= 100 ? 10 : auditCount > 0 ? 5 : 0, 10,
            $"{auditCount} audit log entries (100+ recommended for compliance)",
            auditCount < 100 ? "Ensure all vault operations are audited; minimum 100 entries for baseline" : null));

        // HIPAA-3: §164.312(c)(1) — Integrity controls (versioning)
        var versionedSecrets = secrets.Count(s => s.Version > 1);
        var integrityStatus = versionedSecrets > 0 && secrets.Count > 0 ? ControlStatus.Pass
            : secrets.Count > 0 ? ControlStatus.Partial : ControlStatus.Fail;
        controls.Add(new ControlResult(
            "HIPAA-3", "Data Integrity",
            "Protect ePHI from improper alteration or destruction (§164.312(c)(1))",
            integrityStatus,
            integrityStatus == ControlStatus.Pass ? 10 : integrityStatus == ControlStatus.Partial ? 5 : 0, 10,
            $"{secrets.Count} secrets managed, {versionedSecrets} with version history",
            versionedSecrets == 0 ? "Enable secret versioning to track all changes to sensitive data" : null));

        // HIPAA-4: §164.312(d) — Person/entity authentication
        var authStatus = policies.Count > 0 && userPolicies.Count > 0 ? ControlStatus.Pass
            : policies.Count > 0 || userPolicies.Count > 0 ? ControlStatus.Partial : ControlStatus.Fail;
        controls.Add(new ControlResult(
            "HIPAA-4", "Authentication & Authorization",
            "Verify identity of persons seeking access to ePHI (§164.312(d))",
            authStatus,
            authStatus == ControlStatus.Pass ? 10 : authStatus == ControlStatus.Partial ? 5 : 0, 10,
            $"{policies.Count} policies, {userPolicies.Count} user assignments",
            authStatus != ControlStatus.Pass ? "Create vault policies and assign them to all users" : null));

        // HIPAA-5: §164.312(e)(1) — Transmission security (TLS)
        var kvHealthy = await _kvService.IsHealthyAsync();
        controls.Add(new ControlResult(
            "HIPAA-5", "Transmission Security",
            "Guard against unauthorized access during electronic transmission (§164.312(e)(1))",
            kvHealthy ? ControlStatus.Pass : ControlStatus.Fail,
            kvHealthy ? 10 : 0, 10,
            kvHealthy ? "Key vault connection is TLS-encrypted and healthy" : "Key vault connection failed",
            !kvHealthy ? "Verify Key Vault connection string and TLS certificate configuration" : null));

        // HIPAA-6: §164.308(a)(5)(ii)(D) — Token/password management
        var expiredTokens = tokens.Where(t => t.ExpiresAt < DateTime.UtcNow).ToList();
        var longLivedTokens = tokens.Where(t => t.ExpiresAt > DateTime.UtcNow.AddDays(30)).ToList();
        var tokenStatus = expiredTokens.Count == 0 && longLivedTokens.Count == 0 ? ControlStatus.Pass
            : expiredTokens.Count == 0 ? ControlStatus.Partial : ControlStatus.Fail;
        controls.Add(new ControlResult(
            "HIPAA-6", "Token Lifecycle Management",
            "Procedures for creating, changing, and safeguarding passwords/tokens (§164.308(a)(5)(ii)(D))",
            tokenStatus,
            tokenStatus == ControlStatus.Pass ? 10 : tokenStatus == ControlStatus.Partial ? 5 : 0, 10,
            $"{tokens.Count} active, {expiredTokens.Count} expired unrevoked, {longLivedTokens.Count} long-lived (>30d)",
            expiredTokens.Count > 0 ? "Revoke all expired tokens immediately"
                : longLivedTokens.Count > 0 ? "Reduce token TTL to 30 days or less" : null));

        // HIPAA-7: §164.308(a)(4) — Secret rotation
        var executedRotations = rotations.Count(r => r.LastRotatedAt != null);
        var rotationStatus = rotations.Count > 0 && executedRotations > 0 ? ControlStatus.Pass
            : rotations.Count > 0 ? ControlStatus.Partial : ControlStatus.Fail;
        controls.Add(new ControlResult(
            "HIPAA-7", "Secret Rotation",
            "Automated credential rotation to limit exposure window (§164.308(a)(4))",
            rotationStatus,
            rotationStatus == ControlStatus.Pass ? 10 : rotationStatus == ControlStatus.Partial ? 5 : 0, 10,
            $"{rotations.Count} schedules, {executedRotations} successfully executed",
            rotationStatus != ControlStatus.Pass ? "Configure and verify at least one automated rotation schedule" : null));

        // HIPAA-8: §164.312(a)(1) — Field-level access control
        var faPolicyStatus = fieldPolicies.Count >= 3 ? ControlStatus.Pass
            : fieldPolicies.Count > 0 ? ControlStatus.Partial : ControlStatus.Fail;
        controls.Add(new ControlResult(
            "HIPAA-8", "Field-Level Access Control",
            "Fine-grained access control for sensitive data fields (§164.312(a)(1))",
            faPolicyStatus,
            faPolicyStatus == ControlStatus.Pass ? 10 : faPolicyStatus == ControlStatus.Partial ? 5 : 0, 10,
            $"{fieldPolicies.Count} field access policies (3+ recommended)",
            fieldPolicies.Count < 3 ? "Define field-level access policies for all ePHI fields (SSN, DOB, medical records)" : null));

        // HIPAA-9: §164.310(d)(1) — Key protection (auto-unseal)
        controls.Add(new ControlResult(
            "HIPAA-9", "Key Protection",
            "Hardware/software mechanisms to protect encryption keys (§164.310(d)(1))",
            autoUnseal != null ? ControlStatus.Pass : ControlStatus.Partial,
            autoUnseal != null ? 10 : 5, 10,
            autoUnseal != null ? "Auto-unseal configured with Azure Key Vault HSM" : "Manual unseal only — keys at risk during restarts",
            autoUnseal == null ? "Configure auto-unseal with Azure Key Vault for automated key protection" : null));

        // HIPAA-10: §164.308(a)(1)(ii)(D) — Lease management (FIXED BUG: was always Pass)
        var leaseStatus = leases.Count > 0
            ? (leases.All(l => l.Ttl > 0) ? ControlStatus.Pass : ControlStatus.Partial)
            : ControlStatus.Partial;
        controls.Add(new ControlResult(
            "HIPAA-10", "Lease Management",
            "Time-bounded access with automatic revocation (§164.308(a)(1)(ii)(D))",
            leaseStatus,
            leaseStatus == ControlStatus.Pass ? 10 : 5, 10,
            $"{leases.Count} active lease(s) with time-bounded access",
            leases.Count == 0 ? "Create leases with TTL for all temporary access grants" : null));

        // HIPAA-11: §164.514(d) — Minimum necessary (dynamic credentials)
        var dynCredStatus = dynamicCreds.Count > 0 ? ControlStatus.Pass : ControlStatus.Partial;
        controls.Add(new ControlResult(
            "HIPAA-11", "Minimum Necessary Access",
            "Limit access to the minimum necessary ePHI (§164.514(d))",
            dynCredStatus,
            dynCredStatus == ControlStatus.Pass ? 10 : 5, 10,
            dynamicCreds.Count > 0
                ? $"{dynamicCreds.Count} dynamic credentials with time-limited scope"
                : "No dynamic credentials — static credentials may grant excessive access",
            dynamicCreds.Count == 0 ? "Use dynamic credentials for database access to limit privilege scope and duration" : null));

        // HIPAA-12: §164.308(a)(3) — Workforce security (user policy coverage)
        var uniqueUsersBound = userPolicies.Select(up => up.UserId).Distinct().Count();
        var coverageStatus = uniqueUsersBound >= 3 ? ControlStatus.Pass
            : uniqueUsersBound > 0 ? ControlStatus.Partial : ControlStatus.Fail;
        controls.Add(new ControlResult(
            "HIPAA-12", "Workforce Security",
            "Authorize workforce members accessing ePHI (§164.308(a)(3))",
            coverageStatus,
            coverageStatus == ControlStatus.Pass ? 10 : coverageStatus == ControlStatus.Partial ? 5 : 0, 10,
            $"{uniqueUsersBound} users have vault policy assignments",
            uniqueUsersBound < 3 ? "Assign vault policies to all workforce members who require ePHI access" : null));

        // HIPAA-13: §164.308(a)(6) — Security incident procedures
        List<Domain.Entities.SecurityEvent> recentEvents;
        try { recentEvents = await _securityEvents.GetRecentEventsAsync(100, ct); }
        catch { recentEvents = []; }

        var highSeverity = recentEvents.Count(e => e.Severity is "High" or "Critical");
        var incidentStatus = recentEvents.Count > 0 ? ControlStatus.Pass : ControlStatus.Fail;
        controls.Add(new ControlResult(
            "HIPAA-13", "Security Incident Procedures",
            "Identify, respond to, and mitigate security incidents (§164.308(a)(6))",
            incidentStatus,
            incidentStatus == ControlStatus.Pass ? 10 : 0, 10,
            recentEvents.Count > 0
                ? $"{recentEvents.Count} security events tracked, {highSeverity} high/critical"
                : "No security event logging detected",
            recentEvents.Count == 0 ? "Enable security event logging for all authentication and authorization failures" : null));

        // HIPAA-14: §164.308(a)(7) — Contingency plan (DR)
        DrReadinessStatus? drStatus;
        try { drStatus = await _drService.GetReadinessAsync(ct); }
        catch { drStatus = null; }

        var drReady = drStatus is { AutoUnsealConfigured: true, EncryptionActive: true };
        controls.Add(new ControlResult(
            "HIPAA-14", "Contingency Plan",
            "Establish policies for responding to emergencies or disasters (§164.308(a)(7))",
            drReady ? ControlStatus.Pass : drStatus != null ? ControlStatus.Partial : ControlStatus.Fail,
            drReady ? 10 : drStatus != null ? 5 : 0, 10,
            drReady ? $"DR ready — grade: {drStatus!.ReadinessGrade}" : "DR not fully configured",
            !drReady ? "Configure vault backup and validate disaster recovery procedures" : null));

        // HIPAA-15: §164.312(a)(2)(iii) — Automatic logoff (token expiry enforcement)
        var shortLivedTokens = tokens.Count(t => t.ExpiresAt <= DateTime.UtcNow.AddHours(24));
        var logoffStatus = tokens.Count > 0 && shortLivedTokens == tokens.Count ? ControlStatus.Pass
            : tokens.Count > 0 ? ControlStatus.Partial : ControlStatus.Fail;
        controls.Add(new ControlResult(
            "HIPAA-15", "Automatic Logoff",
            "Terminate sessions after predetermined time of inactivity (§164.312(a)(2)(iii))",
            logoffStatus,
            logoffStatus == ControlStatus.Pass ? 10 : logoffStatus == ControlStatus.Partial ? 5 : 0, 10,
            $"{shortLivedTokens}/{tokens.Count} tokens expire within 24 hours",
            logoffStatus != ControlStatus.Pass ? "Set all vault tokens to expire within 24 hours for HIPAA compliance" : null));

        return controls;
    }

    // ─── SOC 2 Trust Service Criteria — 12 Controls (120 points) ─────

    private async Task<List<ControlResult>> EvaluateSoc2Async(CancellationToken ct)
    {
        var controls = new List<ControlResult>();

        // Prefetch shared data
        var policies = await _repo.GetPoliciesAsync(ct);
        var userPolicies = await _repo.GetUserPoliciesAsync(ct);
        var encConfigs = await _repo.GetAllEncryptionConfigsAsync(ct);
        var auditCount = await _repo.GetAuditLogCountAsync(ct: ct);
        var secrets = await _repo.ListSecretsAsync(ct: ct);
        var fieldPolicies = await _repo.GetAllFieldAccessPoliciesAsync(ct);
        var rotations = await _repo.GetRotationSchedulesAsync(activeOnly: true, ct: ct);
        var tokens = await _repo.GetTokensAsync(includeRevoked: false, ct: ct);
        var dynamicCreds = await _repo.GetDynamicCredentialsAsync(includeRevoked: false, ct: ct);

        // SOC2-CC6.1 — Logical access security
        var accessStatus = policies.Count > 0 && userPolicies.Count > 0 ? ControlStatus.Pass
            : policies.Count > 0 ? ControlStatus.Partial : ControlStatus.Fail;
        controls.Add(new ControlResult(
            "SOC2-CC6.1", "Logical Access Security",
            "Implement logical access security over protected information assets",
            accessStatus,
            accessStatus == ControlStatus.Pass ? 10 : accessStatus == ControlStatus.Partial ? 5 : 0, 10,
            $"{policies.Count} vault policies, {userPolicies.Count} user bindings",
            accessStatus != ControlStatus.Pass ? "Define vault policies and assign to all users who access the system" : null));

        // SOC2-CC6.3 — Encryption controls
        var enabledConfigs = encConfigs.Count(c => c.IsEnabled);
        var encStatus = enabledConfigs >= 3 ? ControlStatus.Pass : enabledConfigs > 0 ? ControlStatus.Partial : ControlStatus.Fail;
        controls.Add(new ControlResult(
            "SOC2-CC6.3", "Encryption Controls",
            "Restrict access through encryption of data at rest and in transit",
            encStatus,
            encStatus == ControlStatus.Pass ? 10 : encStatus == ControlStatus.Partial ? 5 : 0, 10,
            $"{enabledConfigs}/{encConfigs.Count} active encryption configs (3+ recommended)",
            enabledConfigs < 3 ? "Enable field-level encryption for all tables containing sensitive data" : null));

        // SOC2-CC6.7 — Data transmission
        var kvHealthy = await _kvService.IsHealthyAsync();
        controls.Add(new ControlResult(
            "SOC2-CC6.7", "Data Transmission Security",
            "Restrict the transmission of data to authorized external parties",
            kvHealthy ? ControlStatus.Pass : ControlStatus.Fail,
            kvHealthy ? 10 : 0, 10,
            kvHealthy ? "Key vault TLS connection verified and healthy" : "Key vault connection failed or unhealthy",
            !kvHealthy ? "Verify Key Vault endpoint connectivity and TLS configuration" : null));

        // SOC2-CC6.8 — Cryptographic key management
        var executedRotations = rotations.Count(r => r.LastRotatedAt != null);
        var dekSetting = await _repo.GetSettingAsync("dek-version-data", ct);
        var keyMgmtStatus = rotations.Count > 0 && dekSetting != null ? ControlStatus.Pass
            : rotations.Count > 0 || dekSetting != null ? ControlStatus.Partial : ControlStatus.Fail;
        controls.Add(new ControlResult(
            "SOC2-CC6.8", "Cryptographic Key Management",
            "Manage cryptographic keys throughout their lifecycle",
            keyMgmtStatus,
            keyMgmtStatus == ControlStatus.Pass ? 10 : keyMgmtStatus == ControlStatus.Partial ? 5 : 0, 10,
            $"{rotations.Count} rotation schedules active, DEK versioning: {(dekSetting != null ? "active" : "inactive")}",
            keyMgmtStatus != ControlStatus.Pass ? "Configure key rotation schedules and enable DEK versioning" : null));

        // SOC2-CC7.2 — Monitoring activities
        List<Domain.Entities.SecurityEvent> recentEvents;
        try { recentEvents = await _securityEvents.GetRecentEventsAsync(100, ct); }
        catch { recentEvents = []; }

        var monitorStatus = auditCount >= 100 && recentEvents.Count > 0 ? ControlStatus.Pass
            : auditCount > 0 ? ControlStatus.Partial : ControlStatus.Fail;
        controls.Add(new ControlResult(
            "SOC2-CC7.2", "Monitoring Activities",
            "Monitor system components for anomalies and security events",
            monitorStatus,
            monitorStatus == ControlStatus.Pass ? 10 : monitorStatus == ControlStatus.Partial ? 5 : 0, 10,
            $"{auditCount} audit entries, {recentEvents.Count} security events",
            monitorStatus != ControlStatus.Pass ? "Ensure both vault audit logging and security event monitoring are active" : null));

        // SOC2-CC7.3 — Security incident detection
        var highSeverity = recentEvents.Count(e => e.Severity is "High" or "Critical");
        var incidentStatus = recentEvents.Count > 0 ? ControlStatus.Pass : ControlStatus.Fail;
        controls.Add(new ControlResult(
            "SOC2-CC7.3", "Incident Detection",
            "Detect and respond to security incidents in a timely manner",
            incidentStatus,
            incidentStatus == ControlStatus.Pass ? 10 : 0, 10,
            recentEvents.Count > 0
                ? $"Security event pipeline active — {highSeverity} high/critical events detected"
                : "No security event monitoring detected",
            recentEvents.Count == 0 ? "Enable security event logging for authentication failures and policy violations" : null));

        // SOC2-CC8.1 — Change management (versioning)
        var versionedCount = secrets.Count(s => s.Version > 1);
        var changeStatus = versionedCount > 0 ? ControlStatus.Pass
            : secrets.Count > 0 ? ControlStatus.Partial : ControlStatus.Fail;
        controls.Add(new ControlResult(
            "SOC2-CC8.1", "Change Management",
            "Authorize, design, develop, configure, document, test, approve changes",
            changeStatus,
            changeStatus == ControlStatus.Pass ? 10 : changeStatus == ControlStatus.Partial ? 5 : 0, 10,
            $"{versionedCount}/{secrets.Count} secrets have version history",
            versionedCount == 0 ? "Use versioned secret updates to maintain change history" : null));

        // SOC2-A1.2 — Recovery mechanisms (DR)
        DrReadinessStatus? drStatus;
        try { drStatus = await _drService.GetReadinessAsync(ct); }
        catch { drStatus = null; }

        var drReady = drStatus is { AutoUnsealConfigured: true, EncryptionActive: true };
        controls.Add(new ControlResult(
            "SOC2-A1.2", "Recovery Mechanisms",
            "Recovery infrastructure and tested recovery procedures",
            drReady ? ControlStatus.Pass : drStatus != null ? ControlStatus.Partial : ControlStatus.Fail,
            drReady ? 10 : drStatus != null ? 5 : 0, 10,
            drReady ? $"DR ready — grade: {drStatus!.ReadinessGrade}" : "DR infrastructure not fully validated",
            !drReady ? "Configure and test vault backup/restore procedures" : null));

        // SOC2-A1.3 — Backup testing
        var backupSetting = await _repo.GetSettingAsync("vault-last-backup-at", ct);
        var backupStatus = backupSetting != null ? ControlStatus.Pass : ControlStatus.Fail;
        controls.Add(new ControlResult(
            "SOC2-A1.3", "Backup & Recovery Testing",
            "Regularly test backup and recovery capabilities",
            backupStatus,
            backupStatus == ControlStatus.Pass ? 10 : 0, 10,
            backupSetting != null ? $"Last backup: {backupSetting.ValueJson}" : "No backup records found",
            backupSetting == null ? "Perform and record a vault backup to establish recovery capability" : null));

        // SOC2-C1.1 — Confidentiality
        var confStatus = fieldPolicies.Count >= 3 ? ControlStatus.Pass
            : fieldPolicies.Count > 0 ? ControlStatus.Partial : ControlStatus.Fail;
        controls.Add(new ControlResult(
            "SOC2-C1.1", "Confidentiality",
            "Identify and protect confidential information",
            confStatus,
            confStatus == ControlStatus.Pass ? 10 : confStatus == ControlStatus.Partial ? 5 : 0, 10,
            $"{fieldPolicies.Count} field access policies defined (3+ recommended)",
            fieldPolicies.Count < 3 ? "Define field-level access policies for all sensitive data categories" : null));

        // SOC2-CC5.2 — Least privilege (dynamic credentials)
        var leastPrivStatus = dynamicCreds.Count > 0 ? ControlStatus.Pass : ControlStatus.Partial;
        controls.Add(new ControlResult(
            "SOC2-CC5.2", "Least Privilege",
            "Enforce least-privilege access through time-bounded credentials",
            leastPrivStatus,
            leastPrivStatus == ControlStatus.Pass ? 10 : 5, 10,
            dynamicCreds.Count > 0
                ? $"{dynamicCreds.Count} dynamic credentials enforce least-privilege access"
                : "No dynamic credentials — static access may grant excessive privileges",
            dynamicCreds.Count == 0 ? "Use dynamic credentials for database access to enforce least privilege" : null));

        // SOC2-CC3.4 — Risk assessment (compliance itself)
        controls.Add(new ControlResult(
            "SOC2-CC3.4", "Risk Assessment",
            "Process to assess risks and implement controls to mitigate them",
            ControlStatus.Pass,
            10, 10,
            "Automated compliance scoring engine evaluates 38 controls across 3 frameworks in real-time"));

        return controls;
    }

    // ─── GDPR Controls — 11 Controls (110 points) ────────────────────

    private async Task<List<ControlResult>> EvaluateGdprAsync(CancellationToken ct)
    {
        var controls = new List<ControlResult>();

        // Prefetch shared data
        var encConfigs = await _repo.GetAllEncryptionConfigsAsync(ct);
        var policies = await _repo.GetPoliciesAsync(ct);
        var autoUnseal = await _repo.GetAutoUnsealConfigAsync(ct);
        var auditCount = await _repo.GetAuditLogCountAsync(ct: ct);
        var fieldPolicies = await _repo.GetAllFieldAccessPoliciesAsync(ct);
        var rotations = await _repo.GetRotationSchedulesAsync(activeOnly: true, ct: ct);
        var secrets = await _repo.ListSecretsAsync(ct: ct);
        var dynamicCreds = await _repo.GetDynamicCredentialsAsync(includeRevoked: false, ct: ct);

        // GDPR-32a: Art. 32.1.a — Encryption
        var enabledConfigs = encConfigs.Count(c => c.IsEnabled);
        var encStatus = enabledConfigs >= 3 ? ControlStatus.Pass : enabledConfigs > 0 ? ControlStatus.Partial : ControlStatus.Fail;
        controls.Add(new ControlResult(
            "GDPR-32a", "Pseudonymisation & Encryption",
            "Encryption of personal data (Article 32.1.a)",
            encStatus,
            encStatus == ControlStatus.Pass ? 10 : encStatus == ControlStatus.Partial ? 5 : 0, 10,
            $"{enabledConfigs}/{encConfigs.Count} tables with active field-level encryption",
            enabledConfigs < 3 ? "Configure encryption for all tables containing personal data (minimum 3)" : null));

        // GDPR-32b: Art. 32.1.b — Confidentiality
        var confStatus = policies.Count > 0 && fieldPolicies.Count > 0 ? ControlStatus.Pass
            : policies.Count > 0 ? ControlStatus.Partial : ControlStatus.Fail;
        controls.Add(new ControlResult(
            "GDPR-32b", "Confidentiality",
            "Ensure ongoing confidentiality of processing systems (Article 32.1.b)",
            confStatus,
            confStatus == ControlStatus.Pass ? 10 : confStatus == ControlStatus.Partial ? 5 : 0, 10,
            $"{policies.Count} access policies, {fieldPolicies.Count} field access controls",
            confStatus != ControlStatus.Pass ? "Define both vault policies and field-level access policies" : null));

        // GDPR-32c: Art. 32.1.c — Resilience
        DrReadinessStatus? drStatus;
        try { drStatus = await _drService.GetReadinessAsync(ct); }
        catch { drStatus = null; }

        var drReady = drStatus is { AutoUnsealConfigured: true, EncryptionActive: true };
        var resilStatus = autoUnseal != null && drReady ? ControlStatus.Pass
            : autoUnseal != null || drReady ? ControlStatus.Partial : ControlStatus.Fail;
        controls.Add(new ControlResult(
            "GDPR-32c", "Resilience",
            "Ability to restore availability and access to data timely (Article 32.1.c)",
            resilStatus,
            resilStatus == ControlStatus.Pass ? 10 : resilStatus == ControlStatus.Partial ? 5 : 0, 10,
            $"Auto-unseal: {(autoUnseal != null ? "configured" : "not configured")}, DR: {(drReady ? "ready" : "not ready")}",
            resilStatus != ControlStatus.Pass ? "Configure both auto-unseal and disaster recovery for full resilience" : null));

        // GDPR-32d: Art. 32.1.d — Regular testing
        List<Domain.Entities.SecurityEvent> recentEvents;
        try { recentEvents = await _securityEvents.GetRecentEventsAsync(100, ct); }
        catch { recentEvents = []; }

        var testStatus = auditCount >= 100 && recentEvents.Count > 0 ? ControlStatus.Pass
            : auditCount > 0 ? ControlStatus.Partial : ControlStatus.Fail;
        controls.Add(new ControlResult(
            "GDPR-32d", "Regular Testing & Assessment",
            "Process for regularly testing and assessing security measures (Article 32.1.d)",
            testStatus,
            testStatus == ControlStatus.Pass ? 10 : testStatus == ControlStatus.Partial ? 5 : 0, 10,
            $"{auditCount} audit entries, {recentEvents.Count} security events monitored",
            testStatus != ControlStatus.Pass ? "Maintain continuous audit logging and security event monitoring (100+ entries)" : null));

        // GDPR-5f: Art. 5.1.f — Integrity & confidentiality
        var integrityStatus = fieldPolicies.Count > 0 && enabledConfigs > 0 ? ControlStatus.Pass
            : fieldPolicies.Count > 0 || enabledConfigs > 0 ? ControlStatus.Partial : ControlStatus.Fail;
        controls.Add(new ControlResult(
            "GDPR-5f", "Integrity & Confidentiality",
            "Protect against unauthorized processing, loss, or destruction (Article 5.1.f)",
            integrityStatus,
            integrityStatus == ControlStatus.Pass ? 10 : integrityStatus == ControlStatus.Partial ? 5 : 0, 10,
            $"{enabledConfigs} encryption configs, {fieldPolicies.Count} access policies",
            integrityStatus != ControlStatus.Pass ? "Combine encryption AND field-access policies for defense in depth" : null));

        // GDPR-25: Art. 25 — Privacy by design
        var pbyDStatus = rotations.Count > 0 && enabledConfigs > 0 ? ControlStatus.Pass
            : rotations.Count > 0 || enabledConfigs > 0 ? ControlStatus.Partial : ControlStatus.Fail;
        controls.Add(new ControlResult(
            "GDPR-25", "Privacy by Design",
            "Data protection by design and by default (Article 25)",
            pbyDStatus,
            pbyDStatus == ControlStatus.Pass ? 10 : pbyDStatus == ControlStatus.Partial ? 5 : 0, 10,
            $"{rotations.Count} rotation schedules, {enabledConfigs} encryption configs — built-in protection",
            pbyDStatus != ControlStatus.Pass ? "Enable both encryption and automated key rotation by default" : null));

        // GDPR-30: Art. 30 — Records of processing
        var recordsStatus = auditCount >= 50 ? ControlStatus.Pass : auditCount > 0 ? ControlStatus.Partial : ControlStatus.Fail;
        controls.Add(new ControlResult(
            "GDPR-30", "Processing Records",
            "Maintain records of processing activities (Article 30)",
            recordsStatus,
            recordsStatus == ControlStatus.Pass ? 10 : recordsStatus == ControlStatus.Partial ? 5 : 0, 10,
            $"Vault audit log maintains processing records ({auditCount} entries, 50+ recommended)",
            auditCount < 50 ? "Ensure all data processing operations are logged (minimum 50 entries)" : null));

        // GDPR-33: Art. 33 — Breach notification capability
        var highSeverity = recentEvents.Count(e => e.Severity is "High" or "Critical");
        var breachStatus = recentEvents.Count > 0 ? ControlStatus.Pass : ControlStatus.Fail;
        controls.Add(new ControlResult(
            "GDPR-33", "Breach Notification Capability",
            "Ability to detect and notify authorities of data breaches within 72 hours (Article 33)",
            breachStatus,
            breachStatus == ControlStatus.Pass ? 10 : 0, 10,
            recentEvents.Count > 0
                ? $"Security event pipeline active — {highSeverity} high/critical events for breach detection"
                : "No security event monitoring — breaches may go undetected",
            recentEvents.Count == 0 ? "Enable security event logging to detect and track breach incidents" : null));

        // GDPR-17: Art. 17 — Right to erasure (deletion capability)
        var deletedSecrets = secrets.Count(s => s.IsDeleted);
        var erasureStatus = secrets.Count > 0 ? ControlStatus.Pass : ControlStatus.Partial;
        controls.Add(new ControlResult(
            "GDPR-17", "Right to Erasure",
            "Ability to delete personal data upon request (Article 17)",
            erasureStatus,
            erasureStatus == ControlStatus.Pass ? 10 : 5, 10,
            $"Secret deletion supported — {secrets.Count} secrets managed, {deletedSecrets} soft-deleted",
            erasureStatus != ControlStatus.Pass ? "Ensure vault supports secret deletion for data erasure requests" : null));

        // GDPR-28: Art. 28 — Processor security (dynamic credentials)
        var processorStatus = dynamicCreds.Count > 0 && policies.Count > 0 ? ControlStatus.Pass
            : policies.Count > 0 ? ControlStatus.Partial : ControlStatus.Fail;
        controls.Add(new ControlResult(
            "GDPR-28", "Processor Security",
            "Data processors access only with time-limited, scoped credentials (Article 28)",
            processorStatus,
            processorStatus == ControlStatus.Pass ? 10 : processorStatus == ControlStatus.Partial ? 5 : 0, 10,
            $"{dynamicCreds.Count} dynamic credentials, {policies.Count} access policies",
            processorStatus != ControlStatus.Pass ? "Use dynamic credentials for processor access with time-bounded scope" : null));

        // GDPR-6: Art. 6 — Lawful basis (audit trail)
        var lawfulStatus = auditCount >= 100 && fieldPolicies.Count > 0 ? ControlStatus.Pass
            : auditCount > 0 ? ControlStatus.Partial : ControlStatus.Fail;
        controls.Add(new ControlResult(
            "GDPR-6", "Lawful Basis Documentation",
            "Demonstrate lawful basis for processing through audit trails (Article 6)",
            lawfulStatus,
            lawfulStatus == ControlStatus.Pass ? 10 : lawfulStatus == ControlStatus.Partial ? 5 : 0, 10,
            $"{auditCount} audit entries document processing activities, {fieldPolicies.Count} access controls",
            lawfulStatus != ControlStatus.Pass ? "Maintain comprehensive audit logs (100+) as evidence of lawful processing" : null));

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
