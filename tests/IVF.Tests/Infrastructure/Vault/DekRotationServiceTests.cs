using System.Diagnostics.Metrics;
using System.Text.Json;
using FluentAssertions;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using IVF.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace IVF.Tests.Infrastructure.Vault;

public class DekRotationServiceTests
{
    private readonly Mock<IKeyVaultService> _kvMock;
    private readonly Mock<IVaultRepository> _repoMock;
    private readonly Mock<IVaultDecryptionService> _decryptMock;
    private readonly DekRotationService _sut;

    public DekRotationServiceTests()
    {
        _kvMock = new Mock<IKeyVaultService>();
        _repoMock = new Mock<IVaultRepository>();
        _decryptMock = new Mock<IVaultDecryptionService>();
        var loggerMock = new Mock<ILogger<DekRotationService>>();
        var metrics = CreateVaultMetrics();

        _sut = new DekRotationService(
            _kvMock.Object,
            _repoMock.Object,
            _decryptMock.Object,
            metrics,
            loggerMock.Object);
    }

    private static VaultMetrics CreateVaultMetrics()
    {
        var meterFactoryMock = new Mock<IMeterFactory>();
        meterFactoryMock.Setup(f => f.Create(It.IsAny<MeterOptions>()))
            .Returns((MeterOptions opts) => new Meter(opts));
        return new VaultMetrics(meterFactoryMock.Object);
    }

    // ─── RotateDekAsync Tests ───────────────────────────

