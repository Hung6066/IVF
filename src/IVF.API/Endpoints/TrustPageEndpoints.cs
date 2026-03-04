using IVF.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IVF.API.Endpoints;

public static class TrustPageEndpoints
{
    public static void MapTrustPageEndpoints(this WebApplication app)
    {
        // Public endpoint — no authentication required
        var group = app.MapGroup("/api/trust")
            .WithTags("Trust Page (Public)")
            .AllowAnonymous();

        group.MapGet("/", async (IvfDbContext db) =>
        {
            // Return sanitized, public-safe compliance posture data
            // No sensitive counts, no internal details — only compliance status
            var trainings = await db.ComplianceTrainings
                .Where(t => !t.IsDeleted)
                .Select(t => new { t.IsCompleted })
                .ToListAsync();

            var biasTests = await db.AiBiasTestResults
                .Where(b => !b.IsDeleted)
                .Select(b => new { b.PassesFairnessThreshold, b.CreatedAt })
                .ToListAsync();

            var modelVersions = await db.AiModelVersions
                .Where(m => !m.IsDeleted)
                .Select(m => new { m.AiSystemName, m.Status })
                .ToListAsync();

            var incidents = await db.SecurityIncidents
                .Where(i => !i.IsDeleted)
                .Select(i => new { i.Status })
                .ToListAsync();

            var trainingRate = trainings.Count == 0 ? 100.0
                : Math.Round((double)trainings.Count(t => t.IsCompleted) / trainings.Count * 100, 0);

            var biasPassRate = biasTests.Count == 0 ? 100.0
                : Math.Round((double)biasTests.Count(b => b.PassesFairnessThreshold) / biasTests.Count * 100, 0);

            var resolvedRate = incidents.Count == 0 ? 100.0
                : Math.Round((double)incidents.Count(i => i.Status is "Resolved" or "Closed") / incidents.Count * 100, 0);

            return Results.Ok(new
            {
                companyName = "IVF Information System",
                lastUpdated = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                frameworks = new object[]
                {
                    new { id = "soc2", name = "SOC 2 Type II", score = 90, status = "Audit Ready", icon = "shield", description = "Security, Availability, Processing Integrity, Confidentiality, Privacy" },
                    new { id = "iso27001", name = "ISO 27001:2022", score = 90, status = "Audit Ready", icon = "lock", description = "Information Security Management System (ISMS)" },
                    new { id = "hipaa", name = "HIPAA", score = 94, status = "Compliant", icon = "heart", description = "Health Insurance Portability and Accountability Act" },
                    new { id = "gdpr", name = "GDPR", score = 92, status = "Compliant", icon = "globe", description = "General Data Protection Regulation (EU)" },
                    new { id = "hitrust", name = "HITRUST CSF v11", score = 84, status = "Maturing", icon = "award", description = "Health Information Trust Alliance — Healthcare Gold Standard" },
                    new { id = "nist_ai", name = "NIST AI RMF 1.0", score = 84, status = "On Track", icon = "cpu", description = "AI Risk Management Framework" },
                    new { id = "iso42001", name = "ISO 42001:2023", score = 80, status = "Developing", icon = "brain", description = "Artificial Intelligence Management System" },
                },
                securityControls = new object[]
                {
                    new { category = "Authentication", items = new[] { "JWT RS256 (3072-bit keys)", "Multi-Factor Authentication (TOTP/SMS)", "WebAuthn / Passkey", "Biometric (Fingerprint)", "Account lockout (5 failed attempts)" } },
                    new { category = "Encryption", items = new[] { "AES-256-GCM field-level encryption", "TLS 1.3 in transit", "mTLS for inter-service communication", "RSA-3072 digital signatures", "Azure Key Vault envelope encryption" } },
                    new { category = "Access Control", items = new[] { "Role-Based Access Control (9 roles)", "Conditional Access Policies", "Zero Trust Architecture", "Device fingerprinting & binding", "Impersonation with dual-approval" } },
                    new { category = "Data Protection", items = new[] { "PII masking (SSN, email, phone)", "Data retention policies (HIPAA/GDPR)", "Right to erasure (GDPR Art. 17)", "Consent enforcement middleware", "Pseudonymization procedures" } },
                    new { category = "Monitoring", items = new[] { "50+ security event types", "Real-time threat detection (7 signals)", "Behavioral analytics (z-score)", "MITRE ATT&CK mapping", "Automated incident response" } },
                    new { category = "Infrastructure", items = new[] { "3-2-1 backup strategy", "PostgreSQL streaming replication", "Point-in-time recovery (14 days)", "Docker network segmentation", "Certificate Authority (internal PKI)" } },
                },
                metrics = new
                {
                    trainingComplianceRate = trainingRate,
                    aiBiasFairnessRate = biasPassRate,
                    incidentResolutionRate = resolvedRate,
                    encryptionAtRest = "AES-256-GCM",
                    encryptionInTransit = "TLS 1.3",
                    mfaEnforced = true,
                    zeroTrustEnabled = true,
                    uptimeSla = "99.9%",
                    backupFrequency = "Daily + WAL continuous",
                    dataRetention = "Per HIPAA/GDPR policy",
                },
                certifications = new object[]
                {
                    new { name = "HIPAA Self-Assessment", status = "completed", date = "2026-03" },
                    new { name = "GDPR Compliance", status = "completed", date = "2026-03" },
                    new { name = "SOC 2 Type II", status = "ready", date = "Audit ready" },
                    new { name = "ISO 27001:2022", status = "ready", date = "Stage 1/2 ready" },
                },
                documents = new object[]
                {
                    new { name = "Information Security Policy", available = true },
                    new { name = "Data Processing Impact Assessment (DPIA)", available = true },
                    new { name = "Records of Processing Activities (RoPA)", available = true },
                    new { name = "Business Continuity Plan (BCP/DRP)", available = true },
                    new { name = "Incident Response Procedures", available = true },
                    new { name = "AI Governance Charter", available = true },
                    new { name = "Vendor Risk Assessment", available = true },
                    new { name = "Privacy Notice", available = true },
                    new { name = "Penetration Test Report", available = true },
                    new { name = "Risk Assessment Report", available = true },
                },
                contact = new
                {
                    securityEmail = "security@ivf-system.com",
                    dpoEmail = "dpo@ivf-system.com",
                    reportVulnerability = "security@ivf-system.com",
                }
            });
        }).WithName("GetTrustPage");
    }
}
