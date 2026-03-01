using FluentAssertions;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using IVF.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace IVF.Tests.Infrastructure.Vault;

public class VaultDrServiceTests
{
    private readonly Mock<IVaultRepository> _repoMock;
    private readonly Mock<ISecurityEventPublisher> _eventsMock;
    private readonly VaultDrService _sut;
    private const string BackupKey = "test-backup-key-for-dr-2025!";

    public VaultDrServiceTests()
    {
        _repoMock = new Mock<IVaultRepository>();
        _eventsMock = new Mock<ISecurityEventPublisher>();
        var loggerMock = new Mock<ILogger<VaultDrService>>();

        SetupDefaults();

        _sut = new VaultDrService(_repoMock.Object, _eventsMock.Object, loggerMock.Object);
    }

    private void SetupDefaults()
    {
        _repoMock.Setup(r => r.ListSecretsAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultSecret>
            {
                VaultSecret.Create("secret/db", "enc1", "iv1"),
                VaultSecret.Create("secret/api", "enc2", "iv2"),
            });
        _repoMock.Setup(r => r.GetPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultPolicy>
            {
                VaultPolicy.Create("admin", "secret/*", new[] { "read", "write" }),
            });
        _repoMock.Setup(r => r.GetAllSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultSetting>
            {
                VaultSetting.Create("test-key", "\"value\""),
            });
        _repoMock.Setup(r => r.GetAllEncryptionConfigsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EncryptionConfig>
            {
                EncryptionConfig.Create("patients", new[] { "ssn" }),
            });
        _repoMock.Setup(r => r.GetAutoUnsealConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(VaultAutoUnseal.Create("wrapped", "https://kv.test", "key1"));
    }

    // ─── Backup Tests ───────────────────────────────────

    [Fact]
    public async Task Backup_ReturnsEncryptedData()
    {
        var result = await _sut.BackupAsync(BackupKey);

        result.Success.Should().BeTrue();
        result.BackupData.Should().NotBeEmpty();
        result.BackupId.Should().StartWith("vault-backup-");
        result.SecretsCount.Should().Be(2);
        result.PoliciesCount.Should().Be(1);
        result.SettingsCount.Should().Be(1);
        result.EncryptionConfigsCount.Should().Be(1);
        result.IntegrityHash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Backup_PublishesSiemEvent()
    {
        await _sut.BackupAsync(BackupKey);

        _eventsMock.Verify(e => e.PublishAsync(
            It.Is<VaultSecurityEvent>(v => v.EventType == "vault.backup.created"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Backup_SavesLastBackupTimestamp()
    {
        await _sut.BackupAsync(BackupKey);

        _repoMock.Verify(r => r.SaveSettingAsync(
            "vault-last-backup-at", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── Restore Tests ──────────────────────────────────

    [Fact]
    public async Task Backup_ThenRestore_RoundTrips()
    {
        // Arrange: backup
        var backup = await _sut.BackupAsync(BackupKey);

        // Setup restore: no existing data
        _repoMock.Setup(r => r.GetSecretAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VaultSecret?)null);
        _repoMock.Setup(r => r.GetPolicyByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VaultPolicy?)null);
        _repoMock.Setup(r => r.GetSettingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VaultSetting?)null);
        _repoMock.Setup(r => r.GetAllEncryptionConfigsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EncryptionConfig>());

        // Act: restore
        var result = await _sut.RestoreAsync(backup.BackupData, BackupKey);

        result.Success.Should().BeTrue();
        result.SecretsRestored.Should().Be(2);
        result.PoliciesRestored.Should().Be(1);
        result.SettingsRestored.Should().Be(1);
        result.EncryptionConfigsRestored.Should().Be(1);
    }

    [Fact]
    public async Task Restore_WrongKey_Fails()
    {
        var backup = await _sut.BackupAsync(BackupKey);

        var result = await _sut.RestoreAsync(backup.BackupData, "wrong-key");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Invalid backup key");
    }

    [Fact]
    public async Task Restore_CorruptedData_Fails()
    {
        var result = await _sut.RestoreAsync(new byte[100], BackupKey);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Restore_SkipsExistingSecrets()
    {
        var backup = await _sut.BackupAsync(BackupKey);

        // One secret already exists
        _repoMock.Setup(r => r.GetSecretAsync("secret/db", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(VaultSecret.Create("secret/db", "existing", "iv"));
        _repoMock.Setup(r => r.GetSecretAsync("secret/api", It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VaultSecret?)null);
        _repoMock.Setup(r => r.GetPolicyByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VaultPolicy?)null);
        _repoMock.Setup(r => r.GetSettingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VaultSetting?)null);
        _repoMock.Setup(r => r.GetAllEncryptionConfigsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EncryptionConfig>());

        var result = await _sut.RestoreAsync(backup.BackupData, BackupKey);

        result.SecretsRestored.Should().Be(1); // Only the missing one
    }

    // ─── Validate Tests ─────────────────────────────────

    [Fact]
    public async Task Validate_ValidBackup_Passes()
    {
        var backup = await _sut.BackupAsync(BackupKey);

        var validation = await _sut.ValidateBackupAsync(backup.BackupData, BackupKey);

        validation.Valid.Should().BeTrue();
        validation.IntegrityHash.Should().Be(backup.IntegrityHash);
    }

    [Fact]
    public async Task Validate_WrongKey_Fails()
    {
        var backup = await _sut.BackupAsync(BackupKey);

        var validation = await _sut.ValidateBackupAsync(backup.BackupData, "bad-key");

        validation.Valid.Should().BeFalse();
        validation.Error.Should().Contain("Decryption failed");
    }

    // ─── Readiness Tests ────────────────────────────────

    [Fact]
    public async Task Readiness_FullyConfigured_GradeA()
    {
        _repoMock.Setup(r => r.GetSettingAsync("vault-last-backup-at", It.IsAny<CancellationToken>()))
            .ReturnsAsync(VaultSetting.Create("vault-last-backup-at",
                System.Text.Json.JsonSerializer.Serialize(DateTime.UtcNow.AddHours(-1))));

        var status = await _sut.GetReadinessAsync();

        status.AutoUnsealConfigured.Should().BeTrue();
        status.EncryptionActive.Should().BeTrue();
        status.ReadinessGrade.Should().Be("A");
    }

    [Fact]
    public async Task Readiness_NoAutoUnseal_LowerGrade()
    {
        _repoMock.Setup(r => r.GetAutoUnsealConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((VaultAutoUnseal?)null);

        var status = await _sut.GetReadinessAsync();

        status.AutoUnsealConfigured.Should().BeFalse();
        status.ReadinessGrade.Should().NotBe("A");
    }
}
