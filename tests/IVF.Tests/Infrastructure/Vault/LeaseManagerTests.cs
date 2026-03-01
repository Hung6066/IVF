using FluentAssertions;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.Diagnostics.Metrics;

namespace IVF.Tests.Infrastructure.Vault;

public class LeaseManagerTests
{
    private readonly Mock<IVaultRepository> _repoMock;
    private readonly Mock<IVaultSecretService> _secretServiceMock;
    private readonly Mock<ILogger<LeaseManager>> _loggerMock;
    private readonly LeaseManager _sut;

    public LeaseManagerTests()
    {
        _repoMock = new Mock<IVaultRepository>();
        _secretServiceMock = new Mock<IVaultSecretService>();
        _loggerMock = new Mock<ILogger<LeaseManager>>();
        var metrics = CreateVaultMetrics();

        _sut = new LeaseManager(_repoMock.Object, _secretServiceMock.Object, _loggerMock.Object, metrics);
    }

    private static VaultMetrics CreateVaultMetrics()
    {
        var meterFactoryMock = new Mock<IMeterFactory>();
        meterFactoryMock.Setup(f => f.Create(It.IsAny<MeterOptions>()))
            .Returns((MeterOptions opts) => new Meter(opts));
        return new VaultMetrics(meterFactoryMock.Object);
    }

    [Fact]
    public async Task CreateLeaseAsync_ShouldCreateLeaseForSecret()
    {
        // Arrange
        var secretId = Guid.NewGuid();
        var secretResult = new VaultSecretResult(secretId, "config/db", 1, "value", null, DateTime.UtcNow, null);

        _secretServiceMock.Setup(s => s.GetSecretAsync("config/db", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(secretResult);
        _repoMock.Setup(r => r.AddLeaseAsync(It.IsAny<VaultLease>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.AddAuditLogAsync(It.IsAny<VaultAuditLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.CreateLeaseAsync("config/db", 3600, true);

        // Assert
        result.LeaseId.Should().StartWith("lease_");
        result.SecretId.Should().Be(secretId);
        result.Ttl.Should().Be(3600);
        result.Renewable.Should().BeTrue();
        result.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddSeconds(3600), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateLeaseAsync_WithNonExistentSecret_ShouldThrow()
    {
        // Arrange
        _secretServiceMock.Setup(s => s.GetSecretAsync("missing", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VaultSecretResult?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CreateLeaseAsync("missing", 3600));
    }

    [Fact]
    public async Task CreateLeaseAsync_ShouldAuditLog()
    {
        // Arrange
        var secretResult = new VaultSecretResult(Guid.NewGuid(), "path", 1, "v", null, DateTime.UtcNow, null);
        _secretServiceMock.Setup(s => s.GetSecretAsync("path", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(secretResult);
        _repoMock.Setup(r => r.AddLeaseAsync(It.IsAny<VaultLease>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.AddAuditLogAsync(It.IsAny<VaultAuditLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.CreateLeaseAsync("path", 3600);

        // Assert
        _repoMock.Verify(r => r.AddAuditLogAsync(
            It.Is<VaultAuditLog>(a => a.Action == "lease.create"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RenewLeaseAsync_ShouldExtendExpiry()
    {
        // Arrange
        var lease = VaultLease.Create(Guid.NewGuid(), 3600, true);
        _repoMock.Setup(r => r.GetLeaseByIdAsync(lease.LeaseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lease);
        _repoMock.Setup(r => r.UpdateLeaseAsync(It.IsAny<VaultLease>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.AddAuditLogAsync(It.IsAny<VaultAuditLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.RenewLeaseAsync(lease.LeaseId, 7200);

        // Assert
        result.Ttl.Should().Be(7200);
        result.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddSeconds(7200), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RenewLeaseAsync_NonRenewable_ShouldThrow()
    {
        // Arrange
        var lease = VaultLease.Create(Guid.NewGuid(), 3600, renewable: false);
        _repoMock.Setup(r => r.GetLeaseByIdAsync(lease.LeaseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lease);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RenewLeaseAsync(lease.LeaseId, 7200));
    }

    [Fact]
    public async Task RenewLeaseAsync_RevokedLease_ShouldThrow()
    {
        // Arrange
        var lease = VaultLease.Create(Guid.NewGuid(), 3600, true);
        lease.Revoke();
        _repoMock.Setup(r => r.GetLeaseByIdAsync(lease.LeaseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lease);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RenewLeaseAsync(lease.LeaseId, 7200));
    }

    [Fact]
    public async Task RenewLeaseAsync_NonExistentLease_ShouldThrow()
    {
        _repoMock.Setup(r => r.GetLeaseByIdAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((VaultLease?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RenewLeaseAsync("missing", 7200));
    }

    [Fact]
    public async Task RevokeLeaseAsync_ShouldDelegateAndAudit()
    {
        // Arrange
        _repoMock.Setup(r => r.RevokeLeaseAsync("lid", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.AddAuditLogAsync(It.IsAny<VaultAuditLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.RevokeLeaseAsync("lid");

        // Assert
        _repoMock.Verify(r => r.RevokeLeaseAsync("lid", It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.AddAuditLogAsync(
            It.Is<VaultAuditLog>(a => a.Action == "lease.revoke"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetLeasedSecretAsync_WithActiveLease_ShouldReturnSecret()
    {
        // Arrange
        var secretId = Guid.NewGuid();
        var lease = VaultLease.Create(secretId, 3600, true);
        var secret = VaultSecret.Create("config/db", "encrypted", "iv");

        _repoMock.Setup(r => r.GetLeaseByIdAsync(lease.LeaseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lease);
        _repoMock.Setup(r => r.GetSecretByIdAsync(secretId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(secret);
        _secretServiceMock.Setup(s => s.GetSecretAsync("config/db", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VaultSecretResult(secretId, "config/db", 1, "decrypted-value", null, DateTime.UtcNow, null));

        // Act
        var result = await _sut.GetLeasedSecretAsync(lease.LeaseId);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Should().Be("decrypted-value");
        result.RemainingSeconds.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetLeasedSecretAsync_WithRevokedLease_ShouldReturnNull()
    {
        // Arrange
        var lease = VaultLease.Create(Guid.NewGuid(), 3600, true);
        lease.Revoke();

        _repoMock.Setup(r => r.GetLeaseByIdAsync(lease.LeaseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lease);

        // Act
        var result = await _sut.GetLeasedSecretAsync(lease.LeaseId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetLeasedSecretAsync_WithNonExistentLease_ShouldReturnNull()
    {
        _repoMock.Setup(r => r.GetLeaseByIdAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((VaultLease?)null);

        var result = await _sut.GetLeasedSecretAsync("missing");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveLeasesAsync_ShouldReturnOnlyActive()
    {
        // Arrange
        var active = VaultLease.Create(Guid.NewGuid(), 3600, true);
        var revoked = VaultLease.Create(Guid.NewGuid(), 3600, true);
        revoked.Revoke();

        _repoMock.Setup(r => r.GetLeasesAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultLease> { active, revoked });

        // Act
        var result = await _sut.GetActiveLeasesAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].LeaseId.Should().Be(active.LeaseId);
    }
}
