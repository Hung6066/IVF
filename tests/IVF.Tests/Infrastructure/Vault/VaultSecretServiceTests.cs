using FluentAssertions;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Domain.Enums;
using IVF.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System.Diagnostics.Metrics;

namespace IVF.Tests.Infrastructure.Vault;

public class VaultSecretServiceTests
{
    private readonly Mock<IVaultRepository> _repoMock;
    private readonly Mock<IKeyVaultService> _kvMock;
    private readonly Mock<ILogger<VaultSecretService>> _loggerMock;
    private readonly IConfiguration _config;
    private readonly VaultSecretService _sut;

    public VaultSecretServiceTests()
    {
        _repoMock = new Mock<IVaultRepository>();
        _kvMock = new Mock<IKeyVaultService>();
        _loggerMock = new Mock<ILogger<VaultSecretService>>();

        var configData = new Dictionary<string, string?>
        {
            ["JwtSettings:Secret"] = "test-secret-key-for-unit-tests-only"
        };
        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        // Setup: No existing wrapped KEK → triggers migration path
        _repoMock.Setup(r => r.GetSettingAsync("vault-secret-wrapped-kek", It.IsAny<CancellationToken>()))
            .ReturnsAsync((VaultSetting?)null);
        _repoMock.Setup(r => r.GetSettingAsync("vault-secret-wrapped-kek-iv", It.IsAny<CancellationToken>()))
            .ReturnsAsync((VaultSetting?)null);

        // Migration: no existing secrets
        _repoMock.Setup(r => r.ListSecretsAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultSecret>());

        // WrapKeyAsync returns a mock wrapped key
        _kvMock.Setup(kv => kv.WrapKeyAsync(It.IsAny<byte[]>(), "vault-secret-kek", It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[] key, string _, CancellationToken _) =>
                new WrappedKeyResult(
                    Convert.ToBase64String(key), // simplified: wrapped = original for test
                    Convert.ToBase64String(new byte[12]),
                    "AES-256-GCM",
                    "vault-secret-kek",
                    1));

        // UnwrapKeyAsync returns the key back
        _kvMock.Setup(kv => kv.UnwrapKeyAsync(It.IsAny<string>(), It.IsAny<string>(), "vault-secret-kek", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string wrapped, string _, string _, CancellationToken _) =>
                Convert.FromBase64String(wrapped));

        // SaveSettingAsync
        _repoMock.Setup(r => r.SaveSettingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var metrics = CreateVaultMetrics();
        _sut = new VaultSecretService(_repoMock.Object, _kvMock.Object, _config, _loggerMock.Object, metrics);

        // Reset the static KEK cache via reflection for test isolation
        ResetStaticKek();
    }

    private static void ResetStaticKek()
    {
        var field = typeof(VaultSecretService).GetField("s_kek",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        field?.SetValue(null, null);
    }

    private static VaultMetrics CreateVaultMetrics()
    {
        var meterFactoryMock = new Mock<IMeterFactory>();
        meterFactoryMock.Setup(f => f.Create(It.IsAny<MeterOptions>()))
            .Returns((MeterOptions opts) => new Meter(opts));
        return new VaultMetrics(meterFactoryMock.Object);
    }

    [Fact]
    public async Task PutSecretAsync_ShouldEncryptAndStore()
    {
        // Arrange
        var path = "config/db/password";
        var plaintext = "super-secret-password";

        _repoMock.Setup(r => r.GetLatestVersionAsync(path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repoMock.Setup(r => r.AddSecretAsync(It.IsAny<VaultSecret>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.PutSecretAsync(path, plaintext);

        // Assert
        result.Path.Should().Be(path);
        result.Version.Should().Be(1);
        result.Value.Should().Be(plaintext);
        _repoMock.Verify(r => r.AddSecretAsync(
            It.Is<VaultSecret>(s => s.Path == path && s.EncryptedData != plaintext),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PutSecretAsync_ShouldAutoIncrementVersion()
    {
        // Arrange
        _repoMock.Setup(r => r.GetLatestVersionAsync("path", It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);
        _repoMock.Setup(r => r.AddSecretAsync(It.IsAny<VaultSecret>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.PutSecretAsync("path", "value");

        // Assert
        result.Version.Should().Be(6);
    }

    [Fact]
    public async Task GetSecretAsync_ShouldDecryptStoredSecret()
    {
        // Arrange — we need to first put a secret to get the KEK initialized
        var path = "test/secret";
        VaultSecret? storedSecret = null;

        _repoMock.Setup(r => r.GetLatestVersionAsync(path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repoMock.Setup(r => r.AddSecretAsync(It.IsAny<VaultSecret>(), It.IsAny<CancellationToken>()))
            .Callback<VaultSecret, CancellationToken>((s, _) => storedSecret = s)
            .Returns(Task.CompletedTask);

        await _sut.PutSecretAsync(path, "my-secret-value");

        // Now setup GetSecretAsync to return the stored secret
        _repoMock.Setup(r => r.GetSecretAsync(path, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedSecret);

        // Act
        var result = await _sut.GetSecretAsync(path);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Should().Be("my-secret-value");
        result.Path.Should().Be(path);
    }

    [Fact]
    public async Task GetSecretAsync_WithNonExistentPath_ShouldReturnNull()
    {
        // Arrange — Initialize KEK via a put first
        _repoMock.Setup(r => r.GetLatestVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repoMock.Setup(r => r.AddSecretAsync(It.IsAny<VaultSecret>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        await _sut.PutSecretAsync("init", "init");

        _repoMock.Setup(r => r.GetSecretAsync("nonexistent", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VaultSecret?)null);

        // Act
        var result = await _sut.GetSecretAsync("nonexistent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteSecretAsync_ShouldDelegateToRepo()
    {
        // Arrange
        _repoMock.Setup(r => r.DeleteSecretAsync("path", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.DeleteSecretAsync("path");

        // Assert
        _repoMock.Verify(r => r.DeleteSecretAsync("path", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListSecretsAsync_ShouldReturnFolderStructure()
    {
        // Arrange
        var secrets = new List<VaultSecret>
        {
            VaultSecret.Create("config/db/host", "e1", "iv1"),
            VaultSecret.Create("config/db/password", "e2", "iv2"),
            VaultSecret.Create("config/redis", "e3", "iv3"),
        };
        _repoMock.Setup(r => r.ListSecretsAsync("config", It.IsAny<CancellationToken>()))
            .ReturnsAsync(secrets);

        // Act
        var result = (await _sut.ListSecretsAsync("config")).ToList();

        // Assert
        result.Should().Contain(e => e.Name == "db/" && e.Type == "folder");
        result.Should().Contain(e => e.Name == "redis" && e.Type == "secret");
    }

    [Fact]
    public async Task GetVersionsAsync_ShouldReturnVersionHistory()
    {
        // Arrange
        var versions = new List<VaultSecret>
        {
            VaultSecret.Create("path", "e1", "iv1", version: 1),
            VaultSecret.Create("path", "e2", "iv2", version: 2),
        };
        _repoMock.Setup(r => r.GetSecretVersionsAsync("path", It.IsAny<CancellationToken>()))
            .ReturnsAsync(versions);

        // Act
        var result = (await _sut.GetVersionsAsync("path")).ToList();

        // Assert
        result.Should().HaveCount(2);
        result[0].Version.Should().Be(1);
        result[1].Version.Should().Be(2);
    }

    [Fact]
    public async Task ImportSecretsAsync_ShouldImportAll()
    {
        // Arrange
        var secrets = new Dictionary<string, string>
        {
            ["db/host"] = "localhost",
            ["db/port"] = "5432"
        };
        _repoMock.Setup(r => r.GetLatestVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repoMock.Setup(r => r.AddSecretAsync(It.IsAny<VaultSecret>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.ImportSecretsAsync(secrets, "config");

        // Assert
        result.Imported.Should().Be(2);
        result.Failed.Should().Be(0);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ImportSecretsAsync_ShouldTrackFailures()
    {
        // Arrange
        var secrets = new Dictionary<string, string>
        {
            ["ok"] = "value",
            ["fail"] = "value"
        };
        _repoMock.Setup(r => r.GetLatestVersionAsync("ok", It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repoMock.Setup(r => r.GetLatestVersionAsync("fail", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("DB error"));
        _repoMock.Setup(r => r.AddSecretAsync(It.IsAny<VaultSecret>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.ImportSecretsAsync(secrets);

        // Assert
        result.Imported.Should().Be(1);
        result.Failed.Should().Be(1);
        result.Errors.Should().ContainSingle(e => e.Contains("fail"));
    }

    [Fact]
    public async Task EncryptionRoundTrip_ShouldPreservePlaintext()
    {
        // Arrange — Test that encrypt then decrypt works
        var path = "roundtrip/test";
        var plaintext = "Hello, World! 🌍 Đây là Unicode text.";
        VaultSecret? stored = null;

        _repoMock.Setup(r => r.GetLatestVersionAsync(path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _repoMock.Setup(r => r.AddSecretAsync(It.IsAny<VaultSecret>(), It.IsAny<CancellationToken>()))
            .Callback<VaultSecret, CancellationToken>((s, _) => stored = s)
            .Returns(Task.CompletedTask);

        await _sut.PutSecretAsync(path, plaintext);

        _repoMock.Setup(r => r.GetSecretAsync(path, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => stored);

        // Act
        var result = await _sut.GetSecretAsync(path);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Should().Be(plaintext);
    }

    [Fact]
    public async Task PutSecretAsync_ShouldProduceDifferentCiphertextForSamePlaintext()
    {
        // AES-GCM with random IV should produce different ciphertext each time
        var path1 = "test1";
        var path2 = "test2";
        VaultSecret? stored1 = null, stored2 = null;

        _repoMock.Setup(r => r.GetLatestVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var callCount = 0;
        _repoMock.Setup(r => r.AddSecretAsync(It.IsAny<VaultSecret>(), It.IsAny<CancellationToken>()))
            .Callback<VaultSecret, CancellationToken>((s, _) =>
            {
                if (callCount++ == 0) stored1 = s;
                else stored2 = s;
            })
            .Returns(Task.CompletedTask);

        await _sut.PutSecretAsync(path1, "same-plaintext");
        await _sut.PutSecretAsync(path2, "same-plaintext");

        // Ciphertexts should differ due to random IV
        stored1!.EncryptedData.Should().NotBe(stored2!.EncryptedData);
        stored1.Iv.Should().NotBe(stored2.Iv);
    }
}
