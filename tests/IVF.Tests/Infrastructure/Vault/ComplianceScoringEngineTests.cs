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
    private readonly ComplianceScoringEngine _sut;

    public ComplianceScoringEngineTests()
    {
        _repoMock = new Mock<IVaultRepository>();
        _kvMock = new Mock<IKeyVaultService>();
        var loggerMock = new Mock<ILogger<ComplianceScoringEngine>>();

        // Defaults — everything healthy/populated
        _kvMock.Setup(k => k.IsHealthyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _repoMock.Setup(r => r.GetAllEncryptionConfigsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EncryptionConfig> { CreateEncryptionConfig() });
        _repoMock.Setup(r => r.GetAuditLogCountAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(500);
        _repoMock.Setup(r => r.ListSecretsAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultSecret> { CreateSecret(version: 2) });
        _repoMock.Setup(r => r.GetPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultPolicy> { CreatePolicy() });
        _repoMock.Setup(r => r.GetUserPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultUserPolicy> { CreateUserPolicy() });
        _repoMock.Setup(r => r.GetTokensAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultToken>());
        _repoMock.Setup(r => r.GetRotationSchedulesAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SecretRotationSchedule> { CreateRotationSchedule() });
        _repoMock.Setup(r => r.GetAllFieldAccessPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FieldAccessPolicy> { CreateFieldPolicy() });
        _repoMock.Setup(r => r.GetAutoUnsealConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(VaultAutoUnseal.Create("wrapped-key", "https://kv.vault.azure.net", "test-key"));
        _repoMock.Setup(r => r.GetLeasesAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultLease>());
        _repoMock.Setup(r => r.GetSettingAsync("dek-version-data", It.IsAny<CancellationToken>()))
            .ReturnsAsync(VaultSetting.Create("dek-version-data", "{}"));

        _sut = new ComplianceScoringEngine(_repoMock.Object, _kvMock.Object, loggerMock.Object);
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

    // ─── HIPAA Tests ────────────────────────────────────

    [Fact]
    public async Task Hipaa_AllControlsPresent_MaxScore()
    {
        var result = await _sut.EvaluateFrameworkAsync(ComplianceFramework.Hipaa);

        result.Controls.Should().HaveCount(10);
        result.Controls.Should().AllSatisfy(c => c.Status.Should().Be(ControlStatus.Pass));
        result.Score.Should().Be(result.MaxScore);
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

    // ─── SOC 2 Tests ────────────────────────────────────

    [Fact]
    public async Task Soc2_AllControlsPresent_HighScore()
    {
        var result = await _sut.EvaluateFrameworkAsync(ComplianceFramework.Soc2);

        result.Controls.Should().HaveCount(7);
        result.Score.Should().Be(result.MaxScore);
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

    // ─── GDPR Tests ─────────────────────────────────────

    [Fact]
    public async Task Gdpr_AllControlsPresent_HighScore()
    {
        var result = await _sut.EvaluateFrameworkAsync(ComplianceFramework.Gdpr);

        result.Controls.Should().HaveCount(7);
        result.Score.Should().Be(result.MaxScore);
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

    // ─── Grade Calculation ──────────────────────────────

    [Fact]
    public async Task Evaluate_EmptySystem_LowGrade()
    {
        _repoMock.Setup(r => r.GetAllEncryptionConfigsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EncryptionConfig>());
        _repoMock.Setup(r => r.GetAuditLogCountAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repoMock.Setup(r => r.GetPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultPolicy>());
        _repoMock.Setup(r => r.GetUserPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultUserPolicy>());
        _repoMock.Setup(r => r.GetRotationSchedulesAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SecretRotationSchedule>());
        _repoMock.Setup(r => r.GetAllFieldAccessPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FieldAccessPolicy>());
        _repoMock.Setup(r => r.GetAutoUnsealConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((VaultAutoUnseal?)null);
        _repoMock.Setup(r => r.GetSettingAsync("dek-version-data", It.IsAny<CancellationToken>()))
            .ReturnsAsync((VaultSetting?)null);
        _kvMock.Setup(k => k.IsHealthyAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var report = await _sut.EvaluateAsync();

        report.Percentage.Should().BeLessThan(50);
        report.Grade.Should().BeOneOf("D", "F");
    }

    // ─── Helpers ────────────────────────────────────────

    private static EncryptionConfig CreateEncryptionConfig()
    {
        return EncryptionConfig.Create("patients", new[] { "ssn", "phone" }, "data");
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

    private static FieldAccessPolicy CreateFieldPolicy()
    {
        return FieldAccessPolicy.Create("patients", "ssn", "Doctor", "full");
    }

    private static SecretRotationSchedule CreateRotationSchedule()
    {
        return SecretRotationSchedule.Create("secret/db-password", 30);
    }
}
