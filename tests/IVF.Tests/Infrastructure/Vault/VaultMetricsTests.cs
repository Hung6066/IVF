using FluentAssertions;
using IVF.Infrastructure.Services;
using Moq;
using System.Diagnostics.Metrics;

namespace IVF.Tests.Infrastructure.Vault;

public class VaultMetricsTests
{
    private readonly VaultMetrics _sut;

    public VaultMetricsTests()
    {
        var meterFactoryMock = new Mock<IMeterFactory>();
        meterFactoryMock.Setup(f => f.Create(It.IsAny<MeterOptions>()))
            .Returns((MeterOptions opts) => new Meter(opts));
        _sut = new VaultMetrics(meterFactoryMock.Object);
    }

    [Fact]
    public void MeterName_ShouldBeIVFVault()
    {
        VaultMetrics.MeterName.Should().Be("IVF.Vault");
    }

    [Fact]
    public void RecordSecretOperation_ShouldNotThrow()
    {
        var act = () => _sut.RecordSecretOperation("get", "config/db/password");
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordEncryptionOperation_ShouldNotThrow()
    {
        var act = () => _sut.RecordEncryptionOperation("encrypt");
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordTokenValidation_ShouldNotThrow()
    {
        var act = () => _sut.RecordTokenValidation("valid");
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordPolicyEvaluation_ShouldNotThrow()
    {
        var act = () => _sut.RecordPolicyEvaluation(true);
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordZeroTrustEvaluation_ShouldNotThrow()
    {
        var act = () => _sut.RecordZeroTrustEvaluation(true, "passed");
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordRotation_ShouldNotThrow()
    {
        var act = () => _sut.RecordRotation(true);
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordEncryptionDuration_ShouldNotThrow()
    {
        var act = () => _sut.RecordEncryptionDuration(15.5, "encrypt");
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordSecretAccessDuration_ShouldNotThrow()
    {
        var act = () => _sut.RecordSecretAccessDuration(25.0, "get");
        act.Should().NotThrow();
    }

    [Fact]
    public void SetActiveLeases_ShouldNotThrow()
    {
        var act = () => _sut.SetActiveLeases(42);
        act.Should().NotThrow();
    }

    [Fact]
    public void SetActiveDynamicCredentials_ShouldNotThrow()
    {
        var act = () => _sut.SetActiveDynamicCredentials(10);
        act.Should().NotThrow();
    }

    [Fact]
    public void SetActiveRotationSchedules_ShouldNotThrow()
    {
        var act = () => _sut.SetActiveRotationSchedules(5);
        act.Should().NotThrow();
    }

    [Fact]
    public void SetSecretsCount_ShouldNotThrow()
    {
        var act = () => _sut.SetSecretsCount(100);
        act.Should().NotThrow();
    }

    [Fact]
    public void MeterInstruments_ShouldBeRegistered()
    {
        // Verify the meter has the expected instruments
        var instrumentNames = new List<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, _) =>
        {
            if (instrument.Meter.Name == VaultMetrics.MeterName)
                instrumentNames.Add(instrument.Name);
        };
        listener.Start();

        // Re-create to trigger instrument registration on a fresh listen
        var meterFactoryMock = new Mock<IMeterFactory>();
        meterFactoryMock.Setup(f => f.Create(It.IsAny<MeterOptions>()))
            .Returns((MeterOptions opts) => new Meter(opts));
        _ = new VaultMetrics(meterFactoryMock.Object);

        instrumentNames.Should().Contain("vault.secret.operations");
        instrumentNames.Should().Contain("vault.encryption.operations");
        instrumentNames.Should().Contain("vault.token.validations");
        instrumentNames.Should().Contain("vault.policy.evaluations");
        instrumentNames.Should().Contain("vault.zerotrust.evaluations");
        instrumentNames.Should().Contain("vault.rotation.operations");
        instrumentNames.Should().Contain("vault.encryption.duration");
        instrumentNames.Should().Contain("vault.secret.access.duration");
        instrumentNames.Should().Contain("vault.lease.active");
        instrumentNames.Should().Contain("vault.dynamic_credential.active");
        instrumentNames.Should().Contain("vault.rotation.schedules.active");
        instrumentNames.Should().Contain("vault.secrets.total");
    }

    [Fact]
    public void SecretOperationCounter_ShouldRecordWithTags()
    {
        // MeterListener to capture measurements
        long recorded = 0;
        string? operationTag = null;
        string? pathTag = null;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Name == "vault.secret.operations")
                listener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            recorded = value;
            foreach (var tag in tags)
            {
                if (tag.Key == "operation") operationTag = tag.Value?.ToString();
                if (tag.Key == "path_prefix") pathTag = tag.Value?.ToString();
            }
        });
        listener.Start();

        // Re-create with listener active
        var meterFactoryMock = new Mock<IMeterFactory>();
        meterFactoryMock.Setup(f => f.Create(It.IsAny<MeterOptions>()))
            .Returns((MeterOptions opts) => new Meter(opts));
        var metrics = new VaultMetrics(meterFactoryMock.Object);
        metrics.RecordSecretOperation("put", "config/db/password");

        recorded.Should().Be(1);
        operationTag.Should().Be("put");
        pathTag.Should().Be("config");
    }

    [Fact]
    public void GetPrefix_ShouldExtractFirstSegment()
    {
        // Test via RecordSecretOperation which calls GetPrefix internally
        string? pathTag = null;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Name == "vault.secret.operations")
                listener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            foreach (var tag in tags)
                if (tag.Key == "path_prefix") pathTag = tag.Value?.ToString();
        });
        listener.Start();

        var meterFactoryMock = new Mock<IMeterFactory>();
        meterFactoryMock.Setup(f => f.Create(It.IsAny<MeterOptions>()))
            .Returns((MeterOptions opts) => new Meter(opts));
        var metrics = new VaultMetrics(meterFactoryMock.Object);

        metrics.RecordSecretOperation("get", "secrets/production/api-key");
        pathTag.Should().Be("secrets");
    }

    [Fact]
    public void GetPrefix_NoSlash_ReturnsFullPath()
    {
        string? pathTag = null;

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Name == "vault.secret.operations")
                listener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            foreach (var tag in tags)
                if (tag.Key == "path_prefix") pathTag = tag.Value?.ToString();
        });
        listener.Start();

        var meterFactoryMock = new Mock<IMeterFactory>();
        meterFactoryMock.Setup(f => f.Create(It.IsAny<MeterOptions>()))
            .Returns((MeterOptions opts) => new Meter(opts));
        var metrics = new VaultMetrics(meterFactoryMock.Object);

        metrics.RecordSecretOperation("get", "standalone-secret");
        pathTag.Should().Be("standalone-secret");
    }
}
