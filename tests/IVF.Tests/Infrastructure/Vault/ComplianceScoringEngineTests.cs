using FluentAssertions;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace IVF.Tests.Infrastructure.Vault;

public class ComplianceScoringEngineTests
{
    private readonly Mock<IVaultRepository> _repoMock;
    private readonly Mock<IKeyVaultService> _kvMock;
    private readonly Mock<ISecurityEventService> _secEventsMock;
    private readonly Mock<IVaultDrService> _drMock;
    private readonly ComplianceScoringEngine _sut;

    public ComplianceScoringEngineTests()
    {
        _repoMock = new Mock<IVaultRepository>();
        _kvMock = new Mock<IKeyVaultService>();
        _secEventsMock = new Mock<ISecurityEventService>();
        _drMock = new Mock<IVaultDrService>();
        var loggerMock = new Mock<ILogger<ComplianceScoringEngine>>();

        // Defaults — everything healthy/populated
        _kvMock.Setup(k => k.IsHealthyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _repoMock.Setup(r => r.GetAllEncryptionConfigsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EncryptionConfig>
            {
                CreateEncryptionConfig("patients"),
                CreateEncryptionConfig("couples"),
                CreateEncryptionConfig("treatment_cycles"),
            });
        _repoMock.Setup(r => r.GetAuditLogCountAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(500);
        _repoMock.Setup(r => r.ListSecretsAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultSecret> { CreateSecret(version: 2) });
        _repoMock.Setup(r => r.GetPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultPolicy> { CreatePolicy() });
        _repoMock.Setup(r => r.GetUserPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultUserPolicy>
            {
                CreateUserPolicy(), CreateUserPolicy(), CreateUserPolicy(),
            });
        _repoMock.Setup(r => r.GetTokensAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultToken>());
        _repoMock.Setup(r => r.GetRotationSchedulesAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SecretRotationSchedule> { CreateRotationSchedule(executed: true) });
        _repoMock.Setup(r => r.GetAllFieldAccessPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FieldAccessPolicy>
            {
                CreateFieldPolicy("patients", "ssn"),
                CreateFieldPolicy("patients", "phone"),
                CreateFieldPolicy("couples", "notes"),
            });
        _repoMock.Setup(r => r.GetAutoUnsealConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(VaultAutoUnseal.Create("wrapped-key", "https://kv.vault.azure.net", "test-key"));
        _repoMock.Setup(r => r.GetLeasesAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultLease> { CreateLease() });
        _repoMock.Setup(r => r.GetDynamicCredentialsAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultDynamicCredential> { CreateDynamicCredential() });
        _repoMock.Setup(r => r.GetSettingAsync("dek-version-data", It.IsAny<CancellationToken>()))
            .ReturnsAsync(VaultSetting.Create("dek-version-data", "{}"));
        _repoMock.Setup(r => r.GetSettingAsync("vault-last-backup-at", It.IsAny<CancellationToken>()))
            .ReturnsAsync(VaultSetting.Create("vault-last-backup-at", "\"2026-03-01T00:00:00Z\""));

        _secEventsMock.Setup(s => s.GetRecentEventsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SecurityEvent> { CreateSecurityEvent() });
        _drMock.Setup(d => d.GetReadinessAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DrReadinessStatus(true, true, 5, 3, DateTime.UtcNow.AddHours(-1), "A"));

        _sut = new ComplianceScoringEngine(
            _repoMock.Object, _kvMock.Object,
            _secEventsMock.Object, _drMock.Object,
            loggerMock.Object);
    }

    // ─── Full Report Tests ──────────────────────────────

    [Fact]
    public async Task Evaluate_HealthySystem_ReturnsHighScore()
    {
        var report = await _sut.EvaluateAsync();

        report.Percentage.Should().BeGreaterThan(80);
        report.Grade.Should().BeOneOf("A+", "A", "A-", "B+");
        report.Frameworks.Should().HaveCount(3);
    }

    [Fact]
    public async Task Evaluate_ContainsAllThreeFrameworks()
    {
        var report = await _sut.EvaluateAsync();

        report.Frameworks.Select(f => f.Framework)
            .Should().Contain(new[]
            {
                ComplianceFramework.Hipaa,
                ComplianceFramework.Soc2,
                ComplianceFramework.Gdpr,
            });
    }

    [Fact]
    public async Task Evaluate_OverallScore_SumsFrameworks()
    {
        var report = await _sut.EvaluateAsync();

        report.OverallScore.Should().Be(report.Frameworks.Sum(f => f.Score));
        report.MaxScore.Should().Be(report.Frameworks.Sum(f => f.MaxScore));
    }

    [Fact]
    public async Task Evaluate_MaxScore_Is380()
    {
        var report = await _sut.EvaluateAsync();

        // 15 HIPAA + 12 SOC2 + 11 GDPR = 38 controls × 10 pts
        report.MaxScore.Should().Be(380);
    }

    // ─── HIPAA Tests ────────────────────────────────────

    [Fact]
    public async Task Hipaa_AllControlsPresent()
    {
        var result = await _sut.EvaluateFrameworkAsync(ComplianceFramework.Hipaa);

        result.Controls.Should().HaveCount(15);
        result.MaxScore.Should().Be(150);
    }

    [Fact]
    public async Task Hipaa_HealthySystem_HighScore()
    {
        var result = await _sut.EvaluateFrameworkAsync(ComplianceFramework.Hipaa);

        result.Percentage.Should().BeGreaterThan(80);
    }

    [Fact]
    public async Task Hipaa_NoEncryption_FailsControl()
    {
        _repoMock.Setup(r => r.GetAllEncryptionConfigsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EncryptionConfig>());

        var result = await _sut.EvaluateFrameworkAsync(ComplianceFramework.Hipaa);

        var enc = result.Controls.First(c => c.ControlId == "HIPAA-1");
        enc.Status.Should().Be(ControlStatus.Fail);
        enc.Score.Should().Be(0);
        enc.Remediation.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Hipaa_NoAuditLogs_FailsAuditControl()
    {
        _repoMock.Setup(r => r.GetAuditLogCountAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _sut.EvaluateFrameworkAsync(ComplianceFramework.Hipaa);

        var audit = result.Controls.First(c => c.ControlId == "HIPAA-2");
        audit.Status.Should().Be(ControlStatus.Fail);
    }

    [Fact]
    public async Task Hipaa_KvUnhealthy_FailsTransmissionSecurity()
    {
        _kvMock.Setup(k => k.IsHealthyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _sut.EvaluateFrameworkAsync(ComplianceFramework.Hipaa);

        var tx = result.Controls.First(c => c.ControlId == "HIPAA-5");
        tx.Status.Should().Be(ControlStatus.Fail);
    }

    [Fact]
    public async Task Hipaa_LeaseManagement_FixedBug_NotAlwaysPass()
    {
        // Verify HIPAA-10 no longer always passes (was: leases.Count >= 0)
        _repoMock.Setup(r => r.GetLeasesAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultLease>());

        var result = await _sut.EvaluateFrameworkAsync(ComplianceFramework.Hipaa);

        var lease = result.Controls.First(c => c.ControlId == "HIPAA-10");
        lease.Status.Should().Be(ControlStatus.Partial);
        lease.Score.Should().Be(5);
    }

    [Fact]
    public async Task Hipaa_SecurityIncidents_RequiresEvents()
    {
        _secEventsMock.Setup(s => s.GetRecentEventsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SecurityEvent>());

        var result = await _sut.EvaluateFrameworkAsync(ComplianceFramework.Hipaa);

        var incident = result.Controls.First(c => c.ControlId == "HIPAA-13");
        incident.Status.Should().Be(ControlStatus.Fail);
    }

    [Fact]
    public async Task Hipaa_ContingencyPlan_ChecksDR()
    {
        var result = await _sut.EvaluateFrameworkAsync(ComplianceFramework.Hipaa);

        var dr = result.Controls.First(c => c.ControlId == "HIPAA-14");
        dr.Status.Should().Be(ControlStatus.Pass);
    }

    // ─── SOC 2 Tests ────────────────────────────────────

    [Fact]
    public async Task Soc2_AllControlsPresent()
    {
        var result = await _sut.EvaluateFrameworkAsync(ComplianceFramework.Soc2);

        result.Controls.Should().HaveCount(12);
        result.MaxScore.Should().Be(120);
    }

    [Fact]
    public async Task Soc2_HealthySystem_HighScore()
    {
        var result = await _sut.EvaluateFrameworkAsync(ComplianceFramework.Soc2);

        result.Percentage.Should().BeGreaterThan(80);
    }

    [Fact]
    public async Task Soc2_LowAuditCount_PartialMonitoring()
    {
        _repoMock.Setup(r => r.GetAuditLogCountAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(50);

        var result = await _sut.EvaluateFrameworkAsync(ComplianceFramework.Soc2);

        var monitoring = result.Controls.First(c => c.ControlId == "SOC2-CC7.2");
        monitoring.Status.Should().Be(ControlStatus.Partial);
        monitoring.Score.Should().Be(5);
    }

    [Fact]
    public async Task Soc2_IncludesRiskAssessment_AlwaysPass()
    {
        var result = await _sut.EvaluateFrameworkAsync(ComplianceFramework.Soc2);

        var risk = result.Controls.First(c => c.ControlId == "SOC2-CC3.4");
        risk.Status.Should().Be(ControlStatus.Pass);
    }

    // ─── GDPR Tests ─────────────────────────────────────

    [Fact]
    public async Task Gdpr_AllControlsPresent()
    {
        var result = await _sut.EvaluateFrameworkAsync(ComplianceFramework.Gdpr);

        result.Controls.Should().HaveCount(11);
        result.MaxScore.Should().Be(110);
    }

    [Fact]
    public async Task Gdpr_HealthySystem_HighScore()
    {
        var result = await _sut.EvaluateFrameworkAsync(ComplianceFramework.Gdpr);

        result.Percentage.Should().BeGreaterThan(80);
    }

    [Fact]
    public async Task Gdpr_NoEncryption_FailsPseudonymisation()
    {
        _repoMock.Setup(r => r.GetAllEncryptionConfigsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EncryptionConfig>());

        var result = await _sut.EvaluateFrameworkAsync(ComplianceFramework.Gdpr);

        var enc = result.Controls.First(c => c.ControlId == "GDPR-32a");
        enc.Status.Should().Be(ControlStatus.Fail);
    }

    [Fact]
    public async Task Gdpr_BreachNotification_RequiresSecurityEvents()
    {
        _secEventsMock.Setup(s => s.GetRecentEventsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SecurityEvent>());

        var result = await _sut.EvaluateFrameworkAsync(ComplianceFramework.Gdpr);

        var breach = result.Controls.First(c => c.ControlId == "GDPR-33");
        breach.Status.Should().Be(ControlStatus.Fail);
    }

    [Fact]
    public async Task Gdpr_RightToErasure_PassesWithSecrets()
    {
        var result = await _sut.EvaluateFrameworkAsync(ComplianceFramework.Gdpr);

        var erasure = result.Controls.First(c => c.ControlId == "GDPR-17");
        erasure.Status.Should().Be(ControlStatus.Pass);
    }

    // ─── Remediation Tests ──────────────────────────────

    [Fact]
    public async Task FailedControls_HaveRemediation()
    {
        _repoMock.Setup(r => r.GetAllEncryptionConfigsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EncryptionConfig>());

        var report = await _sut.EvaluateAsync();

        var failedControls = report.Frameworks
            .SelectMany(f => f.Controls)
            .Where(c => c.Status == ControlStatus.Fail);

        failedControls.Should().AllSatisfy(c => c.Remediation.Should().NotBeNullOrEmpty());
    }

    // ─── Grade Calculation ──────────────────────────────

    [Fact]
    public async Task Evaluate_EmptySystem_LowGrade()
    {
        _repoMock.Setup(r => r.GetAllEncryptionConfigsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EncryptionConfig>());
        _repoMock.Setup(r => r.GetAuditLogCountAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repoMock.Setup(r => r.ListSecretsAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultSecret>());
        _repoMock.Setup(r => r.GetPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultPolicy>());
        _repoMock.Setup(r => r.GetUserPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultUserPolicy>());
        _repoMock.Setup(r => r.GetTokensAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultToken>());
        _repoMock.Setup(r => r.GetRotationSchedulesAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SecretRotationSchedule>());
        _repoMock.Setup(r => r.GetAllFieldAccessPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FieldAccessPolicy>());
        _repoMock.Setup(r => r.GetAutoUnsealConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((VaultAutoUnseal?)null);
        _repoMock.Setup(r => r.GetLeasesAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultLease>());
        _repoMock.Setup(r => r.GetDynamicCredentialsAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultDynamicCredential>());
        _repoMock.Setup(r => r.GetSettingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VaultSetting?)null);
        _kvMock.Setup(k => k.IsHealthyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _secEventsMock.Setup(s => s.GetRecentEventsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SecurityEvent>());
        _drMock.Setup(d => d.GetReadinessAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DR not configured"));

        var report = await _sut.EvaluateAsync();

        report.Percentage.Should().BeLessThan(50);
        report.Grade.Should().BeOneOf("D", "F");
    }

    // ─── Helpers ────────────────────────────────────────

    private static EncryptionConfig CreateEncryptionConfig(string table = "patients")
    {
        return EncryptionConfig.Create(table, new[] { "ssn", "phone" }, "data");
    }

    private static VaultSecret CreateSecret(int version = 1)
    {
        return VaultSecret.Create("secret/test", "encrypted-value", "iv-value", version: version);
    }

    private static VaultPolicy CreatePolicy()
    {
        return VaultPolicy.Create("admin-policy", "secret/*", new[] { "read", "write" });
    }

    private static VaultUserPolicy CreateUserPolicy()
    {
        return VaultUserPolicy.Create(Guid.NewGuid(), Guid.NewGuid());
    }

    private static FieldAccessPolicy CreateFieldPolicy(string table = "patients", string field = "ssn")
    {
        return FieldAccessPolicy.Create(table, field, "Doctor", "full");
    }

    private static SecretRotationSchedule CreateRotationSchedule(bool executed = false)
    {
        var schedule = SecretRotationSchedule.Create("secret/db-password", 30);
        return schedule;
    }

    private static VaultLease CreateLease()
    {
        return VaultLease.Create(Guid.NewGuid(), 3600, true);
    }

    private static VaultDynamicCredential CreateDynamicCredential()
    {
        return VaultDynamicCredential.Create(
            "postgres", "dyn_user", "localhost", 5433, "ivf_db",
            "postgres", "encrypted-admin-pwd", 3600);
    }

    private static SecurityEvent CreateSecurityEvent()
    {
        return SecurityEvent.Create(
            "AUTH_LOGIN_SUCCESS", "Info",
            userId: Guid.NewGuid(), username: "admin",
            ipAddress: "127.0.0.1");
    }
}
