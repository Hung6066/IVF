using FluentAssertions;
using IVF.Application.Common.Interfaces;
using IVF.Domain.Entities;
using IVF.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;

namespace IVF.Tests.Infrastructure.Vault;

public class VaultTokenValidatorTests
{
    private readonly Mock<IVaultRepository> _repoMock;
    private readonly Mock<ILogger<VaultTokenValidator>> _loggerMock;
    private readonly VaultTokenValidator _sut;

    public VaultTokenValidatorTests()
    {
        _repoMock = new Mock<IVaultRepository>();
        _loggerMock = new Mock<ILogger<VaultTokenValidator>>();
        var metrics = CreateVaultMetrics();
        _sut = new VaultTokenValidator(_repoMock.Object, _loggerMock.Object, metrics);
    }

    private static VaultMetrics CreateVaultMetrics()
    {
        var meterFactoryMock = new Mock<IMeterFactory>();
        meterFactoryMock.Setup(f => f.Create(It.IsAny<MeterOptions>()))
            .Returns((MeterOptions opts) => new Meter(opts));
        return new VaultMetrics(meterFactoryMock.Object);
    }

    private static string ComputeHash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    [Fact]
    public async Task ValidateTokenAsync_WithValidToken_ShouldReturnResult()
    {
        // Arrange
        var rawToken = "my-secret-token";
        var hash = ComputeHash(rawToken);
        var token = VaultToken.Create(hash, "Test Token", ["read-policy"], "service", 3600, 100);

        _repoMock.Setup(r => r.GetTokenByHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        _repoMock.Setup(r => r.UpdateTokenAsync(It.IsAny<VaultToken>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.ValidateTokenAsync(rawToken);

        // Assert
        result.Should().NotBeNull();
        result!.DisplayName.Should().Be("Test Token");
        result.Policies.Should().Contain("read-policy");
        result.UsesCount.Should().Be(1); // Incremented once
    }

    [Fact]
    public async Task ValidateTokenAsync_ShouldIncrementUsageCount()
    {
        // Arrange
        var rawToken = "token-usage-test";
        var hash = ComputeHash(rawToken);
        var token = VaultToken.Create(hash, numUses: 10);

        _repoMock.Setup(r => r.GetTokenByHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        _repoMock.Setup(r => r.UpdateTokenAsync(It.IsAny<VaultToken>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.ValidateTokenAsync(rawToken);

        // Assert
        _repoMock.Verify(r => r.UpdateTokenAsync(
            It.Is<VaultToken>(t => t.UsesCount == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ValidateTokenAsync_WithEmptyToken_ShouldReturnNull()
    {
        var result = await _sut.ValidateTokenAsync("");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateTokenAsync_WithWhitespaceToken_ShouldReturnNull()
    {
        var result = await _sut.ValidateTokenAsync("   ");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateTokenAsync_WithNonExistentToken_ShouldReturnNull()
    {
        // Arrange
        _repoMock.Setup(r => r.GetTokenByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VaultToken?)null);

        // Act
        var result = await _sut.ValidateTokenAsync("does-not-exist");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateTokenAsync_WithRevokedToken_ShouldReturnNull()
    {
        // Arrange
        var rawToken = "revoked-token";
        var hash = ComputeHash(rawToken);
        var token = VaultToken.Create(hash);
        token.Revoke();

        _repoMock.Setup(r => r.GetTokenByHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        // Act
        var result = await _sut.ValidateTokenAsync(rawToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateTokenAsync_WithExhaustedToken_ShouldReturnNull()
    {
        // Arrange
        var rawToken = "exhausted-token";
        var hash = ComputeHash(rawToken);
        var token = VaultToken.Create(hash, numUses: 1);
        token.IncrementUse(); // Now exhausted

        _repoMock.Setup(r => r.GetTokenByHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        // Act
        var result = await _sut.ValidateTokenAsync(rawToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task HasCapabilityAsync_WithMatchingPolicy_ShouldReturnTrue()
    {
        // Arrange
        var rawToken = "cap-token";
        var hash = ComputeHash(rawToken);
        var token = VaultToken.Create(hash, policies: ["secrets-read"]);

        var policy = VaultPolicy.Create("secrets-read", "secrets/*", ["read", "list"]);

        _repoMock.Setup(r => r.GetTokenByHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        _repoMock.Setup(r => r.GetPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultPolicy> { policy });

        // Act
        var result = await _sut.HasCapabilityAsync(rawToken, "secrets/db", "read");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasCapabilityAsync_WithNoMatchingPolicy_ShouldReturnFalse()
    {
        // Arrange
        var rawToken = "no-cap-token";
        var hash = ComputeHash(rawToken);
        var token = VaultToken.Create(hash, policies: ["secrets-read"]);

        var policy = VaultPolicy.Create("secrets-read", "secrets/*", ["read"]);

        _repoMock.Setup(r => r.GetTokenByHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        _repoMock.Setup(r => r.GetPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultPolicy> { policy });

        // Act — requesting "delete" but only "read" granted
        var result = await _sut.HasCapabilityAsync(rawToken, "secrets/db", "delete");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasCapabilityAsync_WithSudoCapability_ShouldGrantAll()
    {
        // Arrange
        var rawToken = "sudo-token";
        var hash = ComputeHash(rawToken);
        var token = VaultToken.Create(hash, policies: ["admin"]);

        var policy = VaultPolicy.Create("admin", "**", ["sudo"]);

        _repoMock.Setup(r => r.GetTokenByHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        _repoMock.Setup(r => r.GetPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultPolicy> { policy });

        // Act — sudo should grant any capability
        var result = await _sut.HasCapabilityAsync(rawToken, "anything/here", "delete");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasCapabilityAsync_WithInvalidToken_ShouldReturnFalse()
    {
        // Arrange
        _repoMock.Setup(r => r.GetTokenByHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VaultToken?)null);

        // Act
        var result = await _sut.HasCapabilityAsync("invalid", "path", "read");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasCapabilityAsync_SingleWildcard_ShouldMatchSingleSegment()
    {
        // Arrange
        var rawToken = "wildcard-token";
        var hash = ComputeHash(rawToken);
        var token = VaultToken.Create(hash, policies: ["env-read"]);

        var policy = VaultPolicy.Create("env-read", "config/*/connection", ["read"]);

        _repoMock.Setup(r => r.GetTokenByHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        _repoMock.Setup(r => r.GetPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultPolicy> { policy });

        // Act — * should match single segment
        var matchSingle = await _sut.HasCapabilityAsync(rawToken, "config/db/connection", "read");
        var noMatchMulti = await _sut.HasCapabilityAsync(rawToken, "config/db/prod/connection", "read");

        // Assert
        matchSingle.Should().BeTrue();
        noMatchMulti.Should().BeFalse();
    }

    [Fact]
    public async Task HasCapabilityAsync_DoubleWildcard_ShouldMatchAnyDepth()
    {
        // Arrange
        var rawToken = "globstar-token";
        var hash = ComputeHash(rawToken);
        var token = VaultToken.Create(hash, policies: ["all-read"]);

        var policy = VaultPolicy.Create("all-read", "config/**", ["read"]);

        _repoMock.Setup(r => r.GetTokenByHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        _repoMock.Setup(r => r.GetPoliciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VaultPolicy> { policy });

        // Act — ** should match any depth
        var match1 = await _sut.HasCapabilityAsync(rawToken, "config/db", "read");
        var match2 = await _sut.HasCapabilityAsync(rawToken, "config/db/prod/connection", "read");

        // Assert
        match1.Should().BeTrue();
        match2.Should().BeTrue();
    }
}