    [Fact]
    public async Task RotateDekAsync_FirstRotation_ArchivesV1AndCreatesV2()
    {
        // Arrange: existing DEK at version 1 (no version metadata yet)
        _repoMock.Setup(r => r.GetSettingAsync("dek-version-data", It.IsAny<CancellationToken>()))
            .ReturnsAsync((VaultSetting?)null);

        _kvMock.Setup(k => k.GetSecretAsync("dek-data", It.IsAny<CancellationToken>()))
            .ReturnsAsync("existingDekBase64==");

        // Act
        var result = await _sut.RotateDekAsync("data");

        // Assert
        result.Success.Should().BeTrue();
        result.DekPurpose.Should().Be("data");
        result.NewVersion.Should().Be(2);
        result.PreviousVersion.Should().Be(1);
        result.Error.Should().BeNull();

        // Should archive the old DEK as v1
        _kvMock.Verify(k => k.SetSecretAsync("dek-data-v1", "existingDekBase64==", It.IsAny<CancellationToken>()), Times.Once);

        // Should set a new DEK (32 bytes → base64)
        _kvMock.Verify(k => k.SetSecretAsync("dek-data", It.Is<string>(s => s != "existingDekBase64==" && s.Length > 0), It.IsAny<CancellationToken>()), Times.Once);

        // Should save version metadata
        _repoMock.Verify(r => r.SaveSettingAsync("dek-version-data", It.Is<string>(s => s.Contains("\"CurrentVersion\":2")), It.IsAny<CancellationToken>()), Times.Once);

        // Should audit log
        _repoMock.Verify(r => r.AddAuditLogAsync(It.Is<VaultAuditLog>(l => l.Action == "dek.rotate"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RotateDekAsync_SubsequentRotation_IncrementsVersion()
    {
        // Arrange: already at version 3
        var versionMeta = JsonSerializer.Serialize(new { CurrentVersion = 3, RotatedAt = DateTime.UtcNow, OldVersionsKept = 2 });
        var setting = VaultSetting.Create("dek-version-session", versionMeta);

        _repoMock.Setup(r => r.GetSettingAsync("dek-version-session", It.IsAny<CancellationToken>()))
            .ReturnsAsync(setting);

        _kvMock.Setup(k => k.GetSecretAsync("dek-session", It.IsAny<CancellationToken>()))
            .ReturnsAsync("currentSessionDek==");

        // Act
        var result = await _sut.RotateDekAsync("session");

        // Assert
        result.Success.Should().BeTrue();
        result.NewVersion.Should().Be(4);
        result.PreviousVersion.Should().Be(3);

        // Should archive as v3
        _kvMock.Verify(k => k.SetSecretAsync("dek-session-v3", "currentSessionDek==", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RotateDekAsync_NoPreviousDek_CreatesFirstVersion()
    {
        // Arrange: no DEK exists yet
        _repoMock.Setup(r => r.GetSettingAsync("dek-version-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync((VaultSetting?)null);

        _kvMock.Setup(k => k.GetSecretAsync("dek-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

        // Act
        var result = await _sut.RotateDekAsync("api");

        // Assert
        result.Success.Should().BeTrue();
        result.NewVersion.Should().Be(2);

        // Should NOT archive (there's nothing to archive)
        _kvMock.Verify(k => k.SetSecretAsync("dek-api-v1", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        // Should set the new DEK
        _kvMock.Verify(k => k.SetSecretAsync("dek-api", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RotateDekAsync_ThrowsOnNullPurpose()
    {
        var act = () => _sut.RotateDekAsync(null!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RotateDekAsync_ExceptionDuringRotation_ReturnsFailure()
    {
        _repoMock.Setup(r => r.GetSettingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB unavailable"));

        var result = await _sut.RotateDekAsync("data");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("DB unavailable");
    }

    [Fact]
    public async Task RotateDekAsync_PurposeIsCaseInsensitive()
    {
        _repoMock.Setup(r => r.GetSettingAsync("dek-version-data", It.IsAny<CancellationToken>()))
            .ReturnsAsync((VaultSetting?)null);

        _kvMock.Setup(k => k.GetSecretAsync("dek-data", It.IsAny<CancellationToken>()))
            .ReturnsAsync("someDek==");

        var result = await _sut.RotateDekAsync("DATA");

        result.Success.Should().BeTrue();
        result.DekPurpose.Should().Be("DATA");

        // Should use lowercase for secret names
        _kvMock.Verify(k => k.SetSecretAsync("dek-data-v1", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── GetDekVersionInfoAsync Tests ───────────────────

    [Fact]
    public async Task GetDekVersionInfo_WithVersionMetadata_ReturnsCorrectInfo()
    {
        var rotatedAt = DateTime.UtcNow.AddHours(-1);
        var versionMeta = JsonSerializer.Serialize(new { CurrentVersion = 5, RotatedAt = rotatedAt, OldVersionsKept = 4 });
        var setting = VaultSetting.Create("dek-version-data", versionMeta);

        _repoMock.Setup(r => r.GetSettingAsync("dek-version-data", It.IsAny<CancellationToken>()))
            .ReturnsAsync(setting);

        var info = await _sut.GetDekVersionInfoAsync("data");

        info.Should().NotBeNull();
        info!.DekPurpose.Should().Be("data");
        info.CurrentVersion.Should().Be(5);
        info.OldVersionsKept.Should().Be(4);
    }

    [Fact]
    public async Task GetDekVersionInfo_NoMetadataButDekExists_ReturnsVersion1()
    {
        _repoMock.Setup(r => r.GetSettingAsync("dek-version-data", It.IsAny<CancellationToken>()))
            .ReturnsAsync((VaultSetting?)null);

        _kvMock.Setup(k => k.GetSecretAsync("dek-data", It.IsAny<CancellationToken>()))
            .ReturnsAsync("someDek==");

        var info = await _sut.GetDekVersionInfoAsync("data");

        info.Should().NotBeNull();
        info!.CurrentVersion.Should().Be(1);
        info.OldVersionsKept.Should().Be(0);
        info.LastRotatedAt.Should().BeNull();
    }

    [Fact]
    public async Task GetDekVersionInfo_NoDekExists_ReturnsNull()
    {
        _repoMock.Setup(r => r.GetSettingAsync("dek-version-data", It.IsAny<CancellationToken>()))
            .ReturnsAsync((VaultSetting?)null);

        _kvMock.Setup(k => k.GetSecretAsync("dek-data", It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

        var info = await _sut.GetDekVersionInfoAsync("data");

        info.Should().BeNull();
    }

    // ─── ReEncryptTableAsync Tests ──────────────────────

    [Fact]
    public async Task ReEncryptTable_NoConfig_ReturnsEmptyResult()
    {
        _repoMock.Setup(r => r.GetAllEncryptionConfigsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EncryptionConfig>());

        var result = await _sut.ReEncryptTableAsync("patients", "data");

        result.TableName.Should().Be("patients");
        result.TotalRows.Should().Be(0);
        result.ReEncrypted.Should().Be(0);
    }

    [Fact]
    public async Task ReEncryptTable_WithConfig_AuditsOperation()
    {
        var config = EncryptionConfig.Create("patients", ["medical_history"], "data", "test");

        _repoMock.Setup(r => r.GetAllEncryptionConfigsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EncryptionConfig> { config });

        _repoMock.Setup(r => r.GetSettingAsync("dek-version-data", It.IsAny<CancellationToken>()))
            .ReturnsAsync((VaultSetting?)null);

        _kvMock.Setup(k => k.GetSecretAsync("dek-data", It.IsAny<CancellationToken>()))
            .ReturnsAsync("someDek==");

        var result = await _sut.ReEncryptTableAsync("patients", "data");

        result.TableName.Should().Be("patients");

        // Audit log should be created
        _repoMock.Verify(r => r.AddAuditLogAsync(
            It.Is<VaultAuditLog>(l => l.Action == "dek.reencrypt"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReEncryptTable_ThrowsOnNullTableName()
    {
        var act = () => _sut.ReEncryptTableAsync(null!, "data");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ReEncryptTable_ThrowsOnNullPurpose()
    {
        var act = () => _sut.ReEncryptTableAsync("patients", null!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ─── GetReEncryptionProgressAsync Tests ─────────────

    [Fact]
    public async Task GetReEncryptionProgress_ReturnsProgressPerConfig()
    {
        var configs = new List<EncryptionConfig>
        {
            EncryptionConfig.Create("patients", ["medical_history"], "data", "Patient data"),
            EncryptionConfig.Create("prescriptions", ["medications"], "data", "Rx data")
        };

        _repoMock.Setup(r => r.GetAllEncryptionConfigsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configs);

        var progress = await _sut.GetReEncryptionProgressAsync();

        progress.Should().HaveCount(2);
        progress[0].TableName.Should().Be("patients");
        progress[0].DekPurpose.Should().Be("data");
        progress[1].TableName.Should().Be("prescriptions");
    }

    [Fact]
    public async Task GetReEncryptionProgress_EmptyConfigs_ReturnsEmpty()
    {
        _repoMock.Setup(r => r.GetAllEncryptionConfigsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EncryptionConfig>());

        var progress = await _sut.GetReEncryptionProgressAsync();

        progress.Should().BeEmpty();
    }
}
