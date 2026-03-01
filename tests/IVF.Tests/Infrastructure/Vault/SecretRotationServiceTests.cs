using FluentAssertions;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.Diagnostics.Metrics;

namespace IVF.Tests.Infrastructure.Vault;

public class SecretRotationServiceTests
{
    private readonly Mock<IVaultRepository> _repoMock;
    private readonly Mock<IVaultSecretService> _secretServiceMock;
    private readonly SecretRotationService _sut;

    public SecretRotationServiceTests()
    {
        _repoMock = new Mock<IVaultRepository>();
        _secretServiceMock = new Mock<IVaultSecretService>();
        var loggerMock = new Mock<ILogger<SecretRotationService>>();
        var metrics = CreateVaultMetrics();

        _sut = new SecretRotationService(_repoMock.Object, _secretServiceMock.Object, loggerMock.Object, metrics);
    }

    private static VaultMetrics CreateVaultMetrics()
    {
        var meterFactoryMock = new Mock<IMeterFactory>();
        meterFactoryMock.Setup(f => f.Create(It.IsAny<MeterOptions>()))
            .Returns((MeterOptions opts) => new Meter(opts));
        return new VaultMetrics(meterFactoryMock.Object);
    }

    [Fact]
    public async Task SetRotationSchedule_NewSecret_CreatesSchedule()
    {
        _secretServiceMock.Setup(s => s.GetSecretAsync("config/jwt-secret", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VaultSecretResult(Guid.NewGuid(), "path", 1, "encrypted", null, DateTime.UtcNow, null));

        _repoMock.Setup(r => r.GetRotationScheduleByPathAsync("config/jwt-secret", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SecretRotationSchedule?)null);

        var result = await _sut.SetRotationScheduleAsync("config/jwt-secret",
            new RotationConfig(30, 24, true));

        result.SecretPath.Should().Be("config/jwt-secret");
        result.RotationIntervalDays.Should().Be(30);
        result.AutomaticallyRotate.Should().BeTrue();
        result.NextRotationAt.Should().BeAfter(DateTime.UtcNow);

        _repoMock.Verify(r => r.AddRotationScheduleAsync(It.IsAny<SecretRotationSchedule>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetRotationSchedule_NonExistentSecret_Throws()
    {
        _secretServiceMock.Setup(s => s.GetSecretAsync("nonexistent", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VaultSecretResult?)null);

        _repoMock.Setup(r => r.GetRotationScheduleByPathAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SecretRotationSchedule?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.SetRotationScheduleAsync("nonexistent", new RotationConfig(30)));
    }

    [Fact]
    public async Task SetRotationSchedule_ExistingSchedule_Updates()
    {
        var existing = SecretRotationSchedule.Create("config/db-password", 30);
        _repoMock.Setup(r => r.GetRotationScheduleByPathAsync("config/db-password", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await _sut.SetRotationScheduleAsync("config/db-password",
            new RotationConfig(60, 48, true));

        result.RotationIntervalDays.Should().Be(60);
        result.GracePeriodHours.Should().Be(48);

        _repoMock.Verify(r => r.UpdateRotationScheduleAsync(It.IsAny<SecretRotationSchedule>(), It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.AddRotationScheduleAsync(It.IsAny<SecretRotationSchedule>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveRotationSchedule_ExistingSchedule_Deactivates()
    {
        var existing = SecretRotationSchedule.Create("config/api-key", 30);
        _repoMock.Setup(r => r.GetRotationScheduleByPathAsync("config/api-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        await _sut.RemoveRotationScheduleAsync("config/api-key");

        _repoMock.Verify(r => r.UpdateRotationScheduleAsync(
            It.Is<SecretRotationSchedule>(s => !s.IsActive), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveRotationSchedule_NonExistent_NoOp()
    {
        _repoMock.Setup(r => r.GetRotationScheduleByPathAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SecretRotationSchedule?)null);

        await _sut.RemoveRotationScheduleAsync("nonexistent");

        _repoMock.Verify(r => r.UpdateRotationScheduleAsync(It.IsAny<SecretRotationSchedule>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RotateNow_ExistingSecret_CreatesNewVersion()
    {
        var userId = Guid.NewGuid();
        _secretServiceMock.Setup(s => s.GetSecretAsync("config/jwt-secret", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VaultSecretResult(Guid.NewGuid(), "path", 1, "old-value", null, DateTime.UtcNow, null));

        _secretServiceMock.Setup(s => s.PutSecretAsync("config/jwt-secret", It.IsAny<string>(), userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VaultSecretResult(Guid.NewGuid(), "path", 2, "new-value", null, DateTime.UtcNow, null));

        _repoMock.Setup(r => r.GetRotationScheduleByPathAsync("config/jwt-secret", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SecretRotationSchedule?)null);

        var result = await _sut.RotateNowAsync("config/jwt-secret", userId);

        result.Success.Should().BeTrue();
        result.OldVersion.Should().Be(1);
        result.NewVersion.Should().Be(2);
        result.SecretPath.Should().Be("config/jwt-secret");
    }

    [Fact]
    public async Task RotateNow_NonExistentSecret_ReturnsFailed()
    {
        _secretServiceMock.Setup(s => s.GetSecretAsync("nonexistent", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VaultSecretResult?)null);

        var result = await _sut.RotateNowAsync("nonexistent");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task RotateNow_UpdatesScheduleLastRotated()
    {
        var schedule = SecretRotationSchedule.Create("config/key", 30);

        _secretServiceMock.Setup(s => s.GetSecretAsync("config/key", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VaultSecretResult(Guid.NewGuid(), "path", 1, "val", null, DateTime.UtcNow, null));

        _secretServiceMock.Setup(s => s.PutSecretAsync("config/key", It.IsAny<string>(), null, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VaultSecretResult(Guid.NewGuid(), "path", 2, "new-val", null, DateTime.UtcNow, null));

        _repoMock.Setup(r => r.GetRotationScheduleByPathAsync("config/key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(schedule);

        await _sut.RotateNowAsync("config/key");

        _repoMock.Verify(r => r.UpdateRotationScheduleAsync(
            It.Is<SecretRotationSchedule>(s => s.LastRotatedAt.HasValue), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RotateNow_Failure_AuditLogsError()
    {
        _secretServiceMock.Setup(s => s.GetSecretAsync("config/key", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VaultSecretResult(Guid.NewGuid(), "path", 1, "val", null, DateTime.UtcNow, null));

        _secretServiceMock.Setup(s => s.PutSecretAsync("config/key", It.IsAny<string>(), null, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Encryption failure"));

        var result = await _sut.RotateNowAsync("config/key");

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Encryption failure");

        _repoMock.Verify(r => r.AddAuditLogAsync(
            It.Is<VaultAuditLog>(l => l.Action == "rotation.failed"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecutePendingRotations_RotatesDueSecrets()
    {
        // Create a schedule that is due (NextRotationAt in the past)
        var schedule = SecretRotationSchedule.Create("config/due-secret", 0); // 0 days = immediately due

        _repoMock.Setup(r => r.GetRotationSchedulesAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SecretRotationSchedule> { schedule });

        _secretServiceMock.Setup(s => s.GetSecretAsync("config/due-secret", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VaultSecretResult(Guid.NewGuid(), "path", 1, "val", null, DateTime.UtcNow, null));

        _secretServiceMock.Setup(s => s.PutSecretAsync("config/due-secret", It.IsAny<string>(), null, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VaultSecretResult(Guid.NewGuid(), "path", 2, "new", null, DateTime.UtcNow, null));

        _repoMock.Setup(r => r.GetRotationScheduleByPathAsync("config/due-secret", It.IsAny<CancellationToken>()))
            .ReturnsAsync(schedule);

        var result = await _sut.ExecutePendingRotationsAsync();

        result.TotalScheduled.Should().Be(1);
        result.Succeeded.Should().Be(1);
        result.Failed.Should().Be(0);
    }

    [Fact]
    public async Task ExecutePendingRotations_SkipsNotDue()
    {
        var schedule = SecretRotationSchedule.Create("config/future", 90); // Due in 90 days

        _repoMock.Setup(r => r.GetRotationSchedulesAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SecretRotationSchedule> { schedule });

        var result = await _sut.ExecutePendingRotationsAsync();

        result.TotalScheduled.Should().Be(0);
        result.Succeeded.Should().Be(0);

        _secretServiceMock.Verify(s => s.PutSecretAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetSchedules_ReturnsActiveSchedules()
    {
        var schedules = new List<SecretRotationSchedule>
        {
            SecretRotationSchedule.Create("config/a", 30),
            SecretRotationSchedule.Create("config/b", 60),
        };

        _repoMock.Setup(r => r.GetRotationSchedulesAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(schedules);

        var result = await _sut.GetSchedulesAsync();

        result.Should().HaveCount(2);
        result[0].SecretPath.Should().Be("config/a");
        result[1].SecretPath.Should().Be("config/b");
    }
}

public class SecretRotationScheduleEntityTests
{
    [Fact]
    public void Create_SetsCorrectDefaults()
    {
        var schedule = SecretRotationSchedule.Create("config/jwt", 30, gracePeriodHours: 12);

        schedule.SecretPath.Should().Be("config/jwt");
        schedule.RotationIntervalDays.Should().Be(30);
        schedule.GracePeriodHours.Should().Be(12);
        schedule.AutomaticallyRotate.Should().BeTrue();
        schedule.RotationStrategy.Should().Be("generate");
        schedule.IsActive.Should().BeTrue();
        schedule.LastRotatedAt.Should().BeNull();
        schedule.NextRotationAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void RecordRotation_UpdatesTimestamps()
    {
        var schedule = SecretRotationSchedule.Create("config/key", 30);

        schedule.RecordRotation();

        schedule.LastRotatedAt.Should().NotBeNull();
        schedule.LastRotatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        schedule.NextRotationAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void UpdateConfig_ChangesIntervalAndRecalculatesNext()
    {
        var schedule = SecretRotationSchedule.Create("config/key", 30);
        schedule.RecordRotation();
        var lastRotated = schedule.LastRotatedAt!.Value;

        schedule.UpdateConfig(60, 48, false);

        schedule.RotationIntervalDays.Should().Be(60);
        schedule.GracePeriodHours.Should().Be(48);
        schedule.AutomaticallyRotate.Should().BeFalse();
        schedule.NextRotationAt.Should().BeCloseTo(lastRotated.AddDays(60), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Deactivate_SetsInactive()
    {
        var schedule = SecretRotationSchedule.Create("config/key", 30);

        schedule.Deactivate();

        schedule.IsActive.Should().BeFalse();
    }

    [Fact]
    public void IsDueForRotation_PastNextDate_ReturnsTrue()
    {
        var schedule = SecretRotationSchedule.Create("config/key", 0); // 0 days = due immediately

        schedule.IsDueForRotation.Should().BeTrue();
    }

    [Fact]
    public void IsDueForRotation_FutureNextDate_ReturnsFalse()
    {
        var schedule = SecretRotationSchedule.Create("config/key", 90);

        schedule.IsDueForRotation.Should().BeFalse();
    }

    [Fact]
    public void IsDueForRotation_Inactive_ReturnsFalse()
    {
        var schedule = SecretRotationSchedule.Create("config/key", 0);
        schedule.Deactivate();

        schedule.IsDueForRotation.Should().BeFalse();
    }

    [Fact]
    public void IsDueForRotation_ManualOnly_ReturnsFalse()
    {
        var schedule = SecretRotationSchedule.Create("config/key", 0, automaticallyRotate: false);

        schedule.IsDueForRotation.Should().BeFalse();
    }
}
