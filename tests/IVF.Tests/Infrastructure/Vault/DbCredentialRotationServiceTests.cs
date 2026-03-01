using System.Diagnostics.Metrics;
using System.Text.Json;
using FluentAssertions;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace IVF.Tests.Infrastructure.Vault;

public class DbCredentialRotationServiceTests
{
    private readonly Mock<IDynamicCredentialProvider> _credProviderMock;
    private readonly Mock<IVaultRepository> _repoMock;
    private readonly DbCredentialRotationService _sut;

    public DbCredentialRotationServiceTests()
    {
        _credProviderMock = new Mock<IDynamicCredentialProvider>();
        _repoMock = new Mock<IVaultRepository>();
        var loggerMock = new Mock<ILogger<DbCredentialRotationService>>();
        var metrics = CreateVaultMetrics();

        _sut = new DbCredentialRotationService(
            _credProviderMock.Object,
            _repoMock.Object,
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

    private void SetupStateInRepo(DbCredentialRotationService.DbRotationState state)
    {
        var setting = VaultSetting.Create("db-rotation-state", JsonSerializer.Serialize(state));
        _repoMock.Setup(r => r.GetSettingAsync("db-rotation-state", It.IsAny<CancellationToken>()))
            .ReturnsAsync(setting);
    }

    private void SetupCredentialGeneration(string username = "ivf_dyn_abc123")
    {
        var credId = Guid.NewGuid();
        _credProviderMock.Setup(c => c.GenerateCredentialAsync(It.IsAny<DynamicCredentialRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DynamicCredentialResult(
                credId, "lease-123", username, "password123",
                $"Host=localhost;Port=5433;Database=ivf_db;Username={username};Password=password123;",
                DateTime.UtcNow.AddHours(24)));
    }

    // ─── RotateAsync Tests ──────────────────────────────

    [Fact]
    public async Task RotateAsync_FirstRotation_ActivatesSlotB()
    {
        // Arrange: fresh state, active slot A, no credentials yet
        _repoMock.Setup(r => r.GetSettingAsync("db-rotation-state", It.IsAny<CancellationToken>()))
            .ReturnsAsync((VaultSetting?)null);
        SetupCredentialGeneration();

        // Act
        var result = await _sut.RotateAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.ActiveSlot.Should().Be("B");
        result.NewUsername.Should().Be("ivf_dyn_abc123");
        result.ExpiresAt.Should().NotBeNull();

        // State should be saved
        _repoMock.Verify(r => r.SaveSettingAsync("db-rotation-state", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

        // Audit log should be created
        _repoMock.Verify(r => r.AddAuditLogAsync(It.Is<VaultAuditLog>(l => l.Action == "db.credential.rotate"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RotateAsync_SecondRotation_SwapsBackToSlotA()
    {
        // Arrange: slot B is active
        var state = new DbCredentialRotationService.DbRotationState
        {
            ActiveSlot = "B",
            SlotBCredentialId = Guid.NewGuid(),
            SlotBUsername = "ivf_dyn_old",
            RotationCount = 1
        };
        SetupStateInRepo(state);
        SetupCredentialGeneration("ivf_dyn_new");

        // Act
        var result = await _sut.RotateAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.ActiveSlot.Should().Be("A");
        result.NewUsername.Should().Be("ivf_dyn_new");
    }

    [Fact]
    public async Task RotateAsync_RevokesOldStandbyCredential()
    {
        var oldCredId = Guid.NewGuid();
        var state = new DbCredentialRotationService.DbRotationState
        {
            ActiveSlot = "A",
            SlotBCredentialId = oldCredId,
            SlotBUsername = "ivf_dyn_old"
        };
        SetupStateInRepo(state);
        SetupCredentialGeneration();

        await _sut.RotateAsync();

        // Should revoke the old standby credential
        _credProviderMock.Verify(c => c.RevokeCredentialAsync(oldCredId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RotateAsync_IncrementsRotationCount()
    {
        var state = new DbCredentialRotationService.DbRotationState
        {
            ActiveSlot = "A",
            RotationCount = 5
        };
        SetupStateInRepo(state);
        SetupCredentialGeneration();

        await _sut.RotateAsync();

        _repoMock.Verify(r => r.SaveSettingAsync("db-rotation-state",
            It.Is<string>(s => s.Contains("\"RotationCount\":6")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RotateAsync_CredentialGenerationFails_ReturnsFailure()
    {
        _repoMock.Setup(r => r.GetSettingAsync("db-rotation-state", It.IsAny<CancellationToken>()))
            .ReturnsAsync((VaultSetting?)null);

        _credProviderMock.Setup(c => c.GenerateCredentialAsync(It.IsAny<DynamicCredentialRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB connection failed"));

        var result = await _sut.RotateAsync();

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("DB connection failed");
    }

    [Fact]
    public async Task RotateAsync_StoresConnectionStringInVaultConfig()
    {
        _repoMock.Setup(r => r.GetSettingAsync("db-rotation-state", It.IsAny<CancellationToken>()))
            .ReturnsAsync((VaultSetting?)null);
        SetupCredentialGeneration();

        await _sut.RotateAsync();

        // Should save connection string for VaultConfigurationProvider
        _repoMock.Verify(r => r.SaveSettingAsync(
            "config/ConnectionStrings/DefaultConnection",
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─── GetStatusAsync Tests ───────────────────────────

    [Fact]
    public async Task GetStatus_WithState_ReturnsCorrectStatus()
    {
        var rotatedAt = DateTime.UtcNow.AddHours(-1);
        var state = new DbCredentialRotationService.DbRotationState
        {
            ActiveSlot = "B",
            SlotAUsername = "ivf_dyn_old",
            SlotAExpiresAt = DateTime.UtcNow.AddHours(-12),
            SlotBUsername = "ivf_dyn_current",
            SlotBExpiresAt = DateTime.UtcNow.AddHours(12),
            LastRotatedAt = rotatedAt,
            RotationCount = 3
        };
        SetupStateInRepo(state);

        var status = await _sut.GetStatusAsync();

        status.ActiveSlot.Should().Be("B");
        status.SlotAActive.Should().BeFalse();
        status.SlotBActive.Should().BeTrue();
        status.SlotBUsername.Should().Be("ivf_dyn_current");
        status.RotationCount.Should().Be(3);
    }

    [Fact]
    public async Task GetStatus_NoState_ReturnsDefaults()
    {
        _repoMock.Setup(r => r.GetSettingAsync("db-rotation-state", It.IsAny<CancellationToken>()))
            .ReturnsAsync((VaultSetting?)null);

        var status = await _sut.GetStatusAsync();

        status.ActiveSlot.Should().Be("A");
        status.RotationCount.Should().Be(0);
    }

    // ─── GetActiveConnectionStringAsync Tests ───────────

    [Fact]
    public async Task GetActiveConnectionString_ReturnsActiveSlotConnStr()
    {
        var state = new DbCredentialRotationService.DbRotationState
        {
            ActiveSlot = "B",
            SlotAConnectionString = "Host=localhost;Username=slotA",
            SlotBConnectionString = "Host=localhost;Username=slotB"
        };
        SetupStateInRepo(state);

        var connStr = await _sut.GetActiveConnectionStringAsync();

        connStr.Should().Be("Host=localhost;Username=slotB");
    }

    [Fact]
    public async Task GetActiveConnectionString_NoCredentials_ReturnsNull()
    {
        _repoMock.Setup(r => r.GetSettingAsync("db-rotation-state", It.IsAny<CancellationToken>()))
            .ReturnsAsync((VaultSetting?)null);

        var connStr = await _sut.GetActiveConnectionStringAsync();

        connStr.Should().BeNull();
    }
}
