using System.Text;
using System.Text.Json;
using FluentAssertions;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Services.Kms;
using Microsoft.Extensions.Logging;
using Moq;

namespace IVF.Tests.Infrastructure.Vault;

public class KmsProviderTests
{
    private readonly Mock<IVaultRepository> _repoMock;
    private readonly LocalKmsProvider _sut;
    private readonly Dictionary<string, string> _settingsStore = new();

    public KmsProviderTests()
    {
        _repoMock = new Mock<IVaultRepository>();
        var loggerMock = new Mock<ILogger<LocalKmsProvider>>();

        // Simulate in-memory settings store
        _repoMock.Setup(r => r.SaveSettingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((key, val, _) => _settingsStore[key] = val)
            .Returns(Task.CompletedTask);

        _repoMock.Setup(r => r.GetSettingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) =>
                _settingsStore.TryGetValue(key, out var val) ? VaultSetting.Create(key, val) : null);

        _repoMock.Setup(r => r.GetAllSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _settingsStore.Select(kv => VaultSetting.Create(kv.Key, kv.Value)).ToList());

        _sut = new LocalKmsProvider(_repoMock.Object, loggerMock.Object);
    }

    [Fact]
    public async Task IsHealthy_ReturnsTrue()
    {
        var result = await _sut.IsHealthyAsync();
        result.Should().BeTrue();
    }

    [Fact]
    public void ProviderName_IsLocal()
    {
        _sut.ProviderName.Should().Be("Local");
    }

    // ─── Key Management ──────────────────────────────────

    [Fact]
    public async Task CreateKey_StoresKeyInSettings()
    {
        var request = new KmsCreateKeyRequest("test-key");
        var result = await _sut.CreateKeyAsync(request);

        result.KeyName.Should().Be("test-key");
        result.Version.Should().Be(1);
        result.Enabled.Should().BeTrue();
        result.Provider.Should().Be("Local");

        _settingsStore.Should().ContainKey("kms-key-test-key");
    }

    [Fact]
    public async Task CreateKey_DuplicateName_Throws()
    {
        await _sut.CreateKeyAsync(new KmsCreateKeyRequest("dup-key"));

        var act = () => _sut.CreateKeyAsync(new KmsCreateKeyRequest("dup-key"));
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task GetKeyInfo_ExistingKey_ReturnsInfo()
    {
        await _sut.CreateKeyAsync(new KmsCreateKeyRequest("info-key"));

        var info = await _sut.GetKeyInfoAsync("info-key");

        info.Should().NotBeNull();
        info!.KeyName.Should().Be("info-key");
    }

    [Fact]
    public async Task GetKeyInfo_NonExistent_ReturnsNull()
    {
        var info = await _sut.GetKeyInfoAsync("nonexistent");
        info.Should().BeNull();
    }

    [Fact]
    public async Task ListKeys_ReturnsAllKeys()
    {
        await _sut.CreateKeyAsync(new KmsCreateKeyRequest("key-1"));
        await _sut.CreateKeyAsync(new KmsCreateKeyRequest("key-2"));

        var keys = await _sut.ListKeysAsync();

        keys.Should().HaveCount(2);
        keys.Select(k => k.KeyName).Should().Contain(["key-1", "key-2"]);
    }

    [Fact]
    public async Task RotateKey_IncrementsVersionAndArchives()
    {
        await _sut.CreateKeyAsync(new KmsCreateKeyRequest("rotate-key"));

        var rotated = await _sut.RotateKeyAsync("rotate-key");

        rotated.Version.Should().Be(2);
        rotated.RotatedAt.Should().NotBeNull();

        // Should have archived v1
        _settingsStore.Should().ContainKey("kms-key-rotate-key-v1");
    }

    [Fact]
    public async Task RotateKey_NonExistent_Throws()
    {
        var act = () => _sut.RotateKeyAsync("nonexistent");
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ─── Encrypt / Decrypt ──────────────────────────────

    [Fact]
    public async Task EncryptDecrypt_RoundTrip_ReturnsOriginal()
    {
        await _sut.CreateKeyAsync(new KmsCreateKeyRequest("crypto-key"));

        var plaintext = Encoding.UTF8.GetBytes("Hello, enterprise vault!");
        var encrypted = await _sut.EncryptAsync("crypto-key", plaintext);

        encrypted.Ciphertext.Should().NotBeEmpty();
        encrypted.Iv.Should().NotBeEmpty();
        encrypted.Algorithm.Should().Be("AES-256-GCM");

        var decrypted = await _sut.DecryptAsync("crypto-key", encrypted.Ciphertext, encrypted.Iv);

        Encoding.UTF8.GetString(decrypted).Should().Be("Hello, enterprise vault!");
    }

    [Fact]
    public async Task Encrypt_WithNonExistentKey_Throws()
    {
        var act = () => _sut.EncryptAsync("nonexistent", [1, 2, 3]);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ─── Key Wrap / Unwrap ──────────────────────────────

    [Fact]
    public async Task WrapUnwrap_RoundTrip_ReturnsOriginalKey()
    {
        await _sut.CreateKeyAsync(new KmsCreateKeyRequest("wrap-key"));

        var dek = new byte[32]; // 256-bit DEK
        new Random(42).NextBytes(dek);

        var wrapped = await _sut.WrapKeyAsync("wrap-key", dek);

        wrapped.WrappedKey.Should().NotBeEmpty();
        wrapped.Iv.Should().NotBeEmpty();

        var unwrapped = await _sut.UnwrapKeyAsync("wrap-key", wrapped.WrappedKey, wrapped.Iv);

        unwrapped.Should().BeEquivalentTo(dek);
    }

    [Fact]
    public async Task Decrypt_WrongKey_ThrowsCryptoException()
    {
        await _sut.CreateKeyAsync(new KmsCreateKeyRequest("key-a"));
        await _sut.CreateKeyAsync(new KmsCreateKeyRequest("key-b"));

        var plaintext = Encoding.UTF8.GetBytes("secret data");
        var encrypted = await _sut.EncryptAsync("key-a", plaintext);

        // Try to decrypt with a different key — should fail
        var act = () => _sut.DecryptAsync("key-b", encrypted.Ciphertext, encrypted.Iv);
        await act.Should().ThrowAsync<Exception>();
    }
}
